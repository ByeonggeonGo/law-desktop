using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LawDesktop.Models;

namespace LawDesktop.Services
{
    public class LawMcpService
    {
        private readonly HttpClient _httpClient;
        private string _mcpUrl = "https://korean-law-mcp.fly.dev/mcp";
        private string _ocKey = "honggildong"; // default demo key

        public LawMcpService(string? ocKey = null, string? mcpUrl = null)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(25.0)
            };
            if (!string.IsNullOrEmpty(ocKey)) _ocKey = ocKey;
            if (!string.IsNullOrEmpty(mcpUrl)) _mcpUrl = mcpUrl;
        }

        public void UpdateConfig(string ocKey, string mcpUrl)
        {
            if (!string.IsNullOrEmpty(ocKey)) _ocKey = ocKey;
            if (!string.IsNullOrEmpty(mcpUrl)) _mcpUrl = mcpUrl;
        }

        private string GetEndpoint()
        {
            return $"{_mcpUrl.TrimEnd('/')}?oc={_ocKey}";
        }

        /// <summary>
        /// Call law MCP JSON-RPC tool
        /// </summary>
        public async Task<McpCallResult> CallLawToolAsync(string toolName, Dictionary<string, object> arguments)
        {
            try
            {
                var requestPayload = new JsonRpcRequest
                {
                    Method = "tools/call",
                    Params = new JsonRpcParams
                    {
                        Name = toolName,
                        Arguments = arguments
                    }
                };

                var jsonStr = JsonSerializer.Serialize(requestPayload);
                var content = new StringContent(jsonStr, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var response = await _httpClient.PostAsync(GetEndpoint(), content);
                if (!response.IsSuccessStatusCode)
                {
                    return new McpCallResult
                    {
                        Ok = false,
                        Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                    };
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                
                // Parse SSE formatting if present
                JsonRpcResponse? jsonRpcResp = null;
                try
                {
                    jsonRpcResp = JsonSerializer.Deserialize<JsonRpcResponse>(responseBody);
                }
                catch (JsonException)
                {
                    jsonRpcResp = ParseSsePayload(responseBody);
                }

                if (jsonRpcResp == null)
                {
                    return new McpCallResult { Ok = false, Error = "Failed to parse JSON response from MCP server." };
                }

                if (jsonRpcResp.Error != null)
                {
                    return new McpCallResult { Ok = false, Error = $"JSON-RPC Error: {jsonRpcResp.Error.Message}" };
                }

                if (jsonRpcResp.Result == null)
                {
                    return new McpCallResult { Ok = false, Error = "MCP Result payload is empty." };
                }

                var textBuilder = new StringBuilder();
                if (jsonRpcResp.Result.Content != null)
                {
                    foreach (var block in jsonRpcResp.Result.Content)
                    {
                        if (block.Type == "text")
                        {
                            textBuilder.Append(block.Text);
                        }
                    }
                }

                var rawText = textBuilder.ToString();
                rawText = Regex.Replace(rawText, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);

                return new McpCallResult
                {
                    Ok = !jsonRpcResp.Result.IsError,
                    Text = rawText,
                    Error = jsonRpcResp.Result.IsError ? "MCP tool execution returned an error." : null
                };
            }
            catch (Exception ex)
            {
                return new McpCallResult { Ok = false, Error = $"Network exception occurred: {ex.Message}" };
            }
        }

        private JsonRpcResponse? ParseSsePayload(string body)
        {
            var lines = body.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var dataLines = new List<string>();
            foreach (var line in lines)
            {
                if (line.StartsWith("data:"))
                {
                    dataLines.Add(line.Substring(5).Trim());
                }
            }

            if (dataLines.Count == 0) return null;

            try
            {
                var joined = string.Join("\n", dataLines);
                return JsonSerializer.Deserialize<JsonRpcResponse>(joined);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extract citations and perform verification
        /// </summary>
        public async Task<CitationVerifyResult> RunLawCitationGuardAsync(string answer, int maxCitations = 8)
        {
            var citations = ExtractLawCitations(answer);
            var baseOut = new CitationVerifyResult
            {
                Applied = false,
                Called = false,
                AnnotatedAnswer = answer,
                Hallucination = false,
                Partial = false,
                Summary = "No legal citations found.",
                LawCount = citations.Laws.Count,
                PrecedentCount = citations.Precedents.Count
            };

            if (citations.TotalCount == 0)
            {
                return baseOut;
            }

            var args = new Dictionary<string, object>
            {
                { "text", answer },
                { "maxCitations", maxCitations }
            };

            var mcpRes = await CallLawToolAsync("verify_citations", args);
            if (!mcpRes.Ok)
            {
                baseOut.Applied = true;
                baseOut.Called = true;
                baseOut.Error = mcpRes.Error ?? "Failed to call verify_citations.";
                baseOut.Summary = "Citation verification failed (MCP Server Error).";
                return baseOut;
            }

            var rawVerifyText = mcpRes.Text ?? string.Empty;
            
            bool hasHallucination = rawVerifyText.Contains("[HALLUCINATION_DETECTED]");
            bool hasPartial = rawVerifyText.Contains("[PARTIAL_VERIFIED]") && !hasHallucination;
            bool hasVerified = rawVerifyText.Contains("[VERIFIED]") && !hasHallucination && !hasPartial;

            string summaryLine = string.Empty;
            var lines = rawVerifyText.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("실존") || line.Contains("✓") || line.Contains("✗"))
                {
                    summaryLine = line.Trim();
                    break;
                }
            }

            if (string.IsNullOrEmpty(summaryLine))
            {
                summaryLine = hasHallucination ? "Hallucination Detected" :
                              hasPartial ? "Partially Verified" :
                              hasVerified ? "All Verified" : "Unknown Result";
            }

            var annotatedAnswer = answer;
            if (hasHallucination)
            {
                annotatedAnswer = AnnotateInvalidCitations(answer, rawVerifyText);
            }

            return new CitationVerifyResult
            {
                Applied = true,
                Called = true,
                AnnotatedAnswer = annotatedAnswer,
                Hallucination = hasHallucination,
                Partial = hasPartial,
                Summary = summaryLine,
                LawCount = citations.Laws.Count,
                PrecedentCount = citations.Precedents.Count
            };
        }

        /// <summary>
        /// Extract [법령: ...] or [판례: ...] format pattern
        /// </summary>
        public ExtractedCitations ExtractLawCitations(string text)
        {
            var laws = new List<string>();
            var precedents = new List<string>();

            if (string.IsNullOrEmpty(text))
            {
                return new ExtractedCitations(laws, precedents);
            }

            var lawMatches = Regex.Matches(text, @"\[법령\s*:\s*([^\[\]\n]+?)\]");
            foreach (Match match in lawMatches)
            {
                laws.Add(match.Groups[1].Value.Trim());
            }

            var precMatches = Regex.Matches(text, @"\[판례\s*:\s*([^\[\]\n]+?)\]");
            foreach (Match match in precMatches)
            {
                precedents.Add(match.Groups[1].Value.Trim());
            }

            return new ExtractedCitations(laws, precedents);
        }

        /// <summary>
        /// Extract structured list of citations
        /// </summary>
        public List<Citation> ExtractStructuredCitations(string text)
        {
            var list = new List<Citation>();
            if (string.IsNullOrEmpty(text)) return list;

            var matches = new List<(int Index, string Type, string Title)>();
            
            var lawMatches = Regex.Matches(text, @"\[법령\s*:\s*([^\[\]\n]+?)\]");
            foreach (Match m in lawMatches)
            {
                matches.Add((m.Index, "법령", m.Groups[1].Value.Trim()));
            }

            var precMatches = Regex.Matches(text, @"\[판례\s*:\s*([^\[\]\n]+?)\]");
            foreach (Match m in precMatches)
            {
                matches.Add((m.Index, "판례", m.Groups[1].Value.Trim()));
            }

            matches.Sort((a, b) => a.Index.CompareTo(b.Index));

            var seen = new HashSet<(string, string)>();
            foreach (var item in matches)
            {
                var key = (item.Type, item.Title);
                if (string.IsNullOrEmpty(item.Title) || seen.Contains(key)) continue;
                seen.Add(key);

                var highlightTerms = GenerateHighlightTerms(item.Type, item.Title);
                list.Add(new Citation
                {
                    Num = list.Count + 1,
                    Type = item.Type,
                    Title = item.Title,
                    HighlightTerms = highlightTerms
                });
            }

            return list;
        }

        private List<string> GenerateHighlightTerms(string type, string title)
        {
            var terms = new List<string>();
            var cleaned = Regex.Replace(title, @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(cleaned)) return terms;

            if (type == "법령")
            {
                var articleMatch = Regex.Match(cleaned, @"제\s*\d+(?:조|항|호)(?:의\s*\d+)?");
                if (articleMatch.Success)
                {
                    string lawName = cleaned.Substring(0, articleMatch.Index).Trim();
                    if (!string.IsNullOrEmpty(lawName)) terms.Add(lawName);
                    terms.Add(Regex.Replace(articleMatch.Value, @"\s+", ""));
                }
                else
                {
                    terms.Add(cleaned);
                }
            }
            else if (type == "판례")
            {
                var caseNoMatch = Regex.Match(cleaned, @"\d{4}[가-힣]{1,3}\d+");
                if (caseNoMatch.Success)
                {
                    string courtName = cleaned.Substring(0, caseNoMatch.Index).Trim();
                    if (!string.IsNullOrEmpty(courtName)) terms.Add(courtName);
                    terms.Add(caseNoMatch.Value);
                }
                else
                {
                    terms.Add(cleaned);
                }
            }

            terms.Add(cleaned);
            
            var outList = new List<string>();
            foreach (var term in terms)
            {
                if (!string.IsNullOrEmpty(term) && !outList.Contains(term))
                {
                    outList.Add(term);
                }
            }

            return outList;
        }

        private string AnnotateInvalidCitations(string answer, string rawVerifyText)
        {
            if (string.IsNullOrEmpty(answer) || string.IsNullOrEmpty(rawVerifyText)) return answer;

            var badCitations = new List<string>();
            var lines = rawVerifyText.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("✗")) continue;

                var body = trimmed.Substring(1).Trim();
                if (body.Contains("—"))
                {
                    body = body.Split(new[] { '—' }, 2)[0].Trim();
                }
                else if (body.Contains(" - "))
                {
                    body = body.Split(new[] { " - " }, StringSplitOptions.None)[0].Trim();
                }

                if (body.Length >= 3)
                {
                    badCitations.Add(body);
                }
            }

            if (badCitations.Count == 0) return answer;

            var output = answer;
            foreach (var bad in badCitations)
            {
                foreach (var prefix in new[] { "법령", "판례" })
                {
                    var escapedBad = Regex.Escape(bad);
                    var pattern = $@"\[{prefix}\s*:\s*([^\[\]\n]*?{escapedBad}[^\[\]\n]*?)\]";
                    output = Regex.Replace(output, pattern, $"[{prefix}: $1 → 검증실패]");
                }
            }

            return output;
        }
    }

    public class McpCallResult
    {
        public bool Ok { get; set; }
        public string? Text { get; set; }
        public string? Error { get; set; }
    }

    public class ExtractedCitations
    {
        public List<string> Laws { get; }
        public List<string> Precedents { get; }
        public int TotalCount => Laws.Count + Precedents.Count;

        public ExtractedCitations(List<string> laws, List<string> precedents)
        {
            Laws = laws;
            Precedents = precedents;
        }
    }
}
