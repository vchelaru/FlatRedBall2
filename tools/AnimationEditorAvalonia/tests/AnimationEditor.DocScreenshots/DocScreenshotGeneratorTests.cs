using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AnimationEditor.App;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// Drives a named documentation scenario (chain/frame selection state) to completion,
/// then captures one control (or the whole window, when <see cref="TargetControlName"/>
/// is null) to <see cref="OutputFileName"/> via <see cref="ScreenshotCapture"/>.
/// </summary>
/// <remarks>
/// This is the manifest #636 asks for: scenario name → output PNG. Individual doc pages
/// (Timing, Offsets, Collision, …) are expected to add their own entries here — this file
/// only proves the harness end-to-end with a handful of representative scenarios (tree view,
/// inspector panel, full window chrome); actual doc-page screenshot content is follow-on work.
/// </remarks>
internal sealed record ScreenshotScenario(
    string Name,
    Action<MainWindow, TestServices> Arrange,
    string? TargetControlName,
    string OutputFileName,
    // True for scenarios that back a real doc page and get committed under docs/images/ by the
    // opt-in regenerator. Left false for proof-of-harness scenarios (fake texture names, hand-built
    // state) that only exist to verify the capture pipeline and must never land in the docs site.
    bool IncludeInDocs = false);

internal static class DocScreenshotManifest
{
    public static IReadOnlyList<ScreenshotScenario> Scenarios { get; } = new[]
    {
        new ScreenshotScenario(
            Name: "main-window-empty",
            Arrange: (_, _) => { },
            TargetControlName: null,
            OutputFileName: "main-window-empty.png"),

        new ScreenshotScenario(
            Name: "tree-view-two-chains",
            Arrange: (window, ctx) =>
            {
                var walk = new AnimationChainSave { Name = "Walk" };
                walk.Frames.Add(new AnimationFrameSave { TextureName = "walk_0.png" });
                walk.Frames.Add(new AnimationFrameSave { TextureName = "walk_1.png" });
                var idle = new AnimationChainSave { Name = "Idle" };
                idle.Frames.Add(new AnimationFrameSave { TextureName = "idle_0.png" });
                ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(walk);
                ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(idle);

                DocScreenshotGeneratorTests.InvokePrivate(window, "RefreshTreeView");
                Dispatcher.UIThread.RunJobs();
            },
            TargetControlName: "AnimTree",
            OutputFileName: "tree-view-two-chains.png"),

        new ScreenshotScenario(
            Name: "inspector-frame-selected",
            Arrange: (window, ctx) =>
            {
                var chain = new AnimationChainSave { Name = "Walk" };
                var frame = new AnimationFrameSave
                {
                    TextureName = "walk_0.png",
                    FrameLength = 0.1f,
                    RelativeX = 4,
                    RelativeY = -2,
                };
                chain.Frames.Add(frame);
                ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
                ctx.SelectedState.SelectedChain = chain;
                ctx.SelectedState.SelectedFrame = frame;

                Dispatcher.UIThread.RunJobs();
                Dispatcher.UIThread.RunJobs();
            },
            TargetControlName: "InspectorTabContent",
            OutputFileName: "inspector-frame-selected.png"),

        // ── Doc-page scenarios (committed under docs/images/) ─────────────────────────────
        //
        // "Your First Animation" hero shot: a knight walk cycle built from the real character
        // sheet, chain + first frame selected, captured as the full window so the tutorial reader
        // sees the finished result across all four panels (tree, wireframe, preview, inspector).
        new ScreenshotScenario(
            Name: "your-first-animation",
            Arrange: ArrangeFirstAnimation,
            TargetControlName: null,
            OutputFileName: "your-first-animation.png",
            IncludeInDocs: true),
    };

    /// <summary>Number of consecutive cells taken from the sheet row to form the walk cycle.</summary>
    private const int WalkFrameCount = 4;

    /// <summary>Row 1 (y=32) of characters.png is the knight walk cycle.</summary>
    private const int KnightRow = 1;

