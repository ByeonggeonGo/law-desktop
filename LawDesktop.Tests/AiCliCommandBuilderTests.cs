using LawDesktop.Services;
using System.Runtime.InteropServices;

namespace LawDesktop.Tests;

public class AiCliCommandBuilderTests
{
    [Fact]
    public void BuildCodexExecPlacesOptionsBeforeResume()
    {
        var command = AiCliCommandBuilder.BuildCodexExec(
            prompt: "임대차 보증금 반환",
            outputPath: @"C:\temp\last-message.txt",
            workDir: @"C:\repo",
            threadId: "thread-123");

        Assert.EndsWith(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "codex.cmd" : "codex", command.FileName);
        Assert.Contains("--json", command.Arguments);
        Assert.Contains("--output-last-message", command.Arguments);

        var resumeIndex = command.Arguments.IndexOf("resume");
        Assert.True(resumeIndex > 0);
        Assert.True(command.Arguments.IndexOf("-C") < resumeIndex);
        Assert.True(command.Arguments.IndexOf("--output-last-message") < resumeIndex);
        Assert.Equal("thread-123", command.Arguments[resumeIndex + 1]);
        Assert.Equal("임대차 보증금 반환", command.Arguments[^1]);
    }

    [Fact]
    public void BuildAgyPrintPassesPromptAsSingleArgument()
    {
        var prompt = "첫 줄\n\"둘째 줄\"";

        var command = AiCliCommandBuilder.BuildAgyPrint(prompt);

        Assert.EndsWith(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "agy.exe" : "agy", command.FileName);
        Assert.Contains("--print", command.Arguments);
        Assert.Equal(prompt, command.Arguments[^1]);
    }

    [Fact]
    public void StartInfoClosesStandardInputSoCodexDoesNotWaitForPipedInput()
    {
        var command = AiCliCommandBuilder.BuildCodexExec(
            prompt: "OK",
            outputPath: @"C:\temp\last-message.txt");

        var startInfo = command.ToStartInfo();

        Assert.True(startInfo.RedirectStandardInput);
    }

    [Fact]
    public void WindowsCommandCandidatesIncludeUserInstallLocationsBeforePathLookup()
    {
        var candidates = AiCliCommandBuilder.GetWindowsCommandCandidates(
            "codex",
            @"C:\Users\me\AppData\Roaming",
            @"C:\Users\me\AppData\Local");

        Assert.Equal(@"C:\Users\me\AppData\Roaming\npm\codex.cmd", candidates[0]);
        Assert.Contains("codex.cmd", candidates);
    }
}
