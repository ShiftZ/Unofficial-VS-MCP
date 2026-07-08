using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using VsMcp.Extension.McpServer;
using VsMcp.Extension.Services;
using VsMcp.Shared;
using VsMcp.Shared.Protocol;

namespace VsMcp.Extension.Tools
{
    public static class InProcTools
    {
        private const int MaxHandles = 100;
        private const int MaxEnumerationLimit = 500;

        private static readonly object HandlesLock = new object();
        private static readonly Dictionary<string, HandleEntry> Handles = new Dictionary<string, HandleEntry>(StringComparer.OrdinalIgnoreCase);
        private static long _nextHandleId;

        public static void Register(McpToolRegistry registry, VsServiceAccessor accessor)
        {
            registry.Register(
                new McpToolDefinition(
                    "vs_inproc_invoke",
                    "ADVANCED DIAGNOSTIC: resolve a VS in-process target (service, DTE, static type, or previous handle), read or invoke a public member by reflection, and return a JSON-safe summary. Use this to probe VSSDK services from the extension host. Complex return values are stored as handles by default for follow-up calls; release them with vs_inproc_release.",
                    CreateInvokeSchema()),
                args => InvokeAsync(accessor, args));

            registry.Register(
                new McpToolDefinition(
                    "vs_inproc_handles",
                    "List diagnostic object handles retained by vs_inproc_invoke.",
                    SchemaBuilder.Empty()),
                args => Task.FromResult(ListHandles()));

            registry.Register(
                new McpToolDefinition(
                    "vs_inproc_release",
                    "Release one diagnostic object handle, or all handles. Set dispose=true only when you intentionally want to call IDisposable.Dispose on released objects.",
                    CreateReleaseSchema()),
                args => Task.FromResult(ReleaseHandles(args)));
        }

