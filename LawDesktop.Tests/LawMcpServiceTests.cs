using System.Net;
using LawDesktop.Services;

namespace LawDesktop.Tests;

public class LawMcpServiceTests
{
    [Fact]
    public async Task CallLawToolSendsMcpAcceptHeaders()
    {
        using var httpClient = new HttpClient(new RecordingHandler());
        var service = new LawMcpService(httpClient, "0428", "https://example.test/mcp");

        var result = await service.CallLawToolAsync("search_law", new Dictionary<string, object>
        {
            { "query", "환경영향평가법" }
        });

        Assert.True(result.Ok);
        Assert.Contains(RecordingHandler.LastAcceptHeaders, h => h == "application/json");
        Assert.Contains(RecordingHandler.LastAcceptHeaders, h => h == "text/event-stream");
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public static List<string> LastAcceptHeaders { get; private set; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastAcceptHeaders = request.Headers.Accept.Select(h => h.MediaType ?? string.Empty).ToList();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"jsonrpc":"2.0","id":1,"result":{"content":[{"type":"text","text":"ok"}],"isError":false}}
                    """)
            };

            return Task.FromResult(response);
        }
    }
}
