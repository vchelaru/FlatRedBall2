using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The macOS system menu bar (#1625) carries View > Back and Forward like the in-window menu. The menu is built
/// headlessly from the same actions the Mac head uses, so what is checked here is which items exist, their
/// gesture, their enabled state and that they navigate; only the AppKit rendering needs a real Mac.
/// </summary>
public class NativeMenuNavigationScenarioTests
{
    private static async Task<AnimationEditorHarness> OpenHeroAsync()
    {
        var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 16, 16, 16)));
        await editor.OpenAsync(path);
        return editor;
    }

    private static NativeMenuItem ViewItem(NativeMenu bar, string header)
    {
        NativeMenu view = bar.Items.OfType<NativeMenuItem>().Single(item => item.Header == "View").Menu!;
        return view.Items.OfType<NativeMenuItem>().Single(item => item.Header == header);
    }

    private static NativeMenu BuildBar(AnimationEditorHarness editor) =>
        NativeMenuTestSupport.Build(editor);

    private static async Task SettleAsync(AnimationEditorHarness editor) => await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

    [AvaloniaFact]
    public async Task ViewMenu_HasBackAndForward_WithTheSameGesturesAsTheInWindowMenu()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);

        NativeMenuItem back = ViewItem(bar, "Back");
        NativeMenuItem forward = ViewItem(bar, "Forward");

        back.Gesture.ShouldBe(new KeyGesture(Key.Left, KeyModifiers.Alt));
        forward.Gesture.ShouldBe(new KeyGesture(Key.Right, KeyModifiers.Alt));
        back.Gesture.ShouldBe(editor.Control<MenuItem>("MenuNavBack").InputGesture);
        forward.Gesture.ShouldBe(editor.Control<MenuItem>("MenuNavForward").InputGesture);
    }

    [AvaloniaFact]
    public async Task BackAndForward_FollowTheHistory_LikeTheInWindowMenu()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);
        NativeMenuItem back = ViewItem(bar, "Back");
        NativeMenuItem forward = ViewItem(bar, "Forward");
        back.IsEnabled.ShouldBeFalse();
        forward.IsEnabled.ShouldBeFalse();

        editor.ClickRow(editor.ChainNamed("Run"));
        back.IsEnabled.ShouldBeTrue();
        forward.IsEnabled.ShouldBeFalse();

        back.Command!.Execute(null);
        await SettleAsync(editor);
        back.IsEnabled.ShouldBeFalse();
        forward.IsEnabled.ShouldBeTrue();
        back.IsEnabled.ShouldBe(editor.Control<MenuItem>("MenuNavBack").IsEnabled);
        forward.IsEnabled.ShouldBe(editor.Control<MenuItem>("MenuNavForward").IsEnabled);
    }

    [AvaloniaFact]
    public async Task BackAndForwardCommands_Navigate()
    {
        using var editor = await OpenHeroAsync();
        NativeMenu bar = BuildBar(editor);
        NativeMenuItem back = ViewItem(bar, "Back");
        NativeMenuItem forward = ViewItem(bar, "Forward");
        editor.ClickRow(editor.ChainNamed("Run"));
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Run");

        back.Command!.Execute(null);
        await SettleAsync(editor);
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Walk");

        forward.Command!.Execute(null);
        await SettleAsync(editor);
        editor.Services.SelectedState.SelectedChain!.Name.ShouldBe("Run");
    }
}