        private static JObject CreateInvokeSchema()
        {
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["target"] = new JObject
                    {
                        ["type"] = "object",
                        ["description"] = "Target object. kind='service' requires serviceType and may include castType; kind='handle' requires id; kind='dte' uses EnvDTE.DTE2; kind='static' requires type.",
                        ["properties"] = new JObject
                        {
                            ["kind"] = new JObject
                            {
                                ["type"] = "string",
                                ["enum"] = new JArray("service", "handle", "dte", "static")
                            },
                            ["serviceType"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Service type name, for example Microsoft.VisualStudio.Shell.FindAllReferences.SVsFindAllReferences."
                            },
                            ["castType"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Optional interface/base type used for member lookup, for example Microsoft.VisualStudio.Shell.FindAllReferences.IFindAllReferencesService."
                            },
                            ["id"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Handle id returned by a previous vs_inproc_invoke call."
                            },
                            ["type"] = new JObject
                            {
                                ["type"] = "string",
                                ["description"] = "Type name for kind='static'."
                            }
                        },
                        ["required"] = new JArray("kind")
                    },
                    ["operation"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray("get", "invoke", "inspect"),
                        ["description"] = "Operation to perform. Defaults to invoke when args are supplied, otherwise get when member is supplied, otherwise inspect."
                    },
                    ["member"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Property/field path to read, or method path to invoke. Dotted paths are supported, for example TableControl.Entries or TableControl.ForceUpdateAsync."
                    },
                    ["args"] = new JObject
                    {
                        ["type"] = "array",
                        ["description"] = "Arguments for invoke. Primitive JSON values are converted to parameter types. Use {\"handle\":\"obj-1\"} to pass a retained object handle, or {\"type\":\"Namespace.Type\"} to pass a System.Type."
                    },
                    ["thread"] = new JObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JArray("ui", "background"),
                        ["description"] = "Thread used to run the operation. Defaults to ui because most VS services and WPF objects are UI-thread-affine."
                    },
                    ["return"] = new JObject
                    {
                        ["type"] = "object",
                        ["description"] = "Return shaping options.",
                        ["properties"] = new JObject
                        {
                            ["storeHandle"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Store complex returned objects as follow-up handles. Defaults to true."
                            },
                            ["inspectDepth"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "How many levels of public readable properties to inspect. Defaults to 1."
                            },
                            ["enumerate"] = new JObject
                            {
                                ["type"] = "boolean",
                                ["description"] = "Enumerate IEnumerable results. Defaults to false."
                            },
                            ["limit"] = new JObject
                            {
                                ["type"] = "integer",
                                ["description"] = "Maximum enumerable items to return. Defaults to 50, capped at 500."
                            },
                            ["propertyNames"] = new JObject
                            {
                                ["type"] = "array",
                                ["description"] = "Optional public property names to inspect instead of all readable non-indexed properties.",
                                ["items"] = new JObject { ["type"] = "string" }
                            }
                        }
                    }
                },
                ["required"] = new JArray("target")
            };
        }

        private static JObject CreateReleaseSchema()
        {
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["id"] = new JObject
                    {
                        ["type"] = "string",
                        ["description"] = "Handle id to release."
                    },
                    ["all"] = new JObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Release all handles."
                    },
                    ["dispose"] = new JObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Also call IDisposable.Dispose on released objects. Defaults to false."
                    }
                }
            };
        }

        private static async Task<McpToolResult> InvokeAsync(VsServiceAccessor accessor, JObject args)
        {
            var thread = args.Value<string>("thread") ?? "ui";
            if (!thread.Equals("ui", StringComparison.OrdinalIgnoreCase) &&
                !thread.Equals("background", StringComparison.OrdinalIgnoreCase))
            {
                return McpToolResult.Error("Parameter 'thread' must be 'ui' or 'background'.");
            }

            try
            {
                if (thread.Equals("background", StringComparison.OrdinalIgnoreCase))
                    return await InvokeCoreAsync(accessor, args);

                return await accessor.RunOnUIThreadAsync(() => InvokeCoreAsync(accessor, args));
            }
            catch (TargetInvocationException ex)
            {
                return McpToolResult.Error($"Invocation failed: {GetExceptionMessage(ex.InnerException ?? ex)}");
            }
            catch (Exception ex)
            {
                return McpToolResult.Error(GetExceptionMessage(ex));
            }
        }

        private static async Task<McpToolResult> InvokeCoreAsync(VsServiceAccessor accessor, JObject args)
        {
            var targetSpec = args["target"] as JObject;
            if (targetSpec == null)
                return McpToolResult.Error("Parameter 'target' is required and must be an object.");

            var target = await ResolveTargetAsync(accessor, targetSpec);
            if (target.Error != null)
                return target.Error;

            var operation = args.Value<string>("operation");
            var member = args.Value<string>("member");
            var argTokens = args["args"] as JArray ?? new JArray();

            if (string.IsNullOrWhiteSpace(operation))
            {
                if (argTokens.Count > 0)
                    operation = "invoke";
                else if (!string.IsNullOrWhiteSpace(member))
                    operation = "get";
                else
                    operation = "inspect";
            }

            object value;
            Type preferredType;

            switch (operation.ToLowerInvariant())
            {
                case "inspect":
                    if (string.IsNullOrWhiteSpace(member))
                    {
                        value = target.Value;
                        preferredType = target.PreferredType;
                    }
                    else
                    {
                        var read = ReadMemberPath(target.Value, target.PreferredType, member);
                        if (read.Error != null)
                            return read.Error;
                        value = read.Value;
                        preferredType = read.PreferredType;
                    }
                    break;

                case "get":
                    if (string.IsNullOrWhiteSpace(member))
                        return McpToolResult.Error("Parameter 'member' is required for operation 'get'.");

                    var get = ReadMemberPath(target.Value, target.PreferredType, member);
                    if (get.Error != null)
                        return get.Error;
                    value = get.Value;
                    preferredType = get.PreferredType;
                    break;

                case "invoke":
                    if (string.IsNullOrWhiteSpace(member))
                        return McpToolResult.Error("Parameter 'member' is required for operation 'invoke'.");

                    var invoke = await InvokeMemberPathAsync(target.Value, target.PreferredType, member, argTokens);
                    if (invoke.Error != null)
                        return invoke.Error;
                    value = invoke.Value;
                    preferredType = invoke.PreferredType;
                    break;

                default:
                    return McpToolResult.Error("Parameter 'operation' must be 'get', 'invoke', or 'inspect'.");
            }

            var options = ReturnOptions.From(args["return"] as JObject);
            var result = ToJsonSafe(value, preferredType, options, options.InspectDepth, new HashSet<object>(ReferenceEqualityComparer.Instance));

            return McpToolResult.Success(new
            {
                operation,
                member,
                target = target.Description,
                result
            });
        }

        private static async Task<TargetResolution> ResolveTargetAsync(VsServiceAccessor accessor, JObject targetSpec)
        {
            var kind = targetSpec.Value<string>("kind");
            if (string.IsNullOrWhiteSpace(kind))
                return TargetResolution.Failure("target.kind is required.");

            switch (kind.ToLowerInvariant())
            {
                case "service":
                    return await ResolveServiceTargetAsync(accessor, targetSpec);

                case "handle":
                    return ResolveHandleTarget(targetSpec);

                case "dte":
                    var dte = await accessor.GetDteAsync();
                    return TargetResolution.Success(dte, dte.GetType(), "dte");

                case "static":
                    var typeName = targetSpec.Value<string>("type");
                    if (string.IsNullOrWhiteSpace(typeName))
                        return TargetResolution.Failure("target.type is required for kind='static'.");

                    var type = FindType(typeName);
                    if (type == null)
                        return TargetResolution.Failure($"Type '{typeName}' was not found in loaded assemblies.");

                    return TargetResolution.Success(null, type, $"static:{type.FullName}");

                default:
                    return TargetResolution.Failure("target.kind must be 'service', 'handle', 'dte', or 'static'.");
            }
        }

        private static async Task<TargetResolution> ResolveServiceTargetAsync(VsServiceAccessor accessor, JObject targetSpec)
        {
            var serviceTypeName = targetSpec.Value<string>("serviceType");
            if (string.IsNullOrWhiteSpace(serviceTypeName))
                return TargetResolution.Failure("target.serviceType is required for kind='service'.");

            var serviceType = FindType(serviceTypeName);
            if (serviceType == null)
                return TargetResolution.Failure($"Service type '{serviceTypeName}' was not found in loaded assemblies.");

            var service = await accessor.GetServiceAsync(serviceType);
            if (service == null)
                return TargetResolution.Failure($"Service '{serviceTypeName}' returned null.");

            var preferredType = service.GetType();
            var castTypeName = targetSpec.Value<string>("castType");
            if (!string.IsNullOrWhiteSpace(castTypeName))
            {
                var castType = FindType(castTypeName);
                if (castType == null)
                    return TargetResolution.Failure($"Cast type '{castTypeName}' was not found in loaded assemblies.");

                preferredType = castType;
            }

            return TargetResolution.Success(service, preferredType, $"service:{serviceType.FullName}");
        }

        private static TargetResolution ResolveHandleTarget(JObject targetSpec)
        {
            var id = targetSpec.Value<string>("id");
            if (string.IsNullOrWhiteSpace(id))
                return TargetResolution.Failure("target.id is required for kind='handle'.");

            lock (HandlesLock)
            {
                if (!Handles.TryGetValue(id, out var entry))
                    return TargetResolution.Failure($"Handle '{id}' was not found.");

                entry.LastAccessUtc = DateTime.UtcNow;
                return TargetResolution.Success(entry.Value, entry.PreferredType ?? entry.Value?.GetType(), $"handle:{id}");
            }
        }

        private static MemberAccessResult ReadMemberPath(object target, Type preferredType, string memberPath)
        {
            var current = target;
            var currentType = preferredType ?? target?.GetType();

            foreach (var segment in SplitPath(memberPath))
            {
                var read = ReadSingleMember(current, currentType, segment);
                if (read.Error != null)
                    return read;

                current = read.Value;
                currentType = read.PreferredType ?? current?.GetType();
            }

            return MemberAccessResult.Success(current, currentType);
        }

        private static async Task<MemberAccessResult> InvokeMemberPathAsync(object target, Type preferredType, string memberPath, JArray argTokens)
        {
            var segments = SplitPath(memberPath);
            if (segments.Length == 0)
                return MemberAccessResult.Failure("Member path is empty.");

            object current = target;
            Type currentType = preferredType ?? target?.GetType();

            for (int i = 0; i < segments.Length - 1; i++)
            {
                var read = ReadSingleMember(current, currentType, segments[i]);
                if (read.Error != null)
                    return read;

                current = read.Value;
                currentType = read.PreferredType ?? current?.GetType();
            }

            var invoked = InvokeSingleMethod(current, currentType, segments[segments.Length - 1], argTokens);
            if (invoked.Error != null)
                return invoked;

            var awaited = await AwaitIfTaskAsync(invoked.Value);
            var resultType = UnwrapTaskType(invoked.PreferredType) ?? awaited?.GetType();
            return MemberAccessResult.Success(awaited, resultType);
        }

        private static MemberAccessResult ReadSingleMember(object target, Type preferredType, string name)
        {
            var type = preferredType ?? target?.GetType();
            if (type == null)
                return MemberAccessResult.Failure($"Cannot read member '{name}' from null target.");

            var flags = BindingFlags.Public | BindingFlags.FlattenHierarchy |
                        (target == null ? BindingFlags.Static : BindingFlags.Instance);

            var property = FindProperty(type, name, flags);
            if (property != null)
            {
                if (property.GetIndexParameters().Length != 0)
                    return MemberAccessResult.Failure($"Property '{name}' is indexed and cannot be read by this tool.");

                try
                {
                    return MemberAccessResult.Success(property.GetValue(target), property.PropertyType);
                }
                catch when (target != null && preferredType != null && preferredType != target.GetType())
                {
                    property = FindProperty(target.GetType(), name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    if (property == null || property.GetIndexParameters().Length != 0)
                        throw;

                    return MemberAccessResult.Success(property.GetValue(target), property.PropertyType);
                }
            }

            var field = FindField(type, name, flags);
            if (field != null)
                return MemberAccessResult.Success(field.GetValue(target), field.FieldType);

            if (target != null && preferredType != null && preferredType != target.GetType())
                return ReadSingleMember(target, target.GetType(), name);

            return MemberAccessResult.Failure($"Member '{name}' was not found on type '{type.FullName}'.");
        }

        private static MemberAccessResult InvokeSingleMethod(object target, Type preferredType, string name, JArray argTokens)
        {
            var type = preferredType ?? target?.GetType();
            if (type == null)
                return MemberAccessResult.Failure($"Cannot invoke method '{name}' on null target.");

            var flags = BindingFlags.Public | BindingFlags.FlattenHierarchy |
                        (target == null ? BindingFlags.Static : BindingFlags.Instance);

            var methods = type.GetMethods(flags)
                .Where(method => method.Name == name)
                .OrderBy(method => method.GetParameters().Count(parameter => !parameter.IsOptional))
                .ToList();

            if (methods.Count == 0 && target != null && preferredType != null && preferredType != target.GetType())
                return InvokeSingleMethod(target, target.GetType(), name, argTokens);

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                var requiredCount = parameters.Count(parameter => !parameter.IsOptional);
                if (argTokens.Count < requiredCount || argTokens.Count > parameters.Length)
                    continue;

                var converted = new object[parameters.Length];
                var ok = true;

                for (int i = 0; i < parameters.Length; i++)
                {
                    if (i >= argTokens.Count)
                    {
                        converted[i] = parameters[i].DefaultValue;
                        continue;
                    }

                    if (!TryConvertArgument(argTokens[i], parameters[i].ParameterType, out converted[i], out _))
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok)
                    continue;

                return MemberAccessResult.Success(method.Invoke(target, converted), method.ReturnType);
            }

            return MemberAccessResult.Failure($"No public overload '{name}' on '{type.FullName}' accepted {argTokens.Count} argument(s).");
        }

        private static async Task<object> AwaitIfTaskAsync(object value)
        {
            var task = value as Task;
            if (task == null)
                return value;

            await task;

            var taskType = task.GetType();
            if (!taskType.IsGenericType)
                return null;

            var resultProperty = taskType.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            return resultProperty?.GetValue(task);
        }

        private static Type UnwrapTaskType(Type type)
        {
            if (type == null)
                return null;

            if (type == typeof(Task))
                return null;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                return type.GetGenericArguments()[0];

            return type;
        }

        private static bool TryConvertArgument(JToken token, Type targetType, out object value, out string error)
        {
            value = null;
            error = null;

            if (targetType.IsByRef)
            {
                error = "ref/out parameters are not supported.";
                return false;
            }

            if (token == null || token.Type == JTokenType.Null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                    return true;

                error = $"Cannot pass null to value type '{targetType.FullName}'.";
                return false;
            }

            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null)
                targetType = nullableType;

            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                var handleId = obj.Value<string>("handle");
                if (!string.IsNullOrWhiteSpace(handleId))
                {
                    lock (HandlesLock)
                    {
                        if (!Handles.TryGetValue(handleId, out var entry))
                        {
                            error = $"Handle '{handleId}' was not found.";
                            return false;
                        }

                        if (entry.Value != null && !targetType.IsInstanceOfType(entry.Value) && targetType != typeof(object))
                        {
                            error = $"Handle '{handleId}' value type '{entry.Value.GetType().FullName}' is not assignable to '{targetType.FullName}'.";
                            return false;
                        }

                        entry.LastAccessUtc = DateTime.UtcNow;
                        value = entry.Value;
                        return true;
                    }
                }

                var typeName = obj.Value<string>("type");
                if (!string.IsNullOrWhiteSpace(typeName))
                {
                    var type = FindType(typeName);
                    if (type == null)
                    {
                        error = $"Type '{typeName}' was not found.";
                        return false;
                    }

                    if (targetType != typeof(Type) && targetType != typeof(object))
                    {
                        error = $"Type token can only be passed to System.Type or object parameters, not '{targetType.FullName}'.";
                        return false;
                    }

                    value = type;
                    return true;
                }
            }

            try
            {
                if (targetType == typeof(object))
                {
                    value = token is JValue jValue ? jValue.Value : token.ToObject<object>();
                    return true;
                }

                if (targetType == typeof(string))
                {
                    value = token.Type == JTokenType.String ? token.Value<string>() : token.ToString();
                    return true;
                }

                if (targetType == typeof(Type))
                {
                    var typeName = token.Value<string>();
                    var type = FindType(typeName);
                    if (type == null)
                    {
                        error = $"Type '{typeName}' was not found.";
                        return false;
                    }

                    value = type;
                    return true;
                }

                if (targetType.IsEnum)
                {
                    value = token.Type == JTokenType.Integer
                        ? Enum.ToObject(targetType, token.Value<int>())
                        : Enum.Parse(targetType, token.Value<string>(), ignoreCase: true);
                    return true;
                }

                if (targetType == typeof(Guid))
                {
                    value = Guid.Parse(token.Value<string>());
                    return true;
                }

                if (typeof(JToken).IsAssignableFrom(targetType))
                {
                    value = token;
                    return true;
                }

                value = token.ToObject(targetType);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                value = null;
                return false;
            }
        }

        private static JToken ToJsonSafe(object value, Type preferredType, ReturnOptions options, int depth, HashSet<object> visited)
        {
            if (value == null)
                return JValue.CreateNull();

            var valueType = value.GetType();
            if (IsSimple(valueType))
                return JToken.FromObject(ConvertSimple(value));

            if (!visited.Add(value))
                return new JObject
                {
                    ["type"] = valueType.FullName,
                    ["cycle"] = true
                };

            try
            {
                var obj = new JObject
                {
                    ["type"] = valueType.FullName
                };

                if (preferredType != null && preferredType != valueType)
                    obj["preferredType"] = preferredType.FullName;

                if (options.StoreHandle)
                    obj["handleId"] = StoreHandle(value, preferredType);

                AddStringValue(obj, value);

                var enumerable = value as IEnumerable;
                if (enumerable != null && !(value is string))
                {
                    AddEnumerableSummary(obj, enumerable);
                    if (options.Enumerate)
                    {
                        var items = new JArray();
                        int count = 0;
                        foreach (var item in enumerable)
                        {
                            if (count >= options.Limit)
                                break;

                            items.Add(ToJsonSafe(item, item?.GetType(), options, Math.Max(0, depth - 1), visited));
                            count++;
                        }

                        obj["items"] = items;
                        obj["returnedItems"] = count;
                        obj["truncated"] = count >= options.Limit;
                    }
                }

                if (depth > 0)
                {
                    var properties = InspectProperties(value, preferredType ?? valueType, options, depth - 1, visited);
                    if (properties.Count > 0)
                        obj["properties"] = properties;
                }

                return obj;
            }
            finally
            {
                visited.Remove(value);
            }
        }

        private static JObject InspectProperties(object value, Type preferredType, ReturnOptions options, int depth, HashSet<object> visited)
        {
            var properties = new JObject();
            var propertyInfos = GetInspectableProperties(preferredType, value.GetType(), options.PropertyNames);

            foreach (var property in propertyInfos)
            {
                try
                {
                    var propertyValue = property.GetValue(value);
                    properties[property.Name] = ToJsonSafe(propertyValue, property.PropertyType, options, depth, visited);
                }
                catch (Exception ex)
                {
                    properties[property.Name] = new JObject
                    {
                        ["error"] = GetExceptionMessage(ex)
                    };
                }
            }

            return properties;
        }

        private static IEnumerable<PropertyInfo> GetInspectableProperties(Type preferredType, Type runtimeType, HashSet<string> propertyNames)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in new[] { preferredType, runtimeType }.Where(t => t != null).Distinct())
            {
                foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length != 0)
                        continue;

                    if (propertyNames != null && !propertyNames.Contains(property.Name))
                        continue;

                    if (seen.Add(property.Name))
                        yield return property;
                }
            }
        }

        private static void AddEnumerableSummary(JObject obj, IEnumerable enumerable)
        {
            var countProperty = enumerable.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
            if (countProperty == null || countProperty.GetIndexParameters().Length != 0)
                return;

            try
            {
                var count = countProperty.GetValue(enumerable);
                if (count != null)
                    obj["count"] = JToken.FromObject(count);
            }
            catch { }
        }

        private static void AddStringValue(JObject obj, object value)
        {
            try
            {
                var text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text) && text != value.GetType().FullName)
                    obj["stringValue"] = text;
            }
            catch { }
        }

        private static bool IsSimple(Type type)
        {
            return type.IsPrimitive ||
                   type.IsEnum ||
                   type == typeof(string) ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(DateTimeOffset) ||
                   type == typeof(TimeSpan) ||
                   type == typeof(Guid) ||
                   type == typeof(Uri);
        }

        private static object ConvertSimple(object value)
        {
            if (value == null)
                return null;

            var type = value.GetType();
            if (type.IsEnum)
                return value.ToString();

            if (value is DateTime dateTime)
                return dateTime.ToString("o", CultureInfo.InvariantCulture);

            if (value is DateTimeOffset dateTimeOffset)
                return dateTimeOffset.ToString("o", CultureInfo.InvariantCulture);

            if (value is TimeSpan timeSpan)
                return timeSpan.ToString();

            return value;
        }

        private static string StoreHandle(object value, Type preferredType)
        {
            if (value == null || IsSimple(value.GetType()))
                return null;

            lock (HandlesLock)
            {
                foreach (var kvp in Handles)
                {
                    if (ReferenceEquals(kvp.Value.Value, value))
                    {
                        kvp.Value.LastAccessUtc = DateTime.UtcNow;
                        return kvp.Key;
                    }
                }

                while (Handles.Count >= MaxHandles)
                {
                    var oldest = Handles.OrderBy(kvp => kvp.Value.LastAccessUtc).First();
                    Handles.Remove(oldest.Key);
                }

                var id = "obj-" + Interlocked.Increment(ref _nextHandleId).ToString(CultureInfo.InvariantCulture);
                Handles[id] = new HandleEntry
                {
                    Id = id,
                    Value = value,
                    PreferredType = preferredType,
                    CreatedUtc = DateTime.UtcNow,
                    LastAccessUtc = DateTime.UtcNow
                };
                return id;
            }
        }

        private static McpToolResult ListHandles()
        {
            lock (HandlesLock)
            {
                return McpToolResult.Success(new
                {
                    count = Handles.Count,
                    handles = Handles.Values
                        .OrderBy(entry => entry.Id)
                        .Select(entry => new
                        {
                            id = entry.Id,
                            type = entry.Value?.GetType().FullName,
                            preferredType = entry.PreferredType?.FullName,
                            createdUtc = entry.CreatedUtc.ToString("o", CultureInfo.InvariantCulture),
                            lastAccessUtc = entry.LastAccessUtc.ToString("o", CultureInfo.InvariantCulture)
                        })
                        .ToArray()
                });
            }
        }

        private static McpToolResult ReleaseHandles(JObject args)
        {
            var id = args.Value<string>("id");
            var all = args.Value<bool?>("all") == true;
            var dispose = args.Value<bool?>("dispose") == true;

            if (string.IsNullOrWhiteSpace(id) && !all)
                return McpToolResult.Error("Specify either 'id' or all=true.");

            var released = new List<object>();
            lock (HandlesLock)
            {
                var entries = all
                    ? Handles.Values.ToList()
                    : Handles.TryGetValue(id, out var requestedEntry) ? new List<HandleEntry> { requestedEntry } : new List<HandleEntry>();

                if (entries.Count == 0)
                    return McpToolResult.Error($"Handle '{id}' was not found.");

                foreach (var entry in entries)
                {
                    Handles.Remove(entry.Id);
                    string disposeError = null;
                    if (dispose && entry.Value is IDisposable disposable)
                    {
                        try { disposable.Dispose(); }
                        catch (Exception ex) { disposeError = ex.Message; }
                    }

                    released.Add(new
                    {
                        id = entry.Id,
                        type = entry.Value?.GetType().FullName,
                        disposed = dispose && entry.Value is IDisposable,
                        disposeError
                    });
                }
            }

            return McpToolResult.Success(new
            {
                releasedCount = released.Count,
                released
            });
        }

        private static PropertyInfo FindProperty(Type type, string name, BindingFlags flags)
        {
            return type.GetProperty(name, flags) ??
                   type.GetProperties(flags).FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static FieldInfo FindField(Type type, string name, BindingFlags flags)
        {
            return type.GetField(name, flags) ??
                   type.GetFields(flags).FirstOrDefault(field => string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            var type = Type.GetType(typeName, throwOnError: false);
            if (type != null)
                return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                    if (type != null)
                        return type;
                }
                catch { }
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetTypes().FirstOrDefault(t => string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase) ||
                                                                   string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));
                    if (type != null)
                        return type;
                }
                catch { }
            }

            return null;
        }

        private static string[] SplitPath(string path)
        {
            return (path ?? "")
                .Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Trim())
                .Where(segment => segment.Length > 0)
                .ToArray();
        }

        private static string GetExceptionMessage(Exception ex)
        {
            return $"{ex.GetType().Name}: {ex.Message}";
        }

        private sealed class HandleEntry
        {
            public string Id { get; set; }
            public object Value { get; set; }
            public Type PreferredType { get; set; }
            public DateTime CreatedUtc { get; set; }
            public DateTime LastAccessUtc { get; set; }
        }

        private sealed class TargetResolution
        {
            public object Value { get; private set; }
            public Type PreferredType { get; private set; }
            public string Description { get; private set; }
            public McpToolResult Error { get; private set; }

            public static TargetResolution Success(object value, Type preferredType, string description)
            {
                return new TargetResolution
                {
                    Value = value,
                    PreferredType = preferredType,
                    Description = description
                };
            }

            public static TargetResolution Failure(string message)
            {
                return new TargetResolution
                {
                    Error = McpToolResult.Error(message)
                };
            }
        }

        private sealed class MemberAccessResult
        {
            public object Value { get; private set; }
            public Type PreferredType { get; private set; }
            public McpToolResult Error { get; private set; }

            public static MemberAccessResult Success(object value, Type preferredType)
            {
                return new MemberAccessResult
                {
                    Value = value,
                    PreferredType = preferredType
                };
            }

            public static MemberAccessResult Failure(string message)
            {
                return new MemberAccessResult
                {
                    Error = McpToolResult.Error(message)
                };
            }
        }

        private sealed class ReturnOptions
        {
            public bool StoreHandle { get; private set; } = true;
            public int InspectDepth { get; private set; } = 1;
            public bool Enumerate { get; private set; }
            public int Limit { get; private set; } = 50;
            public HashSet<string> PropertyNames { get; private set; }

            public static ReturnOptions From(JObject obj)
            {
                var options = new ReturnOptions();
                if (obj == null)
                    return options;

                if (obj["storeHandle"] != null)
                    options.StoreHandle = obj.Value<bool>("storeHandle");

                if (obj["inspectDepth"] != null)
                    options.InspectDepth = Math.Max(0, obj.Value<int>("inspectDepth"));

                if (obj["enumerate"] != null)
                    options.Enumerate = obj.Value<bool>("enumerate");

                if (obj["limit"] != null)
                    options.Limit = Math.Max(1, Math.Min(MaxEnumerationLimit, obj.Value<int>("limit")));

                var propertyNames = obj["propertyNames"] as JArray;
                if (propertyNames != null)
                {
                    options.PropertyNames = new HashSet<string>(
                        propertyNames.Values<string>().Where(name => !string.IsNullOrWhiteSpace(name)),
                        StringComparer.OrdinalIgnoreCase);
                }

                return options;
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
