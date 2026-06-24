using LawDesktop.Services;

namespace LawDesktop.Tests;

public class CodexJsonlParserTests
{
    [Fact]
    public void ParseCapturesThreadFinalTextAndMcpToolCalls()
    {
        var jsonl = """
            {"type":"thread.started","thread_id":"abc-123"}
            {"type":"item.completed","item":{"type":"mcp_tool_call","server":"law","tool":"search_law","arguments":{"query":"민법"},"result":"검색 결과"}}
            {"type":"item.completed","item":{"type":"agent_message","text":"최종 답변"}}
            {"type":"turn.completed","usage":{"input_tokens":10,"output_tokens":5}}
            """;

        var parsed = CodexJsonlParser.Parse(jsonl);

        Assert.Equal("abc-123", parsed.ThreadId);
        Assert.Equal("최종 답변", parsed.FinalText);
        var call = Assert.Single(parsed.ToolCalls);
        Assert.Equal("law", call.Server);
        Assert.Equal("search_law", call.Tool);
        Assert.Contains("민법", call.ArgumentsJson);
        Assert.Equal("검색 결과", call.ResultText);
    }
}
