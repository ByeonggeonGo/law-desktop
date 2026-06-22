using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LawDesktop.Models;

namespace LawDesktop.Services
{
    public class PipelineService
    {
        private readonly LawMcpService _mcpService;
        private readonly AgyCliService _agyService;

        // Greeting tokens for simple classification
        private static readonly HashSet<string> GreetingTokens = new()
        {
            "hello", "hi", "hey", "greetings", "good morning", "good afternoon", "안녕", "안녕하세요", "반가워", "ㅎㅇ"
        };

        public PipelineService(LawMcpService mcpService, AgyCliService agyService)
        {
            _mcpService = mcpService;
            _agyService = agyService;
        }

        /// <summary>
        /// Executes the 3-stage RAG Pipeline (Intro -> Middle -> Outro) in English
        /// </summary>
        /// <param name="question">User query text</param>
        /// <param name="progressCallback">Callback for updating progress (statusMessage, done)</param>
        public async Task<ChatMessage> RunPipelineAsync(string question, Action<string, bool> progressCallback)
        {
            var message = new ChatMessage
            {
                Sender = "AI",
                Timestamp = DateTime.Now
            };

            // Stage 1: Intent Analysis (Intro)
            progressCallback("Analyzing query intent...", false);
            var isGreeting = CheckIfGreeting(question);

            if (isGreeting)
            {
                progressCallback("Completed", true);
                message.Content = "Hello! How can I help you today? Please feel free to ask any legal issues or disputes, and I will search relevant laws and precedents to guide you.";
                message.GuardSummary = "Greeting detected (Skip Search)";
                return message;
            }

            // Stage 2: Search Keywords Extraction (Using local agy CLI)
            progressCallback("Extracting search keywords using AI...", false);
            var keywords = await ExtractSearchKeywordsAsync(question);
            progressCallback($"Keywords extracted: {string.Join(", ", keywords)}", false);

            // Stage 3: Collect Laws and Precedents (MCP Integration)
            progressCallback("Collecting relevant laws and precedents from MCP...", false);
            var searchContext = await CollectSearchContextAsync(keywords, progressCallback);

            // Stage 4: Generate Draft Answer (Using local agy CLI)
            progressCallback("Generating grounded answer based on facts...", false);
            var draftAnswer = await GenerateDraftAnswerAsync(question, searchContext);

            if (string.IsNullOrEmpty(draftAnswer))
            {
                progressCallback("Generation failed", true);
                message.Content = "I'm sorry, I could not generate an answer. The local agy cli response was empty or returned an error.";
                return message;
            }

            // Stage 5: Citation Verification (Outro)
            progressCallback("Verifying legal citation guard (verify_citations)...", false);
            var verifyResult = await _mcpService.RunLawCitationGuardAsync(draftAnswer);

            progressCallback("Completed", true);

            message.Content = verifyResult.AnnotatedAnswer;
            
            // Translate status summary to English
            message.GuardSummary = TranslateVerifySummary(verifyResult.Summary);
            message.IsHallucinated = verifyResult.Hallucination;
            message.IsPartialVerified = verifyResult.Partial;
            
            // Extract structured citations for UI split pane
            message.Citations = _mcpService.ExtractStructuredCitations(verifyResult.AnnotatedAnswer);

            // Load full-text content in background
            await PopulateCitationContentsAsync(message.Citations);

            return message;
        }

        private bool CheckIfGreeting(string question)
        {
            var cleaned = Regex.Replace(question.ToLower().Replace(" ", ""), @"[^\w\s]", "");
            return GreetingTokens.Any(token => cleaned.Contains(token));
        }

        /// <summary>
        /// Ask local agy cli to extract Korean search keywords from user query
        /// </summary>
        private async Task<List<string>> ExtractSearchKeywordsAsync(string question)
        {
            var prompt = $"User Query: \"{question}\"\n\n" +
                         "Based on this query, extract 1 to 3 core Korean legal terms (e.g. 임차권등기명령, 주택임대차보호법, 부당해고) to query in the Korean database. Respond ONLY with the terms separated by commas. Do not include any explanations, markdown block, or other texts.";

            var res = await _agyService.ExecutePromptAsync(prompt);
            if (!res.Ok || string.IsNullOrEmpty(res.Text))
            {
                // Fallback: simple token splitting
                var fallback = question.Split(' ')
                    .Where(w => w.Length > 2)
                    .Take(2)
                    .Select(w => Regex.Replace(w, @"[^\w]", ""))
                    .ToList();
                return fallback.Count > 0 ? fallback : new List<string> { question };
            }

            var text = res.Text.Trim();
            text = text.Replace("`", "").Replace("\"", "").Replace("\r", "").Replace("\n", "");
            
            var keywords = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();

            return keywords.Count > 0 ? keywords : new List<string> { question };
        }