    private static void ArrangeFirstAnimation(MainWindow window, TestServices ctx)
    {
        ScenarioFixtures.UseCharacterSheetProject(ctx);

        // Build the chain the way the app does — through AppCommands — so every frame carries real
        // defaults (FrameLength = 0.1s, a ShapesSave), not the bare type defaults a hand-built
        // AnimationFrameSave would leave behind. AddFrame with an explicit texture starts a frame on
        // the whole sheet; we then crop each to its walk-cycle cell.
        var walk = ctx.AppCommands.AddAnimationChainWithName("Walk")!;
        for (int col = 0; col < WalkFrameCount; col++)
        {
            ctx.AppCommands.AddFrame(walk, "characters.png");
            SetCell(walk.Frames[^1], col, KnightRow);
        }

        // Select the chain (and its first frame) so the tree highlights it, the wireframe shows the
        // first cell, and the preview settles on frame 0.
        ctx.SelectedState.SelectedChain = walk;
        ctx.SelectedState.SelectedFrame = walk.Frames[0];
        Dispatcher.UIThread.RunJobs();

        // Zoom the wireframe to just the walk-cycle cells rather than the whole 736×128 sheet.
        window.FindControl<WireframeControl>("WireframeCtrl")?.FitChainToView(walk);

        // The preview renders a 32px sprite; at the default 100% it's a speck in a large canvas.
        // Zoom in so the walking knight reads clearly in the hero shot.
        window.FindControl<PreviewControl>("PreviewCtrl")?.SetZoomPercent(400);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Crops <paramref name="frame"/> to the (col, row) cell of the 32×32 grid sheet.</summary>
    private static void SetCell(AnimationFrameSave frame, int col, int row)
    {
        frame.LeftCoordinate   = col * ScenarioFixtures.CellSize / (float)ScenarioFixtures.SheetWidth;
        frame.RightCoordinate  = (col + 1) * ScenarioFixtures.CellSize / (float)ScenarioFixtures.SheetWidth;
        frame.TopCoordinate    = row * ScenarioFixtures.CellSize / (float)ScenarioFixtures.SheetHeight;
        frame.BottomCoordinate = (row + 1) * ScenarioFixtures.CellSize / (float)ScenarioFixtures.SheetHeight;
    }
}

/// <summary>
/// Generates every scenario in <see cref="DocScreenshotManifest"/> in one pass — the "single
/// command" #636 asks for (<c>dotnet test --filter GenerateAll</c>) — and verifies each PNG
/// was actually produced from that scenario's state rather than a blank/cached frame.
/// </summary>
public class DocScreenshotGeneratorTests
{
    /// <summary>Invokes a private instance method by name via reflection (e.g. MainWindow.RefreshTreeView).</summary>
    internal static void InvokePrivate(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{methodName} not found via reflection on {target.GetType()}");
        method.Invoke(target, null);
    }

    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.ConfirmAsync = (_, _) => System.Threading.Tasks.Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    /// <summary>True if the PNG at <paramref name="path"/> contains more than one distinct color.</summary>
    private static bool HasVisibleContent(string path)
    {
        using var bitmap = SKBitmap.Decode(path);
        var first = bitmap.GetPixel(0, 0);
        for (int y = 0; y < bitmap.Height; y += 4)
            for (int x = 0; x < bitmap.Width; x += 4)
                if (bitmap.GetPixel(x, y) != first)
                    return true;
        return false;
    }

    [AvaloniaFact]
    public void GenerateAll_ProducesNonBlankPngPerScenario()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "AnimationEditorDocScreenshots", Guid.NewGuid().ToString("N"));
        var producedPaths = new List<string>();
        try
        {
            foreach (var scenario in DocScreenshotManifest.Scenarios)
            {
                var (window, ctx) = CreateWindow();
                try
                {
                    scenario.Arrange(window, ctx);

                    // The animation trees are created per tab, so none carries a name in the window's scope.
                    Control target = scenario.TargetControlName is null
                        ? window
                        : scenario.TargetControlName == "AnimTree"
                            ? window.AnimTree
                            : window.FindControl<Control>(scenario.TargetControlName)
                                ?? throw new InvalidOperationException(
                                    $"Scenario '{scenario.Name}': control '{scenario.TargetControlName}' not found.");

                    var outputPath = Path.Combine(outputDir, scenario.OutputFileName);
                    ScreenshotCapture.Capture(target, outputPath);

                    Assert.True(File.Exists(outputPath), $"Scenario '{scenario.Name}' did not write a PNG.");
                    Assert.True(new FileInfo(outputPath).Length > 0, $"Scenario '{scenario.Name}' wrote an empty PNG.");
                    Assert.True(HasVisibleContent(outputPath),
                        $"Scenario '{scenario.Name}' captured a blank (single-color) image — " +
                        "the arrange step likely didn't reach the app before capture.");

                    producedPaths.Add(outputPath);
                }
                finally { window.Close(); }
            }

            // Distinct scenarios must produce distinct images — guards against the capture
            // helper always returning the same (e.g. stale/cached) frame regardless of state.
            for (int i = 0; i < producedPaths.Count; i++)
                for (int j = i + 1; j < producedPaths.Count; j++)
                    Assert.False(
                        File.ReadAllBytes(producedPaths[i]).AsSpan().SequenceEqual(File.ReadAllBytes(producedPaths[j])),
                        $"Scenarios '{DocScreenshotManifest.Scenarios[i].Name}' and " +
                        $"'{DocScreenshotManifest.Scenarios[j].Name}' produced byte-identical PNGs.");
        }
        finally
        {
            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, recursive: true);
        }
    }

    /// <summary>
    /// Opt-in regeneration of the committed documentation PNGs under <c>docs/images/</c> — #636's
    /// "rerun the script" entry point. Skipped unless <c>FRB_WRITE_DOC_SCREENSHOTS=1</c> so a normal
    /// <c>dotnet test</c> (including CI) never rewrites tracked files: headless AA/font rendering
    /// isn't byte-identical across platforms, so only a human deliberately regenerating on one
    /// machine should touch them. To refresh the images: set the env var, run this test, review and
    /// commit the changed PNGs.
    /// </summary>
    [AvaloniaFact]
    public void RegenerateCommittedDocScreenshots()
    {
        if (Environment.GetEnvironmentVariable("FRB_WRITE_DOC_SCREENSHOTS") != "1")
            return;

        var imagesDir = Path.Combine(
            RepoLayout.FindDocsRoot(AppContext.BaseDirectory)
                ?? throw new InvalidOperationException(
                    $"Could not locate a docs/ folder above {AppContext.BaseDirectory}."),
            "images");

        foreach (var scenario in DocScreenshotManifest.Scenarios)
        {
            if (!scenario.IncludeInDocs) continue;

            var (window, ctx) = CreateWindow();
            try
            {
                scenario.Arrange(window, ctx);

                Control target = scenario.TargetControlName is null
                    ? window
                    : window.FindControl<Control>(scenario.TargetControlName)
                        ?? throw new InvalidOperationException(
                            $"Scenario '{scenario.Name}': control '{scenario.TargetControlName}' not found.");

                ScreenshotCapture.Capture(target, Path.Combine(imagesDir, scenario.OutputFileName));
            }
            finally { window.Close(); }
        }
    }
}
