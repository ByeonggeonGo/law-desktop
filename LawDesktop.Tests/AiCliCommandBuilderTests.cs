using LawDesktop.Services;

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

        Assert.Equal("codex", command.FileName);
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

        Assert.Equal("agy", command.FileName);
        Assert.Contains("--print", command.Arguments);
        Assert.Equal(prompt, command.Arguments[^1]);
    }
}
