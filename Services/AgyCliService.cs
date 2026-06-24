using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LawDesktop.Services
{
    public class AgyCliService
    {
        public AgyCliService()
        {
        }

        /// <summary>
        /// Check if either codex or agy cli is installed and operational on the host system.
        /// </summary>
        public async Task<bool> CheckAgyCliInstalledAsync()
        {
            return await CheckCliInstalledAsync("codex") || await CheckCliInstalledAsync("agy");
        }

        public async Task<string> GetAvailableCliNameAsync()
        {
            if (await CheckCliInstalledAsync("codex")) return "codex";
            if (await CheckCliInstalledAsync("agy")) return "agy";
            return string.Empty;
        }

        private async Task<bool> CheckCliInstalledAsync(string fileName)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = AiCliCommandBuilder.ResolveCommandName(fileName),
                    Arguments = "--help",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null) return false;

                var completedTask = Task.Run(() => process.WaitForExit(3000));
                var timeoutTask = Task.Delay(3000);

                if (await Task.WhenAny(completedTask, timeoutTask) == timeoutTask)
                {
                    process.Kill();
                    return false;
                }

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Execute prompt on local codex cli first, falling back to agy cli when codex is unavailable.
        /// </summary>
        public async Task<AgyResult> ExecutePromptAsync(string prompt, string? workDir = null, string? codexThreadId = null)
        {
            var codexAvailable = await CheckCliInstalledAsync("codex");
            if (codexAvailable)
            {
                var codexResult = await ExecuteCodexAsync(prompt, workDir, codexThreadId);
                if (codexResult.Ok) return codexResult;
            }

            return await ExecuteAgyAsync(prompt, workDir);
        }

        private async Task<AgyResult> ExecuteCodexAsync(string prompt, string? workDir, string? threadId)
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"law-desktop-codex-{Guid.NewGuid():N}.txt");
            var command = AiCliCommandBuilder.BuildCodexExec(prompt, outputPath, NormalizeWorkDir(workDir), threadId);
            var result = await ExecuteCommandAsync(command, "codex", 180);

            try
            {
                if (File.Exists(outputPath))
                {
                    var lastMessage = await File.ReadAllTextAsync(outputPath, Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(lastMessage))
                    {
                        result.Text = lastMessage.Trim();
                        result.Ok = true;
                    }
                }

                var events = CodexJsonlParser.Parse(result.RawStdout ?? string.Empty);
                result.CliName = "codex";
                result.ThreadId = events.ThreadId;
                result.ToolCalls = events.ToolCalls;
                if (string.IsNullOrWhiteSpace(result.Text) && !string.IsNullOrWhiteSpace(events.FinalText))
                {
                    result.Text = events.FinalText.Trim();
                    result.Ok = true;
                }
            }
            finally
            {
                TryDelete(outputPath);
            }

            return result;
        }

        private async Task<AgyResult> ExecuteAgyAsync(string prompt, string? workDir)
        {
            var command = AiCliCommandBuilder.BuildAgyPrint(prompt, NormalizeWorkDir(workDir));
            var result = await ExecuteCommandAsync(command, "agy", 100);
            result.CliName = "agy";
            if (result.Ok)
            {
                result.Text = (result.RawStdout ?? string.Empty).Trim();
            }

            return result;
        }

        private async Task<AgyResult> ExecuteCommandAsync(AiCliCommand command, string displayName, int timeoutSec)
        {
            try
            {
                using var process = new Process { StartInfo = command.ToStartInfo() };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (_, args) =>
                {
                    if (args.Data != null) outputBuilder.AppendLine(args.Data);
                };
                process.ErrorDataReceived += (_, args) =>
                {
                    if (args.Data != null) errorBuilder.AppendLine(args.Data);
                };

                if (!process.Start())
                {
                    return new AgyResult { Ok = false, CliName = displayName, Error = $"Failed to start {displayName} cli process." };
                }
                process.StandardInput.Close();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var waitTask = process.WaitForExitAsync();
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSec));
                if (await Task.WhenAny(waitTask, timeoutTask) == timeoutTask)
                {
                    TryKill(process);
                    return new AgyResult { Ok = false, CliName = displayName, Error = $"{displayName} cli execution timed out ({timeoutSec} seconds)" };
                }

                await waitTask;
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var errText = errorBuilder.ToString().Trim();
                    return new AgyResult
                    {
                        Ok = false,
                        CliName = displayName,
                        Error = $"{displayName} cli terminated with error code {process.ExitCode}.\nDetails: {errText}",
                        RawStdout = outputBuilder.ToString(),
                        RawStderr = errorBuilder.ToString()
                    };
                }

                return new AgyResult
                {
                    Ok = true,
                    CliName = displayName,
                    RawStdout = outputBuilder.ToString(),
                    RawStderr = errorBuilder.ToString()
                };
            }
            catch (Exception ex)
            {
                return new AgyResult
                {
                    Ok = false,
                    CliName = displayName,
                    Error = $"Exception occurred while executing {displayName} cli: {ex.Message}"
                };
            }
        }

        private static string? NormalizeWorkDir(string? workDir)
        {
            return !string.IsNullOrWhiteSpace(workDir) && Directory.Exists(workDir) ? workDir : null;
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
            }
        }
    }

    public class AgyResult
    {
        public bool Ok { get; set; }
        public string? Text { get; set; }
        public string? Error { get; set; }
        public string? CliName { get; set; }
        public string? ThreadId { get; set; }
        public string? RawStdout { get; set; }
        public string? RawStderr { get; set; }
        public System.Collections.Generic.List<CodexToolCall> ToolCalls { get; set; } = new();
    }
}
