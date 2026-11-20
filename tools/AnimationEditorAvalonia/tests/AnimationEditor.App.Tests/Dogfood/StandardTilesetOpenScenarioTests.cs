using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The repo's own project template tileset (<c>.claude/templates/Tiled/StandardTileset.tsx</c>:
/// wangsets, per-tile collision object layers, hundreds of tiles) opens as a native project and an
/// edit saves without losing any of it.
/// </summary>
public class StandardTilesetOpenScenarioTests
{
    private static string TemplateFolder()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, ".claude", "templates", "Tiled", "StandardTileset.tsx")))
            dir = Path.GetDirectoryName(dir);
        dir.ShouldNotBeNull("run from inside the repo");
        return Path.Combine(dir, ".claude", "templates", "Tiled");
    }

    private static async Task OpenEditAndSaveAsync(string sourceTsx)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string sourceDir = Path.GetDirectoryName(sourceTsx)!;
        string tsx = Path.Combine(editor.ProjectFolder, Path.GetFileName(sourceTsx));
        File.Copy(sourceTsx, tsx);
        File.Copy(Path.Combine(sourceDir, "StandardTilesetIcons.png"), Path.Combine(editor.ProjectFolder, "StandardTilesetIcons.png"));
        string before = File.ReadAllText(tsx);

        await editor.OpenAsync(tsx);

        editor.Services.ProjectManager.IsNativeTsxProject.ShouldBeTrue("the tileset opens natively instead of being refused");
        editor.ProblemText.ShouldBeNull();

        editor.Click(editor.Control<Button>("AddChainBtn"));
        editor.Press(Key.Enter);
        var chain = editor.Project.AnimationChains.Last();
        editor.Click(editor.RowButton(chain, "Add Frame"));

        string after = File.ReadAllText(tsx);
        after.ShouldContain("<animation>", customMessage: "the edit was saved");
        CountOf(after, "<objectgroup").ShouldBe(CountOf(before, "<objectgroup"));
        CountOf(after, "<object ").ShouldBe(CountOf(before, "<object "));
        CountOf(after, "<wangset ").ShouldBe(CountOf(before, "<wangset "));
        CountOf(after, "<wangtile ").ShouldBe(CountOf(before, "<wangtile "));
        CountOf(after, "<polygon ").ShouldBe(CountOf(before, "<polygon "));
    }

    private static int CountOf(string text, string needle) =>
        (text.Length - text.Replace(needle, "").Length) / needle.Length;

    [AvaloniaFact]
    public async Task TemplateStandardTileset_OpensNatively_AndKeepsWangsetsAndCollisionOnSave() =>
        await OpenEditAndSaveAsync(Path.Combine(TemplateFolder(), "StandardTileset.tsx"));
}
