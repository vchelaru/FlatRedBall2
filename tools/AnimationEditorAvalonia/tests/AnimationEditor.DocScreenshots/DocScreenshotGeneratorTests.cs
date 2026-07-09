using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AnimationEditor.App;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
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
    string OutputFileName);

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
    };
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

                    Control target = scenario.TargetControlName is null
                        ? window
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
}
