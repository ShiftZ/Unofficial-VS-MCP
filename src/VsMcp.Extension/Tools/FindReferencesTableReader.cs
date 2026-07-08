using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Newtonsoft.Json.Linq;

namespace VsMcp.Extension.Tools
{
    internal static class FindReferencesTableReader
    {
        private const string FindAllReferencesWindowKind = "{A80FEBB4-E7E0-4147-B476-21AAF2453969}";
        private const int DefaultTimeoutMs = 30000;
        private const int MaximumTimeoutMs = 120000;
        private const int PollDelayMs = 100;

        internal static TimeSpan GetHandlerTimeout(JObject args)
        {
            return TimeSpan.FromMilliseconds(GetTimeoutMs(args) + 5000);
        }

        internal static int GetTimeoutMs(JObject args)
        {
            var timeoutMs = args.Value<int?>("timeoutMs") ?? DefaultTimeoutMs;
            if (timeoutMs < 1000)
                return 1000;
            if (timeoutMs > MaximumTimeoutMs)
                return MaximumTimeoutMs;
            return timeoutMs;
        }

        internal static async Task<FindReferencesReadResult> ReadAsync(
            DTE2 dte,
            List<FindReferencesWindowInfo> windowsBefore,
            DateTime deadline)
        {
            // C++ exposes no discovered non-UI find-references API, so this reads the FAR window's table model directly.
            var farWindow = await WaitForWindowAsync(dte, windowsBefore, deadline);
            if (farWindow == null)
                return new FindReferencesReadResult { WindowFound = false };

            var sourcesReady = await WaitForSourcesAsync(farWindow.Manager, deadline);
            var dataStable = false;

            if (sourcesReady)
                dataStable = await WaitForStableAsync(farWindow.TableControl, deadline);

            await ForceUpdateAsync(farWindow.TableControl, deadline);

            return new FindReferencesReadResult
            {
                WindowFound = true,
                WindowTitle = farWindow.Caption,
                Completed = sourcesReady && dataStable,
                SourceCount = GetSourceCount(farWindow.Manager),
                Sources = GetSourceSummaries(farWindow.Manager),
                References = ExtractRows(farWindow.TableControl)
            };
        }

        internal static List<FindReferencesWindowInfo> GetWindows(DTE2 dte)
        {
            var windows = new List<FindReferencesWindowInfo>();

            foreach (Window window in dte.Windows)
            {
                var info = TryGetWindow(window);
                if (info != null)
                    windows.Add(info);
            }

            return windows;
        }

        internal static async Task<int> CloseExistingWindowsAsync(DTE2 dte, DateTime deadline)
        {
            var existingWindows = GetWindows(dte);
            if (existingWindows.Count == 0)
                return 0;

            foreach (var window in existingWindows)
            {
                try
                {
                    window.Window.Close(vsSaveChanges.vsSaveChangesNo);
                }
                catch
                {
                    // Best effort: stale FAR windows are only a correctness risk if they remain selectable below.
                }
            }

            await WaitForWindowsToCloseAsync(dte, existingWindows, deadline);
            return existingWindows.Count;
        }

        private static async Task<FindReferencesWindowInfo> WaitForWindowAsync(
            DTE2 dte,
            List<FindReferencesWindowInfo> windowsBefore,
            DateTime deadline)
        {
            FindReferencesWindowInfo fallback = null;

            while (DateTime.UtcNow < deadline)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var candidates = GetWindows(dte);
                var selected = SelectWindow(dte, candidates, windowsBefore, allowFallback: false);
                if (selected != null)
                    return selected;

                fallback = candidates.LastOrDefault(w => GetSourceCount(w.Manager) > 0 || HasAnyEntries(w.TableControl));
                await DelayUntilDeadlineAsync(deadline, PollDelayMs);
            }

            var finalCandidates = GetWindows(dte);
            return fallback ?? SelectWindow(dte, finalCandidates, windowsBefore, allowFallback: true);
        }

        private static async Task WaitForWindowsToCloseAsync(
            DTE2 dte,
            List<FindReferencesWindowInfo> closedWindows,
            DateTime deadline)
        {
            while (DateTime.UtcNow < deadline)
            {
                var candidates = GetWindows(dte);
                var anyStillOpen = candidates.Any(candidate =>
                    closedWindows.Any(closed => SameWindowObject(closed, candidate)));

                if (!anyStillOpen)
                    return;

                await DelayUntilDeadlineAsync(deadline, PollDelayMs);
            }
        }

