using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LawDesktop.Services
{
    public sealed class AiCliCommand
    {
        public string FileName { get; init; } = string.Empty;
        public List<string> Arguments { get; init; } = new();
        public string? WorkingDirectory { get; init; }

        public ProcessStartInfo ToStartInfo()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = FileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            foreach (var argument in Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            if (!string.IsNullOrWhiteSpace(WorkingDirectory))
            {
                startInfo.WorkingDirectory = WorkingDirectory;
            }

            return startInfo;
        }
    }

    public static class AiCliCommandBuilder
    {
        public static AiCliCommand BuildCodexExec(
            string prompt,
            string outputPath,
            string? workDir = null,
            string? threadId = null)
        {
            var args = new List<string>
            {
                "exec",
                "--json",
                "--output-last-message",
                outputPath,
                "--dangerously-bypass-approvals-and-sandbox"
            };

            if (!string.IsNullOrWhiteSpace(workDir))
            {
                args.Add("-C");
                args.Add(workDir);
            }

            if (!string.IsNullOrWhiteSpace(threadId))
            {
                args.Add("resume");
                args.Add(threadId);
            }

            args.Add(prompt);

            return new AiCliCommand
            {
                FileName = GetPlatformCommandName("codex"),
                Arguments = args,
                WorkingDirectory = workDir
            };
        }

        public static AiCliCommand BuildAgyPrint(string prompt, string? workDir = null)
        {
            return new AiCliCommand
            {
                FileName = GetPlatformCommandName("agy"),
                Arguments = new List<string>
                {
                    "--dangerously-skip-permissions",
                    "--print",
                    prompt
                },
                WorkingDirectory = workDir
            };
        }

        public static string GetPlatformCommandName(string command)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return command;
            return command == "codex" ? "codex.cmd" : $"{command}.exe";
        }
    }
}
