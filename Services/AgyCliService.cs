using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace LawDesktop.Services
{
    public class AgyCliService
    {
        private string _model = "gemini-3.5-flash"; // default model

        public AgyCliService(string? model = null)
        {
            if (!string.IsNullOrEmpty(model))
            {
                _model = model;
            }
        }

        public void UpdateModel(string model)
        {
            if (!string.IsNullOrEmpty(model))
            {
                _model = model;
            }
        }

        /// <summary>
        /// Check if agy cli is installed and operational on the host system
        /// </summary>
        public async Task<bool> CheckAgyCliInstalledAsync()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "agy",
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
        /// Execute prompt on local agy cli non-interactively
        /// </summary>
        public async Task<AgyResult> ExecutePromptAsync(string prompt, string? workDir = null)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "agy",
                    Arguments = $"--dangerously-skip-permissions --model \"{_model}\" --print \"{prompt.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir))
                {
                    startInfo.WorkingDirectory = workDir;
                }

                using var process = new Process { StartInfo = startInfo };
                
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        outputBuilder.AppendLine(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        errorBuilder.AppendLine(args.Data);
                    }
                };

                if (!process.Start())
                {
                    return new AgyResult { Ok = false, Error = "Failed to start agy cli process." };
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var timeoutSec = 100;
                var waitTask = Task.Run(() => process.WaitForExit());
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSec));

                if (await Task.WhenAny(waitTask, timeoutTask) == timeoutTask)
                {
                    process.Kill();
                    return new AgyResult { Ok = false, Error = $"agy cli execution timed out ({timeoutSec} seconds)" };
                }

                if (process.ExitCode != 0)
                {
                    var errText = errorBuilder.ToString().Trim();
                    return new AgyResult
                    {
                        Ok = false,
                        Error = $"agy cli terminated with error code {process.ExitCode}.\nDetails: {errText}",
                        Text = outputBuilder.ToString()
                    };
                }

                return new AgyResult
                {
                    Ok = true,
                    Text = outputBuilder.ToString().Trim()
                };
            }
            catch (Exception ex)
            {
                return new AgyResult
                {
                    Ok = false,
                    Error = $"Exception occurred while executing agy cli: {ex.Message}"
                };
            }
        }
    }

    public class AgyResult
    {
        public bool Ok { get; set; }
        public string? Text { get; set; }
        public string? Error { get; set; }
    }
}