        /// <summary>
        /// Retrieve matching laws and decisions for the extracted keywords
        /// </summary>
        private async Task<string> CollectSearchContextAsync(List<string> keywords, Action<string, bool> progressCallback)
        {
            var contextBuilder = new System.Text.StringBuilder();

            foreach (var keyword in keywords)
            {
                progressCallback($"Searching laws for '{keyword}'...", false);
                var lawRes = await _mcpService.CallLawToolAsync("search_law", new Dictionary<string, object> { { "query", keyword } });
                if (lawRes.Ok && !string.IsNullOrEmpty(lawRes.Text))
                {
                    contextBuilder.AppendLine($"=== Law Search Result (Keyword: {keyword}) ===");
                    contextBuilder.AppendLine(lawRes.Text);
                    contextBuilder.AppendLine();
                }

                progressCallback($"Searching precedents for '{keyword}'...", false);
                var decRes = await _mcpService.CallLawToolAsync("search_decisions", new Dictionary<string, object> { { "query", keyword } });
                if (decRes.Ok && !string.IsNullOrEmpty(decRes.Text))
                {
                    contextBuilder.AppendLine($"=== Precedent Search Result (Keyword: {keyword}) ===");
                    contextBuilder.AppendLine(decRes.Text);
                    contextBuilder.AppendLine();
                }
            }

            return contextBuilder.ToString();
        }

        /// <summary>
        /// Call agy cli to synthesize the final answer in English
        /// </summary>
        private async Task<string> GenerateDraftAnswerAsync(string question, string context)
        {
            var systemPrompt = "You are a professional legal advisor and AI assistant. Based ONLY on the facts and laws provided in the [Laws and Precedents Context] below, answer the user's question logically and guide them.\n\n" +
                               "=== KEY RULES ===\n" +
                               "1. STRICT CITE MARKUPS: You MUST cite precedents or laws using ONLY the following markup formats: [법령: LawName ArticleNo] or [판례: CaseNumber] in Korean. For example, [법령: 민법 제750조], [판례: 대법원 2020도1234]. Do not translate the text inside the brackets; they must be written in Korean so that the system parser and verification tool can query them.\n" +
                               "2. NO HALLUCINATIONS: Do not invent fake case numbers or law articles. If the context does not contain relevant information, state clearly: \"Based on the retrieved database, no matching precedents or laws were found to support this case.\"\n" +
                               "3. LANGUAGE: Write the entire answer in English. Translate and explain the Korean legal principles into English naturally.\n" +
                               "4. FORMAT: Use a polite tone and organize your answer in a clean markdown list format.";

            var fullPrompt = $"{systemPrompt}\n" +
                             $"[Laws and Precedents Context]\n" +
                             $"{context}\n\n" +
                             $"[User Question]\n" +
                             $"{question}\n\n" +
                             "Answer:";

            var result = await _agyService.ExecutePromptAsync(fullPrompt);
            return result.Ok ? (result.Text ?? string.Empty) : string.Empty;
        }

        private string TranslateVerifySummary(string summary)
        {
            if (string.IsNullOrEmpty(summary)) return "No citations verified.";
            
            // translate simple outcomes
            if (summary.Contains("환각 검출됨") || summary.Contains("환각")) return "Warning: Hallucination detected in citations.";
            if (summary.Contains("부분 검증됨") || summary.Contains("부분")) return "Warning: Partially verified citations.";
            if (summary.Contains("검증 완료")) return "Citations verified successfully.";
            if (summary.Contains("실존") || summary.Contains("✗") || summary.Contains("✓"))
            {
                // Translate: "총 3건 | ✓ 2 실존 | ✗ 1 오류" -> "Total: 3 | ✓ 2 Real | ✗ 1 Invalid"
                var result = summary
                    .Replace("총", "Total:")
                    .Replace("건", "")
                    .Replace("실존", "Real")
                    .Replace("오류", "Invalid")
                    .Replace("확인필요", "Need Check");
                return result;
            }

            return summary;
        }

        /// <summary>
        /// Fetch detailed full texts for the citation links in the background
        /// </summary>
        private async Task PopulateCitationContentsAsync(List<Citation> citations)
        {
            foreach (var citation in citations)
            {
                if (citation.Type == "법령")
                {
                    var match = Regex.Match(citation.Title, @"^(?<lawName>.+?)\s+제\s*(?<artNo>\d+)조");
                    if (match.Success)
                    {
                        var lawName = match.Groups["lawName"].Value.Trim();
                        var artNo = match.Groups["artNo"].Value.Trim();
                        
                        var res = await _mcpService.CallLawToolAsync("get_law_text", new Dictionary<string, object>
                        {
                            { "lawName", lawName },
                            { "articleNo", artNo }
                        });
                        
                        if (res.Ok)
                        {
                            citation.Content = res.Text ?? "Document text not found.";
                            continue;
                        }
                    }
                }
                else if (citation.Type == "판례")
                {
                    var match = Regex.Match(citation.Title, @"(?<caseNo>\d{4}[가-힣]{1,3}\d+)");
                    var caseNo = match.Success ? match.Groups["caseNo"].Value : citation.Title;
                    
                    var res = await _mcpService.CallLawToolAsync("get_decision_text", new Dictionary<string, object>
                    {
                        { "caseNo", caseNo }
                    });

                    if (res.Ok)
                    {
                        citation.Content = res.Text ?? "Document text not found.";
                        continue;
                    }
                }

                var fallbackRes = await _mcpService.CallLawToolAsync(
                    citation.Type == "법령" ? "search_law" : "search_decisions",
                    new Dictionary<string, object> { { "query", citation.Title } }
                );
                citation.Content = fallbackRes.Ok ? (fallbackRes.Text ?? "No content.") : "Retrieval failed.";
            }
        }
    }
}
