using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LawDesktop.Examples.CliConnection
{
    public static class Examples
    {
        private const string DefaultAgyPath = "agy.exe"; // Windows 환경 기준, PATH에 있다고 가정

        /// <summary>
        /// 예시 1: 단순 1회성(One-shot) 프롬프트 전송 및 응답 수신
        /// CLI에 인자를 직접 주어 실행하고, 결과를 즉시 텍스트로 받아와 채팅 UI에 바인딩할 때 사용합니다.
        /// </summary>
        public static async Task RunOneShotExampleAsync()
        {
            Console.WriteLine("[예시 1] 1회성 CLI 호출 테스트 시작");
            var connector = new CliConnector(DefaultAgyPath);

            try
            {
                // Antigravity CLI로 프롬프트 전송을 모사하기 위한 인자 설정
                // 실제 agy command 구조: agy --print "프롬프트 내용"
                var args = new[] { "--print", "Hello Antigravity! 간단한 환영 메시지를 한 줄로 작성해줘." };
                
                string response = await connector.ExecuteOneShotAsync(args, timeoutMs: 15000);
                
                Console.WriteLine("=== CLI 응답 결과 ===");
                Console.WriteLine(response);
                Console.WriteLine("======================\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"오류 발생: {ex.Message}\n");
            }
        }

        /// <summary>
        /// 예시 2: 실시간 출력 스트리밍 연동
        /// LLM의 응답이 길어질 때, 한 번에 받아오지 않고 스트림 형태로 실시간 출력하여 채팅 UI를 부드럽게 업데이트합니다.
        /// </summary>
        public static async Task RunStreamingExampleAsync()
        {
            Console.WriteLine("[예시 2] 실시간 출력 스트리밍 연동 테스트 시작");
            var connector = new CliConnector(DefaultAgyPath);

            try
            {
                var args = new[] { "--print", "대한민국의 헌법 제1조에 대해 상세히 설명하고 그 의의를 적어줘." };

                Console.WriteLine("=== 실시간 수신 데이터 ===");
                await connector.ExecuteStreamAsync(
                    args,
                    onDataReceived: (line) =>
                    {
                        // UI 스레드에서 수신한 라인을 텍스트박스나 리스트뷰에 덧붙이는 동작을 모사
                        Console.WriteLine($">> {line}");
                    },
                    onErrorReceived: (errorLine) =>
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[에러 출력]: {errorLine}");
                        Console.ResetColor();
                    }
                );
                Console.WriteLine("==========================\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"오류 발생: {ex.Message}\n");
            }
        }

        /// <summary>
        /// 예시 3: 타임아웃 및 예외 처리
        /// CLI 호출 시 발생할 수 있는 네트워크 지연, 먹통 현상 등을 타임아웃으로 방지하고 예외 처리하는 안전 장치 예시입니다.
        /// </summary>
        public static async Task RunTimeoutExampleAsync()
        {
            Console.WriteLine("[예시 3] 타임아웃 및 예외 처리 테스트 시작");
            
            // 존재하지 않는 경로를 입력하여 에러 상황 모사
            var connector = new CliConnector("non_existent_cli_path.exe");

            try
            {
                var args = new[] { "some_argument" };
                await connector.ExecuteOneShotAsync(args, timeoutMs: 2000);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("예상된 오류 발생 (성공):");
                Console.WriteLine(ex.Message);
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 예시 4: JSON 기반의 구조화된 데이터 통신 (IPC)
        /// CLI와 주고받는 메시지 형태가 일반 텍스트가 아닌 JSON 포맷일 때 파싱하여 채팅 모델(Model) 객체에 저장하는 예시입니다.
        /// </summary>
        public static async Task RunJsonCommunicationExampleAsync()
        {
            Console.WriteLine("[예시 4] JSON 기반 통신 및 파싱 예제 시작");
            
            // 실제 프로젝트의 CodexJsonlParser 등과 유사한 방식으로 JSON 입출력을 처리합니다.
            // 여기서는 임의의 JSON 반환 CLI 동작을 테스트하기 위해 agy의 json 출력 플래그 등을 모사합니다.
            var connector = new CliConnector(DefaultAgyPath);

            try
            {
                // JSON 출력을 모사할 수 있는 CLI 명령 혹은 인자 지정
                // 여기서는 JSON 형식 데이터를 반환하도록 유도하는 프롬프트 작성
                var args = new[] { 
                    "--print", 
                    "다음 정보를 반드시 JSON 형식만 반환해줘. 그 외의 텍스트는 생략해. { \"user\": \"Antigravity\", \"status\": \"Success\", \"message\": \"CLI와 채팅 UI가 성공적으로 연결되었습니다.\" }" 
                };

                string rawOutput = await connector.ExecuteOneShotAsync(args, timeoutMs: 15000);
                
                // JSON 추출 시도 (마크다운 코드 블록 등이 포함될 수 있으므로 정제)
                string jsonText = CleanJsonString(rawOutput);
                
                using var doc = JsonDocument.Parse(jsonText);
                var root = doc.RootElement;
                
                Console.WriteLine("=== JSON 파싱 결과 ===");
                Console.WriteLine($"사용자: {root.GetProperty("user").GetString()}");
                Console.WriteLine($"상태: {root.GetProperty("status").GetString()}");
                Console.WriteLine($"메시지: {root.GetProperty("message").GetString()}");
                Console.WriteLine("======================\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"JSON 처리 중 오류 발생 (CLI 응답 포맷 이슈 등): {ex.Message}\n");
            }
        }

        private static string CleanJsonString(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "{}";
            
            var trimmed = raw.Trim();
            // ```json ... ``` 형태 제거
            if (trimmed.StartsWith("```"))
            {
                var lines = trimmed.Split('\n');
                var sb = new StringBuilder();
                foreach (var line in lines)
                {
                    var cleanLine = line.Trim();
                    if (cleanLine.StartsWith("```")) continue;
                    sb.AppendLine(cleanLine);
                }
                trimmed = sb.ToString().Trim();
            }
            
            // 실제 JSON 시작 중괄호와 끝 중괄호 범위를 추출
            int start = trimmed.IndexOf('{');
            int end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return trimmed.Substring(start, end - start + 1);
            }
            return trimmed;
        }
    }
}
