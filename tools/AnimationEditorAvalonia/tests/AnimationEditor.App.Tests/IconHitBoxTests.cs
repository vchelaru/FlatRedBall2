using AnimationEditor.App.Tests.Dogfood;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Shouldly;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #1243: a Border or Panel with no Background only hit-tests where it draws (its stroke and
/// children), and a rounded Border only inside its rounded shape, so a hover/click icon built from
/// one flickers as the mouse crosses its empty parts. Icons must hit-test across their whole box.
/// </summary>
public class IconHitBoxTests
{
    private static async Task<AnimationEditorHarness> OpenWithFrameSelectedAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(path);
        var chain = editor.ChainNamed("Walk");
        editor.Expand(chain);
        editor.ClickRow(chain.Frames[0]);
        return editor;
    }

    private static async Task<AnimationEditorHarness> OpenProjectFolderWithSubfolderAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        Directory.CreateDirectory(Path.Combine(editor.ProjectFolder, "enemies"));
        editor.WriteAchx(Path.Combine("enemies", "bat.achx"), AnimationEditorHarness.Chain("Fly", "../sheet.png", (0, 0, 16, 16)));
        await editor.Window.OpenProjectFolderForTestAsync(editor.ProjectFolder);
        editor.Layout();
        return editor;
    }

    [AvaloniaFact]
    public async Task ColorHelpIcon_HitTestsAcrossItsWholeBox()
    {
        using AnimationEditorHarness editor = await OpenWithFrameSelectedAsync();

        // A short window forces the Inspector to scroll, so the test exercises the scrolled layout on
        // every OS instead of only where larger fonts push the Color section off screen.
        editor.Window.Height = 300;
        editor.Layout();
        Control icon = editor.Control<Control>("ColorHelpIcon");
        editor.ScrollIntoView(icon);

        ShouldHitTestAcrossWholeBox(editor.Window, icon);
    }

    [AvaloniaFact]
    public async Task ProjectPanelFolderIcon_HitTestsAcrossItsWholeBox()
    {
        using AnimationEditorHarness editor = await OpenProjectFolderWithSubfolderAsync();
        // Hidden sidebar tabs keep their own folder icons in the tree; take one a user can reach.
        FolderExpanderIcon icon = editor.Window.GetVisualDescendants().OfType<FolderExpanderIcon>()
            .First(i => i.IsEffectivelyVisible && editor.Window.InputHitTest(editor.CenterOf(i)) is Visual hit && i.IsVisualAncestorOf(hit));

        ShouldHitTestAcrossWholeBox(editor.Window, icon);
    }

    [AvaloniaFact]
    public async Task HoverOrClickTargets_WithAFrameSelected_AllHaveABackground()
    {
        using AnimationEditorHarness editor = await OpenWithFrameSelectedAsync();
        editor.Control<Control>("ColorHelpIcon").IsEffectivelyVisible.ShouldBeTrue();

        OffendersIn(editor.Window).ShouldBeEmpty("give these a square Background=\"Transparent\" host so their whole box is hoverable");
    }

    [AvaloniaFact]
    public async Task HoverOrClickTargets_WithAProjectFolderOpen_AllHaveABackground()
    {
        using AnimationEditorHarness editor = await OpenProjectFolderWithSubfolderAsync();
        editor.Window.GetVisualDescendants().OfType<FolderExpanderIcon>().ShouldNotBeEmpty();

        OffendersIn(editor.Window).ShouldBeEmpty("give these a square Background=\"Transparent\" host so their whole box is hoverable");
    }

    /// <summary>
    /// Borders and panels that set their own cursor or tooltip but have no Background, or are rounded.
    /// Cursor is an inherited property, so only a locally set value counts.
    /// </summary>
    private static List<string> OffendersIn(Window window) => window.GetVisualDescendants()
        .OfType<Control>()
        .Where(control => control.IsSet(InputElement.CursorProperty) || control.IsSet(ToolTip.TipProperty))
        .Where(control => control switch
        {
            Border border => border.Background == null || border.CornerRadius != default,
            Panel panel => panel.Background == null,
            _ => false,
        })
        .Select(control => $"{control.GetType().Name} '{control.Name}' in {control.FindAncestorOfType<UserControl>()?.GetType().Name ?? "MainWindow"}")
        .ToList();

    private static void ShouldHitTestAcrossWholeBox(Window window, Control icon)
    {
        icon.IsEffectivelyVisible.ShouldBeTrue();
        List<string> misses = GridPointsIn(window, icon)
            .Select(point => (point, hit: window.InputHitTest(point) as Visual))
            .Where(x => x.hit == null || (x.hit != icon && !icon.IsVisualAncestorOf(x.hit)))
            // The window's 4px resize border overlaps the Project panel's folder icons at the left edge, by design.
            .Where(x => (x.hit as Control)?.Name is not ("GripW" or "GripE"))
            .Select(x => $"{x.point} hit {x.hit?.GetType().Name ?? "nothing"} '{(x.hit as Control)?.Name}'")
            .ToList();
        ScrollViewer? scroller = icon.FindAncestorOfType<ScrollViewer>();
        misses.ShouldBeEmpty($"icon at {icon.TranslatePoint(new Point(0, 0), window)} size {icon.Bounds.Size}; " +
            $"scroller '{scroller?.Name}' offset {scroller?.Offset} viewport {scroller?.Viewport} " +
            $"at {scroller?.TranslatePoint(new Point(0, 0), window)}");
    }

    /// <summary>A 5x5 grid of window points spanning the control's bounds, inset half a pixel.</summary>
    private static IEnumerable<Point> GridPointsIn(Visual root, Control control)
    {
        const double inset = 0.5;
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)!.Value;
        double width = control.Bounds.Width - inset * 2;
        double height = control.Bounds.Height - inset * 2;
        for (int row = 0; row < 5; row++)
        {
            for (int column = 0; column < 5; column++)
            {
                yield return new Point(
                    topLeft.X + inset + width * column / 4,
                    topLeft.Y + inset + height * row / 4);
            }
        }
    }
}
