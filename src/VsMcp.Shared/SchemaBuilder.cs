using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace VsMcp.Shared
{
    /// <summary>
    /// Helper to build JSON Schema objects for MCP tool input definitions.
    /// </summary>
    public class SchemaBuilder
    {
        private readonly JObject _properties = new JObject();
        private readonly List<string> _required = new List<string>();

        public static SchemaBuilder Create() => new SchemaBuilder();

        public static JObject Empty()
        {
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject()
            };
        }

        public SchemaBuilder AddString(string name, string description, bool required = false)
        {
            _properties[name] = new JObject
            {
                ["type"] = "string",
                ["description"] = description
            };
            if (required)
                _required.Add(name);
            return this;
        }

        public SchemaBuilder AddInteger(string name, string description, bool required = false)
        {
            _properties[name] = new JObject
            {
                ["type"] = "integer",
                ["description"] = description
            };
            if (required)
                _required.Add(name);
            return this;
        }

        public SchemaBuilder AddBoolean(string name, string description, bool required = false)
        {
            _properties[name] = new JObject
            {
                ["type"] = "boolean",
                ["description"] = description
            };
            if (required)
                _required.Add(name);
            return this;
        }

        public SchemaBuilder AddEnum(string name, string description, string[] values, bool required = false, string defaultValue = null)
        {
            var prop = new JObject
            {
                ["type"] = "string",
                ["description"] = description,
                ["enum"] = new JArray(values)
            };
            if (defaultValue != null)
                prop["default"] = defaultValue;

            _properties[name] = prop;
            if (required)
                _required.Add(name);
            return this;
        }

        public SchemaBuilder AddEnumArray(string name, string description, string[] values, bool required = false, string[] defaultValue = null)
        {
            var prop = new JObject
            {
                ["type"] = "array",
                ["description"] = description,
                ["items"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray(values)
                },
                ["uniqueItems"] = true,
                ["minItems"] = 1
            };
            if (defaultValue != null)
                prop["default"] = new JArray(defaultValue);

            _properties[name] = prop;
            if (required)
                _required.Add(name);
            return this;
        }

        public JObject Build()
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = _properties
            };
            if (_required.Count > 0)
            {
                schema["required"] = new JArray(_required.ToArray());
            }
            return schema;
        }
    }
}