        private static FindReferencesWindowInfo SelectWindow(
            DTE2 dte,
            List<FindReferencesWindowInfo> candidates,
            List<FindReferencesWindowInfo> windowsBefore,
            bool allowFallback)
        {
            if (candidates.Count == 0)
                return null;

            var selected = candidates.LastOrDefault(candidate => !windowsBefore.Any(before => SameWindowObject(before, candidate)));
            if (selected != null)
                return selected;

            selected = candidates.LastOrDefault(candidate => !windowsBefore.Any(before => SameWindowCaption(before, candidate)));
            if (selected != null)
                return selected;

            var activeWindow = GetActiveWindow(dte);
            selected = candidates.LastOrDefault(candidate => SameDteWindow(candidate.Window, activeWindow));
            if (selected != null && (GetSourceCount(selected.Manager) > 0 || HasAnyEntries(selected.TableControl)))
                return selected;

            selected = candidates.LastOrDefault(candidate => GetSourceCount(candidate.Manager) > 0 || HasAnyEntries(candidate.TableControl));
            if (selected != null)
                return selected;

            return allowFallback ? candidates.Last() : null;
        }

        private static FindReferencesWindowInfo TryGetWindow(Window window)
        {
            try
            {
                var windowObject = window.Object;
                if (windowObject == null)
                    return null;

                var objectKind = SafeGetString(() => window.ObjectKind);
                var objectTypeName = windowObject.GetType().FullName ?? string.Empty;
                var looksLikeFindReferences =
                    string.Equals(objectKind, FindAllReferencesWindowKind, StringComparison.OrdinalIgnoreCase) ||
                    objectTypeName.IndexOf("FindAllReferencesWindow", StringComparison.OrdinalIgnoreCase) >= 0;

                if (!looksLikeFindReferences)
                    return null;

                var tableControl = GetPublicProperty(windowObject, "TableControl") as IWpfTableControl;
                if (tableControl == null)
                    return null;

                var manager = GetPublicProperty(windowObject, "Manager") as ITableManager ?? tableControl.Manager;
                if (manager == null)
                    return null;

                return new FindReferencesWindowInfo
                {
                    Window = window,
                    WindowObject = windowObject,
                    Caption = SafeGetString(() => window.Caption),
                    TableControl = tableControl,
                    Manager = manager
                };
            }
            catch
            {
                return null;
            }
        }

        private static async Task<bool> WaitForSourcesAsync(ITableManager manager, DateTime deadline)
        {
            if (GetSourceCount(manager) > 0)
                return true;

            var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler handler = null;

            handler = (sender, args) =>
            {
                if (GetSourceCount(manager) > 0)
                    changed.TrySetResult(true);
            };

            manager.SourcesChanged += handler;

            try
            {
                if (GetSourceCount(manager) > 0)
                    return true;

                return await WaitForSignalOrConditionAsync(
                    changed.Task,
                    deadline,
                    () => GetSourceCount(manager) > 0);
            }
            finally
            {
                manager.SourcesChanged -= handler;
            }
        }

        private static async Task<bool> WaitForStableAsync(IWpfTableControl tableControl, DateTime deadline)
        {
            var tableControl2 = tableControl as IWpfTableControl2;
            if (tableControl2 == null)
                return await WaitForEntriesQuietAsync(tableControl, deadline);

            if (tableControl2.IsDataStable)
                return true;

            var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler handler = null;

            handler = (sender, args) =>
            {
                if (tableControl2.IsDataStable)
                    changed.TrySetResult(true);
            };

            tableControl2.DataStabilityChanged += handler;

            try
            {
                if (tableControl2.IsDataStable)
                    return true;

                return await WaitForSignalOrConditionAsync(
                    changed.Task,
                    deadline,
                    () => tableControl2.IsDataStable);
            }
            finally
            {
                tableControl2.DataStabilityChanged -= handler;
            }
        }

