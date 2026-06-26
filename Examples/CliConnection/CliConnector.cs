using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LawDesktop.Examples.CliConnection
{
    /// <summary>
    /// CLI 도구(예: agy, codex 등)를 실행하고 표준 입출력을 통해 채팅 UI와 연동할 수 있도록 지원하는 커넥터 클래스입니다.
    /// </summary>
    public class CliConnector
    {
        private readonly string _cliPath;

        public CliConnector(string cliPath)
        {
            _cliPath = cliPath ?? throw new ArgumentNullException(nameof(cliPath));
        }

        /// <summary>
        /// 단순 실행형 (One-shot) 호출: 프롬프트를 인자로 전달하고 결과를 한 번에 받아옵니다.
        /// </summary>
        public async Task<string> ExecuteOneShotAsync(string[] arguments, string? workingDirectory = null, int timeoutMs = 30000)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _cliPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = workingDirectory ?? AppDomain.CurrentDomain.BaseDirectory
            };

            foreach (var arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException($"프로세스 시작 실패: {_cliPath}");
            }

            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
                var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

                await process.WaitForExitAsync(cts.Token);

                var stdout = await stdoutTask;
                var stderr = await stderrTask;

                if (process.ExitCode != 0)
                {
                    throw new Exception($"CLI 프로세스가 에러 코드 {process.ExitCode}로 종료되었습니다.\n에러 내용: {stderr}");
                }

                return stdout;
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"CLI 실행이 타임아웃({timeoutMs}ms) 기준을 초과하였습니다.");
            }
        }

        /// <summary>
        /// 실시간 스트리밍형 호출: 프로세스의 출력을 실시간으로 한 줄씩 읽어서 콜백으로 전달합니다. (채팅 UI 실시간 업데이트용)
        /// </summary>
        public async Task ExecuteStreamAsync(
            string[] arguments,
            Action<string> onDataReceived,
            Action<string> onErrorReceived,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _cliPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = workingDirectory ?? AppDomain.CurrentDomain.BaseDirectory
            };

            foreach (var arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    onDataReceived(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    onErrorReceived(e.Data);
                }
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"프로세스 시작 실패: {_cliPath}");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }
        }

        /// <summary>
        /// 대화형 (Interactive) 세션 호출: 표준 입력을 통해 계속해서 명령을 입력하고 출력을 받아오는 장기 지속 세션입니다.
        /// </summary>
        public async Task StartInteractiveSessionAsync(
            string[] arguments,
            Func<StreamWriter, Task> interactionLogic,
            Action<string> onOutputLine,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _cliPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = workingDirectory ?? AppDomain.CurrentDomain.BaseDirectory
            };

            foreach (var arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                throw new InvalidOperationException($"프로세스 시작 실패: {_cliPath}");
            }

            // 입출력 스트림 획득
            using var writer = process.StandardInput;
            using var reader = process.StandardOutput;

            // 표준 에러 비동기 리드
            var errorReadTask = Task.Run(async () =>
            {
                while (!process.StandardError.EndOfStream)
                {
                    var errLine = await process.StandardError.ReadLineAsync(cancellationToken);
                    if (errLine != null)
                    {
                        // 디버그 또는 에러 로깅용
                        Debug.WriteLine($"[CLI Error] {errLine}");
                    }
                }
            }, cancellationToken);

            // 출력 처리 루프와 인터랙션 로직을 동시에 실행
            var outputReadTask = Task.Run(async () =>
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (line != null)
                    {
                        onOutputLine(line);
                    }
                }
            }, cancellationToken);

            // 사용자가 입력 스트림을 다루는 비즈니스 로직 실행
            await interactionLogic(writer);

            // 프로세스가 완전히 종료될 때까지 대기
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputReadTask, errorReadTask);
        }
    }
}
