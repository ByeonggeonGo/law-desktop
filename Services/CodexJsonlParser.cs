using System;
using System.Collections.Generic;
using System.Text.Json;

namespace LawDesktop.Services
{
    public sealed class CodexRunEvents
    {
        public string? ThreadId { get; set; }
        public string? FinalText { get; set; }
        public List<CodexToolCall> ToolCalls { get; } = new();
    }

    public sealed class CodexToolCall
    {
        public string Server { get; set; } = string.Empty;
        public string Tool { get; set; } = string.Empty;
        public string ArgumentsJson { get; set; } = string.Empty;
        public string ResultText { get; set; } = string.Empty;
    }

    public static class CodexJsonlParser
    {
        public static CodexRunEvents Parse(string jsonl)
        {
            var events = new CodexRunEvents();
            if (string.IsNullOrWhiteSpace(jsonl)) return events;

            var lines = jsonl.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                ParseLine(line.Trim(), events);
            }

            return events;
        }

        private static void ParseLine(string line, CodexRunEvents events)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var type = GetString(root, "type") ?? GetString(root, "event");

                if (type == "thread.started")
                {
                    events.ThreadId = GetString(root, "thread_id") ?? GetString(root, "threadId");
                    return;
                }

                if (!root.TryGetProperty("item", out var item)) return;

                var itemType = GetString(item, "type");
                if (itemType == "agent_message")
                {
                    events.FinalText = GetString(item, "text") ?? GetString(item, "message") ?? events.FinalText;
                    return;
                }

                if (itemType == "mcp_tool_call")
                {
                    events.ToolCalls.Add(new CodexToolCall
                    {
                        Server = GetString(item, "server") ?? string.Empty,
                        Tool = GetString(item, "tool") ?? GetString(item, "name") ?? string.Empty,
                        ArgumentsJson = TryRaw(item, "arguments"),
                        ResultText = GetString(item, "result") ?? TryRaw(item, "result")
                    });
                }
            }
            catch (JsonException)
            {
                // Ignore partial or non-JSON progress lines.
            }
        }

        private static string? GetString(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value)) return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        }

        private static string TryRaw(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) ? value.GetRawText() : string.Empty;
        }
    }
}