        private static async Task<bool> WaitForEntriesQuietAsync(IWpfTableControl tableControl, DateTime deadline)
        {
            var lastChange = DateTime.UtcNow;
            EventHandler<EntriesChangedEventArgs> handler = (sender, args) => lastChange = DateTime.UtcNow;

            tableControl.EntriesChanged += handler;

            try
            {
                while (DateTime.UtcNow < deadline)
                {
                    if (DateTime.UtcNow - lastChange >= TimeSpan.FromMilliseconds(500))
                        return true;

                    await DelayUntilDeadlineAsync(deadline, PollDelayMs);
                }

                return false;
            }
            finally
            {
                tableControl.EntriesChanged -= handler;
            }
        }

        private static async Task ForceUpdateAsync(IWpfTableControl tableControl, DateTime deadline)
        {
            var updateTask = tableControl.ForceUpdateAsync();
            await WaitForSignalOrConditionAsync(updateTask, deadline, () => updateTask.IsCompleted);
        }

        private static async Task<bool> WaitForSignalOrConditionAsync(Task signal, DateTime deadline, Func<bool> condition)
        {
            while (DateTime.UtcNow < deadline)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (condition())
                    return true;

                if (signal.IsCompleted && condition())
                    return true;

                await DelayUntilDeadlineAsync(deadline, PollDelayMs);
            }

