using System.IO;
using System.Text.Json;
using FlatRedBall2.Automation;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Automation;

// --- The {"cmd":"input","type":"text"} protocol command ---

public class AutomationModeTextCommandTests
{
    private static (AutomationMode mode, StringWriter output) Make()
    {
        var output = new StringWriter();
        return (new AutomationMode(new FlatRedBallService(), output), output);
    }

    [Fact]
    public void TextCommand_Valid_ProducesNoResponse()
    {
        var (mode, output) = Make();

        mode.ProcessLine("{\"cmd\":\"input\",\"type\":\"text\",\"text\":\"abc\"}");
        mode.TryAdvanceFrame(0);

        // Same contract as the other input types: silence means accepted.
        output.ToString().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("{\"cmd\":\"input\",\"type\":\"text\"}")]
    [InlineData("{\"cmd\":\"input\",\"type\":\"text\",\"text\":42}")]
    public void TextCommand_MissingOrNonStringText_RespondsWithError(string line)
    {
        var (mode, output) = Make();

        mode.ProcessLine(line);
        mode.TryAdvanceFrame(0);

        output.ToString().ShouldContain("\"ok\":false");
    }

    [Theory]
    [InlineData("\\n")]
    [InlineData("\\t")]
    [InlineData("\\u0000")]
    public void TextCommand_ControlCharacter_RespondsWithErrorAndDeliversNothing(string escaped)
    {
        var (mode, output) = Make();

        // Gum silently drops control characters. Automation rejects them: a client sending "\n"
        // expecting Enter would otherwise face a text box that never changes, with nothing on the
        // wire to say why. Rejection is all-or-nothing — no partial "a" sneaking through.
        mode.ProcessLine("{\"cmd\":\"input\",\"type\":\"text\",\"text\":\"a" + escaped + "b\"}");
        mode.TryAdvanceFrame(0);
        mode.GumKeyboard.Activity(0);

        // Parsed rather than substring-matched: the raw line spells the code point "U+000A",
        // because System.Text.Json's default encoder escapes '+'.
        using var response = JsonDocument.Parse(output.ToString());
        response.RootElement.GetProperty("ok").GetBoolean().ShouldBeFalse();
        response.RootElement.GetProperty("error").GetString()!.ShouldContain("U+00");
        mode.GumKeyboard.GetStringTyped().ShouldBeEmpty();
    }

    [Fact]
    public void TextCommand_QueuedBeforeStep_IsDeliveredOnThatSteppedFrame()
    {
        var mode = new AutomationMode(new FlatRedBallService(), new StringWriter());

        mode.ProcessLine("{\"cmd\":\"input\",\"type\":\"text\",\"text\":\"hi\"}");
        mode.ProcessLine("{\"cmd\":\"step\"}");

        mode.TryAdvanceFrame(0).ShouldBeTrue();
        mode.GumKeyboard.Activity(0);

        mode.GumKeyboard.GetStringTyped().ShouldBe("hi");
    }
}
