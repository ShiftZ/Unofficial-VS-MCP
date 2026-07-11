using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace VsMcp.Debugger
{
    internal static class DebuggerEventLog
    {
        private static readonly object SyncRoot = new object();

        internal static string FilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VsMcp",
            "Debugger",
            "events.jsonl");

        internal static void Write(string eventName, params string[] properties)
        {
            try
            {
                var line = BuildEvent(eventName, properties);
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // A debugger integration must not destabilize the debug session.
            }
        }

        private static string BuildEvent(string eventName, string[] properties)
        {
            var process = Process.GetCurrentProcess();
            var builder = new StringBuilder();
            builder.Append('{');
            AppendProperty(builder, "timestampUtc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            AppendProperty(builder, "event", eventName);
            AppendProperty(builder, "hostProcess", process.ProcessName);
            AppendProperty(builder, "hostProcessId", process.Id.ToString(CultureInfo.InvariantCulture));

            for (var index = 0; index + 1 < properties.Length; index += 2)
                AppendProperty(builder, properties[index], properties[index + 1]);

            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendProperty(StringBuilder builder, string name, string value)
        {
            if (builder.Length > 1) builder.Append(',');

            AppendQuoted(builder, name);
            builder.Append(':');
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            AppendQuoted(builder, value);
        }

        private static void AppendQuoted(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\b':
                        builder.Append("\\b");
                        break;
                    case '\f':
                        builder.Append("\\f");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }
            builder.Append('"');
        }
    }
}
