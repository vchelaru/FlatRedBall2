using System.Text.Json;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Views.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Shapes, Origin and Bounding Box are one setting each for the <c>.achx</c> preview and the sequence preview: flipping a
/// toggle in either flips the other, and both are remembered in the one settings key.
/// </summary>
public class SharedPreviewToggleScenarioTests
{
    private static readonly (string Achx, string Seq)[] Pairs =
    {
        ("ShowShapesCheck", "SequenceShowShapes"),
        ("ShowOriginCheck", "SequenceShowOrigin"),
        ("ShowBoundingBoxCheck", "SequenceShowBoundingBox"),
    };

    private static bool Checked(AnimationEditorHarness editor, string name) =>
        editor.Control<ToggleButton>(name).IsChecked == true;

    private static PreviewControl SeqPreview(AnimationEditorHarness editor) => editor.Control<PreviewControl>("SequencePreview");

    private static bool PreviewValue(PreviewControl preview, int index) => index switch
    {
        0 => preview.ShowShapes,
        1 => preview.ShowOrigin,
        _ => preview.ShowBoundingBox,
    };

    [AvaloniaFact]
    public async Task ATogglePressedInOnePreview_ChangesTheOtherPreviewToo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Idle"));

        for (int i = 0; i < Pairs.Length; i++)
        {
            (string achx, string seq) = Pairs[i];
            bool start = Checked(editor, achx);
            Checked(editor, seq).ShouldBe(start, $"{seq} should start where {achx} is");

            editor.Control<ToggleButton>(achx).IsChecked = !start;
            Checked(editor, seq).ShouldBe(!start, $"{achx} flipped, so {seq} should follow");
            PreviewValue(SeqPreview(editor), i).ShouldBe(!start, $"the sequence preview should follow {achx}");

            editor.Control<ToggleButton>(seq).IsChecked = start;
            Checked(editor, achx).ShouldBe(start, $"{seq} flipped, so {achx} should follow");
            PreviewValue(editor.Preview, i).ShouldBe(start, $"the achx preview should follow {seq}");
        }
    }

    [AvaloniaFact]
    public async Task TheToggles_AreRememberedUnderOneKeyEach_AndTheSequenceOnlyKeysAreGone()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string settingsRoot = Path.Combine(folder, "settings");
        Directory.CreateDirectory(folder);
        string settingsPath = AppSettingsLocation.ForApplicationDataRoot(settingsRoot).FullPath;
        try
        {
            using (AnimationEditorHarness editor = new AnimationEditorHarness(settingsRoot))
            {
                await editor.OpenAsync(editor.WriteSeqj("boss.seqj", "Idle"));
                // Each pressed in the preview that is not the achx one, and the achx one for Shapes, so both directions persist.
                editor.Control<ToggleButton>("ShowShapesCheck").IsChecked = false;
                editor.Control<ToggleButton>("SequenceShowOrigin").IsChecked = true;
                editor.Control<ToggleButton>("SequenceShowBoundingBox").IsChecked = false;
            }

            using JsonDocument stored = JsonDocument.Parse(File.ReadAllText(settingsPath));
            JsonElement root = stored.RootElement;
            root.GetProperty("ShowShapes").GetBoolean().ShouldBeFalse();
            root.GetProperty("ShowOrigin").GetBoolean().ShouldBeTrue();
            root.GetProperty("ShowBoundingBox").GetBoolean().ShouldBeFalse();
            root.TryGetProperty("SequenceShowShapes", out _).ShouldBeFalse();
            root.TryGetProperty("SequenceShowOrigin", out _).ShouldBeFalse();

            using (AnimationEditorHarness editor = new AnimationEditorHarness(settingsRoot))
            {
                editor.WaitUntil(() => editor.TabLabels.Count >= 1, TimeSpan.FromSeconds(5));
                foreach ((string achx, string seq) in Pairs)
                    Checked(editor, achx).ShouldBe(Checked(editor, seq));
                Checked(editor, "ShowShapesCheck").ShouldBeFalse();
                Checked(editor, "ShowOriginCheck").ShouldBeTrue();
                Checked(editor, "ShowBoundingBoxCheck").ShouldBeFalse();
                editor.Preview.ShowShapes.ShouldBeFalse();
                editor.Preview.ShowOrigin.ShouldBeTrue();
            }
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }

    [AvaloniaFact]
    public void AnOldSequenceOnlyKeyInTheSettingsFile_IsIgnored()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string settingsRoot = Path.Combine(folder, "settings");
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(settingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath, """{ "SequenceShowShapes": false, "SequenceShowOrigin": true }""");
        try
        {
            using AnimationEditorHarness editor = new AnimationEditorHarness(settingsRoot);

            // Not migrated: both previews are at the .achx defaults (Shapes on, Origin off).
            Checked(editor, "ShowShapesCheck").ShouldBeTrue();
            Checked(editor, "SequenceShowShapes").ShouldBeTrue();
            Checked(editor, "ShowOriginCheck").ShouldBeFalse();
            Checked(editor, "SequenceShowOrigin").ShouldBeFalse();
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }
}