            return condition();
        }

        private static async Task DelayUntilDeadlineAsync(DateTime deadline, int delayMs)
        {
            var remainingMs = (int)Math.Max(0, (deadline - DateTime.UtcNow).TotalMilliseconds);
            if (remainingMs <= 0)
                return;

            await Task.Delay(Math.Min(delayMs, remainingMs));
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        }

        private static List<Dictionary<string, object>> ExtractRows(IWpfTableControl tableControl)
        {
            var rows = new List<Dictionary<string, object>>();
            var sourceLineCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in tableControl.Entries.ToList())
            {
                ITableEntriesSnapshot pinnedSnapshot = null;

                try
                {
                    pinnedSnapshot = entry.PinSnapshot();
                    if (pinnedSnapshot == null)
                        continue;

                    ITableEntriesSnapshot snapshot;
                    int index;

                    if (!entry.TryGetSnapshot(out snapshot, out index))
                    {
                        snapshot = pinnedSnapshot;
                        index = GetPublicIntProperty(entry, "Index") ?? -1;
                    }

                    if (snapshot == null || index < 0 || index >= snapshot.Count)
                        continue;

                    var row = CreateRow(snapshot, index, sourceLineCache);
                    if (IsReferenceRow(row))
                        rows.Add(row);
                }
                catch
                {
                    // Some FAR rows are grouping/summary rows backed by implementation-only objects.
                    // Ignore rows that cannot be pinned or read as table data.
                }
                finally
                {
                    if (pinnedSnapshot != null)
                        entry.UnpinSnapshot();
                }
            }

            return DeduplicateRows(rows);
        }

        private static Dictionary<string, object> CreateRow(
            ITableEntriesSnapshot snapshot,
            int index,
            Dictionary<string, string> sourceLineCache)
        {
            var cppData = GetSnapshotDataItem(snapshot, index);
            var file = GetTableString(snapshot, index, StandardTableKeyNames.Path)
                ?? GetReflectedString(cppData, "FileName")
                ?? GetTableString(snapshot, index, StandardTableKeyNames.DocumentName)
                ?? GetTableString(snapshot, index, StandardTableKeyNames.DisplayPath);

            var displayPath = GetTableString(snapshot, index, StandardTableKeyNames.DisplayPath);
            var rawLine = GetReflectedInt(cppData, "Line") ?? GetTableInt(snapshot, index, StandardTableKeyNames.Line);
            var rawColumn = GetReflectedInt(cppData, "Column") ?? GetTableInt(snapshot, index, StandardTableKeyNames.Column);
            var line = NormalizeZeroBasedPosition(rawLine);
            var column = NormalizeZeroBasedPosition(rawColumn);
            var text = GetSourceLineText(file, line, sourceLineCache)
                ?? GetTableString(snapshot, index, StandardTableKeyNames.FullText)
                ?? GetTableString(snapshot, index, StandardTableKeyNames.LineText)
                ?? GetTableString(snapshot, index, StandardTableKeyNames.Text)
                ?? GetDisplayTokensText(cppData);
            var projectName = GetReflectedString(cppData, "ProjectName")
                ?? GetTableString(snapshot, index, StandardTableKeyNames.ProjectName)
                ?? GetTableString(snapshot, index, StandardTableKeyNames.ProjectNames);
            var definition = GetReflectedString(cppData, "Definition")
                ?? GetTableString(snapshot, index, StandardTableKeyNames.Definition);
            var symbolKind = GetTableString(snapshot, index, StandardTableKeyNames.SymbolKind);
            var origin = GetTableString(snapshot, index, StandardTableKeyNames.ItemOrigin);
            var readWriteStatus = GetReflectedString(cppData, "ReadWriteStatusText");
            var itemStatus = GetReflectedString(cppData, "ItemStatus");
            var resolutionStatus = GetReflectedString(cppData, "ResolutionStatus");
            var resolutionStatusTooltip = GetReflectedString(cppData, "ResolutionStatusTooltip");

            var row = new Dictionary<string, object>();
            AddString(row, "file", file);
            AddString(row, "displayPath", displayPath);
            AddInt(row, "line", line);
            AddInt(row, "column", column);
            AddString(row, "text", text);
            AddString(row, "projectName", projectName);
            AddString(row, "definition", definition);
            AddString(row, "symbolKind", symbolKind);
            AddString(row, "origin", origin);
            AddString(row, "readWriteStatus", readWriteStatus);
            AddString(row, "status", itemStatus);
            AddString(row, "resolutionStatus", resolutionStatus);
            AddString(row, "resolutionStatusTooltip", resolutionStatusTooltip);

            return row;
        }

        private static string GetSourceLineText(
            string file,
            int? line,
            Dictionary<string, string> sourceLineCache)
        {
            if (string.IsNullOrWhiteSpace(file) || !line.HasValue || line.Value <= 0)
                return null;
            if (!File.Exists(file))
                return null;

            var cacheKey = file + "\0" + line.Value;
            if (sourceLineCache.TryGetValue(cacheKey, out var cachedLine))
                return cachedLine;

            try
            {
                var sourceLine = File.ReadLines(file).Skip(line.Value - 1).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(sourceLine))
                    sourceLineCache[cacheKey] = sourceLine;

                return string.IsNullOrWhiteSpace(sourceLine) ? null : sourceLine;
            }
            catch
            {
                return null;
            }
        }

        private static List<Dictionary<string, object>> DeduplicateRows(List<Dictionary<string, object>> rows)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduplicated = new List<Dictionary<string, object>>();

            foreach (var row in rows)
            {
                var key = string.Join("|",
                    GetDictionaryString(row, "file"),
                    GetDictionaryString(row, "line"),
                    GetDictionaryString(row, "column"),
                    GetDictionaryString(row, "text"));

                if (seen.Add(key))
                    deduplicated.Add(row);
            }

            return deduplicated;
        }

        private static bool IsReferenceRow(Dictionary<string, object> row)
        {
            return row.ContainsKey("file") || row.ContainsKey("displayPath");
        }

        private static List<object> GetSourceSummaries(ITableManager manager)
        {
            return manager.Sources
                .Select(source => new Dictionary<string, object>
                {
                    ["identifier"] = source.Identifier,
                    ["sourceTypeIdentifier"] = source.SourceTypeIdentifier
                })
                .Cast<object>()
                .ToList();
        }

        private static int GetSourceCount(ITableManager manager)
        {
            return manager?.Sources?.Count ?? 0;
        }

        private static bool HasAnyEntries(IWpfTableControl tableControl)
        {
            return tableControl?.Entries != null && tableControl.Entries.Any();
        }

        private static bool SameWindowObject(FindReferencesWindowInfo left, FindReferencesWindowInfo right)
        {
            return ReferenceEquals(left.WindowObject, right.WindowObject);
        }

        private static bool SameWindowCaption(FindReferencesWindowInfo left, FindReferencesWindowInfo right)
        {
            return string.Equals(left.Caption, right.Caption, StringComparison.Ordinal);
        }

        private static bool SameDteWindow(Window left, Window right)
        {
            if (left == null || right == null)
                return false;

            if (ReferenceEquals(left, right))
                return true;

            try
            {
                return left.HWnd == right.HWnd &&
                    string.Equals(left.Caption, right.Caption, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static Window GetActiveWindow(DTE2 dte)
        {
            try
            {
                return dte.ActiveWindow;
            }
            catch
            {
                return null;
            }
        }

        private static object GetSnapshotDataItem(ITableEntriesSnapshot snapshot, int index)
        {
            var data = GetPublicProperty(snapshot, "Data") as IEnumerable;
            if (data == null)
                return null;

            var currentIndex = 0;
            foreach (var item in data)
            {
                if (currentIndex == index)
                    return item;

                currentIndex++;
            }

            return null;
        }

        private static string GetDisplayTokensText(object dataItem)
        {
            var tokens = GetPublicProperty(dataItem, "DisplayTokens") as IEnumerable;
            if (tokens == null)
                return null;

            var parts = new List<string>();

            foreach (var token in tokens)
            {
                var text = GetReflectedString(token, "Text") ?? token?.ToString();
                if (!string.IsNullOrEmpty(text))
                    parts.Add(text);
            }

            return parts.Count == 0 ? null : string.Concat(parts);
        }

        private static string GetTableString(ITableEntriesSnapshot snapshot, int index, string keyName)
        {
            return ToSimpleString(GetTableValue(snapshot, index, keyName));
        }

        private static int? GetTableInt(ITableEntriesSnapshot snapshot, int index, string keyName)
        {
            return ToNullableInt(GetTableValue(snapshot, index, keyName));
        }

        private static object GetTableValue(ITableEntriesSnapshot snapshot, int index, string keyName)
        {
            object value;
            if (snapshot.TryGetValue(index, keyName, out value))
                return value;

            return null;
        }

        private static object GetPublicProperty(object instance, string propertyName)
        {
            if (instance == null)
                return null;

            try
            {
                var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (property == null || property.GetIndexParameters().Length > 0)
                    return null;

                return property.GetValue(instance);
            }
            catch
            {
                return null;
            }
        }

        private static string GetReflectedString(object instance, string propertyName)
        {
            return ToSimpleString(GetPublicProperty(instance, propertyName));
        }

        private static int? GetReflectedInt(object instance, string propertyName)
        {
            return ToNullableInt(GetPublicProperty(instance, propertyName));
        }

        private static int? GetPublicIntProperty(object instance, string propertyName)
        {
            return ToNullableInt(GetPublicProperty(instance, propertyName));
        }

        private static int? NormalizeZeroBasedPosition(int? value)
        {
            return value.HasValue && value.Value >= 0 ? value.Value + 1 : (int?)null;
        }

        private static int? ToNullableInt(object value)
        {
            if (value == null)
                return null;
            if (value is int intValue)
                return intValue;
            if (value is long longValue)
                return (int)longValue;
            if (value is short shortValue)
                return shortValue;
            if (value is byte byteValue)
                return byteValue;
            if (int.TryParse(value.ToString(), out var parsed))
                return parsed;
            return null;
        }

        private static string ToSimpleString(object value)
        {
            if (value == null)
                return null;
            if (value is string stringValue)
                return string.IsNullOrWhiteSpace(stringValue) ? null : stringValue;
            if (value is IEnumerable enumerable)
            {
                var parts = new List<string>();
                foreach (var item in enumerable)
                {
                    var itemText = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(itemText))
                        parts.Add(itemText);
                }

                return parts.Count == 0 ? null : string.Join(", ", parts);
            }

            var text = value.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static void AddString(Dictionary<string, object> dictionary, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                dictionary[key] = value;
        }

        private static void AddInt(Dictionary<string, object> dictionary, string key, int? value)
        {
            if (value.HasValue)
                dictionary[key] = value.Value;
        }

        private static string GetDictionaryString(Dictionary<string, object> dictionary, string key)
        {
            return dictionary.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;
        }

        private static string SafeGetString(Func<string> getter)
        {
            try
            {
                return getter() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        internal sealed class FindReferencesReadResult
        {
            public bool WindowFound { get; set; }
            public string WindowTitle { get; set; }
            public bool Completed { get; set; }
            public int SourceCount { get; set; }
            public List<object> Sources { get; set; } = new List<object>();
            public List<Dictionary<string, object>> References { get; set; } = new List<Dictionary<string, object>>();
        }

        internal sealed class FindReferencesWindowInfo
        {
            public Window Window { get; set; }
            public object WindowObject { get; set; }
            public string Caption { get; set; }
            public IWpfTableControl TableControl { get; set; }
            public ITableManager Manager { get; set; }
        }
    }
}
