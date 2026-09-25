using System.IO;
using FlatRedBall2.Automation;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Automation;

// --- Installing the keyboard and cursor into Gum, and the engine tick that does it ---

// Installs into Gum's static input slots, so it lives with the other Gum-static tests.
[Collection(HeadlessGumFormsCollection.Name)]
public class AutomationGumInputInstallTests
{
    [Fact]
    public void EngineUpdate_WithAutomationActive_InstallsTheAutomationKeyboardAndCursorIntoGumForms()
    {
        var engine = new FlatRedBallService();
        try
        {
            engine.Start<Screen>();
            // Generous count so the tick loop never runs out of steps while waiting on the
            // reader thread to deliver the command.
            engine.StartAutomationMode(
                seed: 0,
                input: new StringReader("{\"cmd\":\"step\",\"count\":10000}\n"),
                output: new StringWriter());

            for (var i = 0; i < 500 && Gum.Forms.FormsUtilities.Keyboard is not AutomationGumKeyboard; i++)
            {
                engine.Update(new Microsoft.Xna.Framework.GameTime());
                System.Threading.Thread.Sleep(2);
            }

            // StartAutomationMode deliberately does not install, so only the update tick can
            // make this pass.
            Gum.Forms.FormsUtilities.Keyboard.ShouldBeOfType<AutomationGumKeyboard>();
            Gum.Forms.FormsUtilities.Cursor.ShouldBeOfType<AutomationGumCursor>();
        }
        finally
        {
            engine.Shutdown();
        }
    }

    [Fact]
    public void EngineUpdate_WithAutomationActiveAndNoStepPending_DoesNotThrowHeadless()
    {
        var engine = new FlatRedBallService();
        try
        {
            engine.Start<Screen>();
            engine.StartAutomationMode(seed: 0, input: new StringReader(""), output: new StringWriter());

            // An ungranted tick asks the Game to suppress its draw. A headless engine has no
            // Game and no draw to suppress, so this is a no-op rather than a crash.
            Should.NotThrow(() => engine.Update(new Microsoft.Xna.Framework.GameTime()));
        }
        finally
        {
            engine.Shutdown();
        }
    }

    [Fact]
    public void EnsureGumInputInstalled_AfterSomethingElseReplacesKeyboardAndCursor_ReinstallsBoth()
    {
        var mode = new AutomationMode(new FlatRedBallService(), new StringWriter());
        try
        {
            mode.EnsureGumInputInstalled();
            Gum.Forms.FormsUtilities.Keyboard.ShouldBeSameAs(mode.GumKeyboard);
            Gum.Forms.FormsUtilities.Cursor.ShouldBeSameAs(mode.GumCursor);

            // Stands in for FormsUtilities.InitializeDefaults, which overwrites both fields
            // depending on when the game calls Initialize.
            var other = new AutomationMode(new FlatRedBallService(), new StringWriter());
            Gum.Forms.FormsUtilities.SetKeyboard(other.GumKeyboard);
            Gum.Forms.FormsUtilities.SetCursor(other.GumCursor);
            mode.EnsureGumInputInstalled();

            Gum.Forms.FormsUtilities.Keyboard.ShouldBeSameAs(mode.GumKeyboard);
            Gum.Forms.FormsUtilities.Cursor.ShouldBeSameAs(mode.GumCursor);
        }
        finally
        {
            mode.Stop();
        }
    }

    [Fact]
    public void Stop_AfterInstall_RestoresTheKeyboardAndCursorItReplaced()
    {
        // Stands in for the cursor and keyboard Gum installed before automation took over.
        var gumOwned = new AutomationMode(new FlatRedBallService(), new StringWriter());
        Gum.Forms.FormsUtilities.SetKeyboard(gumOwned.GumKeyboard);
        Gum.Forms.FormsUtilities.SetCursor(gumOwned.GumCursor);
        var mode = new AutomationMode(new FlatRedBallService(), new StringWriter());
        mode.EnsureGumInputInstalled();

        mode.Stop();

        // Otherwise Gum keeps reading input from a dead engine after Shutdown.
        Gum.Forms.FormsUtilities.Keyboard.ShouldBeSameAs(gumOwned.GumKeyboard);
        Gum.Forms.FormsUtilities.Cursor.ShouldBeSameAs(gumOwned.GumCursor);
    }
}
