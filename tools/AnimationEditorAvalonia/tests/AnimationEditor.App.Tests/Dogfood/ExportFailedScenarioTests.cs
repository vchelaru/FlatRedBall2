using Avalonia.Headless.XUnit;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

public class ExportFailedScenarioTests
{
    [AvaloniaTheory]
    [InlineData("MenuExportPixiJs", "walk.json")]
    [InlineData("MenuExportGodot", "walk.tres")]
    public async Task Export_OntoAFileThatCannotBeWritten_SaysSo(string menu, string name)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        await editor.OpenAsync(editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16))));
        string target = Path.Combine(editor.ProjectFolder, name);
        File.WriteAllText(target, "old");
        // The file itself, not its folder: an export writes in place, which a read-only folder allows on Linux.
        File.SetAttributes(target, FileAttributes.ReadOnly);
        editor.Dialogs.AnswerNextSaveFile(target);

        editor.ClickMenu(menu);

        bool said = await editor.WaitUntilAsync(() => editor.ProblemText is not null, TimeSpan.FromSeconds(3));
        File.SetAttributes(target, FileAttributes.Normal);
        said.ShouldBeTrue("the export failed and nothing says so");
    }
}
