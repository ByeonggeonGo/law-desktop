using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LawDesktop.Models
{
    public class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public int Id { get; set; } = 1;

        [JsonPropertyName("method")]
        public string Method { get; set; } = "tools/call";

        [JsonPropertyName("params")]
        public JsonRpcParams Params { get; set; } = new();
    }

    public class JsonRpcParams
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public Dictionary<string, object> Arguments { get; set; } = new();
    }

    public class JsonRpcResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("result")]
        public McpResult? Result { get; set; }

        [JsonPropertyName("error")]
        public McpError? Error { get; set; }
    }

    public class McpError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class McpResult
    {
        [JsonPropertyName("content")]
        public List<McpContentBlock>? Content { get; set; }

        [JsonPropertyName("isError")]
        public bool IsError { get; set; }
    }

    public class McpContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class Citation
    {
        public int Num { get; set; }
        public string Type { get; set; } = string.Empty; // "법령" or "판례"
        public string Title { get; set; } = string.Empty;
        public List<string> HighlightTerms { get; set; } = new();
        public string Content { get; set; } = string.Empty;
    }

    public class CitationVerifyResult
    {
        public bool Applied { get; set; }
        public bool Called { get; set; }
        public string AnnotatedAnswer { get; set; } = string.Empty;
        public bool Hallucination { get; set; }
        public bool Partial { get; set; }
        public string Summary { get; set; } = string.Empty;
        public int LawCount { get; set; }
        public int PrecedentCount { get; set; }
        public string? Error { get; set; }
    }
}
