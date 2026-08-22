using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Views.Tests;

/// <summary>
/// Issue #953: hovering a usage-overlay rect on the PNG tab shows a "&lt;achx file&gt; (N)" tag,
/// reusing <see cref="AnimationEditor.App.Controls.WireframeControl.DrawHoverLabel"/>'s visual
/// (issue #718's "Frame N" notch) with a different label.
/// </summary>
public class PngPreviewControlUsageHoverTests
{
    private static UsageChainRegions MakeGroup(string achxFileName, string chainName, params SKRect[] rects)
    {
        var entry = new AchxFileEntry(new FakeFile(achxFileName), new FakeFolder("Content"), achxFileName);
        var chain = new AnimationChainSave { Name = chainName };
        var frameRegions = new List<UsageFrameRegion>();
        for (int i = 0; i < rects.Length; i++)
            frameRegions.Add(new UsageFrameRegion(i + 1, rects[i]));
        return new UsageChainRegions(entry, chain, SKColors.Red, frameRegions);
    }

    [AvaloniaFact]
    public void GetUsageHoverLabelForScreenPoint_DifferentAchxFiles_UsesEachGroupsFileName()
    {
        var ctrl = new PngPreviewControl();
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.SetUsageRegions(new[]
        {
            MakeGroup("Hero.achx", "Walk", new SKRect(0, 0, 16, 16)),
            MakeGroup("Enemy.achx", "Idle", new SKRect(20, 0, 36, 16)),
        });

        var label = ctrl.GetUsageHoverLabelForScreenPoint(28f, 8f);

        Assert.Equal("Enemy.achx (1)", label);
    }

    [AvaloniaFact]
    public void GetUsageHoverLabelForScreenPoint_MovedAwayAfterHoveringRegion_ReturnsNull()
    {
        var ctrl = new PngPreviewControl();
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.SetUsageRegions(new[] { MakeGroup("Hero.achx", "Walk", new SKRect(0, 0, 16, 16)) });
        Assert.Equal("Hero.achx (1)", ctrl.GetUsageHoverLabelForScreenPoint(8f, 8f));

        var label = ctrl.GetUsageHoverLabelForScreenPoint(100f, 100f);

        Assert.Null(label);
    }

    [AvaloniaFact]
    public void GetUsageHoverLabelForScreenPoint_OutsideAnyRegion_ReturnsNull()
    {
        var ctrl = new PngPreviewControl();
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.SetUsageRegions(new[] { MakeGroup("Hero.achx", "Walk", new SKRect(0, 0, 16, 16)) });

        var label = ctrl.GetUsageHoverLabelForScreenPoint(100f, 100f);

        Assert.Null(label);
    }

    [AvaloniaFact]
    public void GetUsageHoverLabelForScreenPoint_OverRegion_ReturnsFileNameAndFrameIndex()
    {
        var ctrl = new PngPreviewControl();
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.SetUsageRegions(new[] { MakeGroup("Hero.achx", "Walk", new SKRect(0, 0, 32, 32)) });

        var label = ctrl.GetUsageHoverLabelForScreenPoint(16f, 16f);

        Assert.Equal("Hero.achx (1)", label);
    }

    [AvaloniaFact]
    public void GetUsageHoverLabelForScreenPoint_OverSecondFrameOfChain_ReturnsFrameIndexTwo()
    {
        var ctrl = new PngPreviewControl();
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.SetUsageRegions(new[]
        {
            MakeGroup("Hero.achx", "Walk", new SKRect(0, 0, 16, 16), new SKRect(16, 0, 32, 16))
        });

        var label = ctrl.GetUsageHoverLabelForScreenPoint(24f, 8f); // inside the second rect

        Assert.Equal("Hero.achx (2)", label);
    }

    private sealed class FakeFile : IEditorFile
    {
        public FakeFile(string name) => Name = name;
        public string Name { get; }
        public Task<Stream> OpenReadAsync() => throw new NotSupportedException();
        public Task<Stream> OpenWriteAsync() => throw new NotSupportedException();
        public Task<FolderEntrySnapshot> GetBasicPropertiesAsync() =>
            Task.FromResult(new FolderEntrySnapshot(null, null));
    }

    private sealed class FakeFolder : IEditorFolder
    {
        public FakeFolder(string name) => Name = name;
        public string Name { get; }
#pragma warning disable CS1998 // no subfolders/items -- these entries are hand-built for the hover tests above
        public async IAsyncEnumerable<IEditorFile> GetItemsAsync() { yield break; }
        public async IAsyncEnumerable<IEditorFolder> GetSubfoldersAsync() { yield break; }
#pragma warning restore CS1998
        public Task<IEditorFile?> GetFileAsync(string name) => Task.FromResult<IEditorFile?>(null);
    }
}
