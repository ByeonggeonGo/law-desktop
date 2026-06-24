# LawDesktop
An offline-ready legal consulting desktop application built with C# .NET 8.0 WPF, utilizing local **Codex CLI (`codex`) first and Antigravity CLI (`agy`) as fallback** as the inference engine and integrating the **Korea Law MCP (Model Context Protocol)** server.

## 🌟 Key Features
- **Local CLI Execution**: The application executes prompts directly via your locally authenticated `codex` CLI, falling back to `agy` when Codex is not available.
- **Real-time Legal Data Retrieval (RAG)**: Retrieves matching Korean legal articles and judicial precedents in real-time from the Ministry of Government Legislation database using MCP server commands.
- **Citation Validity Guard (verify_citations)**: Prevents legal hallucinations by verifying AI citations. If a referenced precedent or article is invalid, the tool automatically annotates it as `[법령: 민법 제750조 → 검증실패]` (Verification Failed) in the chat window.
- **Modern Dark UI**: A clean, eye-friendly dark mode chat environment. Features a split-pane layout; clicking on any citation link in the chat immediately loads the full-text details of the law or precedent in the right-hand panel.

## 🛠️ System Requirements
1. **.NET 8.0 SDK** (or higher) installed.
2. **Codex CLI (`codex`) or Antigravity CLI (`agy`)** installed, authenticated, and configured in your system environment variable `PATH`.
3. An **OC Key** (Ministry of Government Legislation Open API Key) for retrieving full database contents. A default free API key (`0428`) is configured out of the box.

## 🚀 Getting Started
```bash
# Clone the repository
git clone https://github.com/ByeonggeonGo/law-desktop.git
cd law-desktop

# Build and run the WPF application
dotnet run
```

## 📂 Architecture Flow
1. **Intro (Intent Classification)**: Analyzes the user inquiry inside C# to determine whether database search is needed or if it is a simple greeting.
2. **Keyword Extraction**: Queries the local CLI (`codex exec --json --output-last-message`, or `agy --print` fallback) to intelligently extract optimal Korean search terms from the user question.
3. **MCP Search**: Sends JSON-RPC v2.0 queries to the remote Law MCP backend to retrieve relevant Korean laws and decisions.
4. **Draft Generation (LLM)**: Combines user question and collected context into a structured prompt, and passes it to the local CLI to generate a grounded response in English.
5. **Outro (Citation Guard)**: Calls `verify_citations` to check the validity of all cited laws and precedents. Hallucinated references are annotated as failed before rendering.
