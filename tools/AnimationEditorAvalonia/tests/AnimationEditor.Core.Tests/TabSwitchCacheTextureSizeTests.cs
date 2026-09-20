using AnimationEditor.Core;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Reproduces the TODO item in plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md:
/// <c>ProjectManager._knownTextureSizes</c> has the same "private per-load state
/// <see cref="TabEditorCache"/> can't see" shape as the just-fixed native-tsx fields --
/// a cache-hit tab switch (<see cref="AnimationEditor.Core.CommandsAndState.AppCommands.TryActivateTabFromCache"/>)
/// never round-trips it, so a save on the reactivated tab uses whatever tab was
/// <c>LoadAnimationChain</c>-ed last instead of the reactivated tab's own known texture sizes.
/// </summary>
[Collection("SequentialSingletons")]
public class TabSwitchCacheTextureSizeTests
{
    private readonly TestServices _ctx = new();

    private static AnimationChainListSave BuildAcls(string chainName, string textureName)
    {
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = textureName,
            LeftCoordinate = 0,
            RightCoordinate = 64,
            TopCoordinate = 0,
            BottomCoordinate = 64,
        });
        acls.AnimationChains.Add(chain);
        return acls;
    }

    [Fact]
    public void TryActivateTabFromCache_SwitchBackAfterAnotherTabLoaded_SaveUsesThisTabsKnownTextureSizesNotTheOtherTabs()
    {
        var tabA = new TabEntry(new FilePath("A.achx"));
        var tabB = new TabEntry(new FilePath("B.achx"));

        // Tab A is loaded with a known texture size for its own texture (the browser-wasm
        // seam -- no PNG on disk to fall back to reading).
        _ctx.ProjectManager.LoadAnimationChain(
            new FilePath("A.achx"),
            preParsed: BuildAcls("ChainA", "TexA.png"),
            knownTextureSizes: new Dictionary<string, (int Width, int Height)> { ["TexA.png"] = (64, 64) });
        _ctx.AppCommands.CaptureTabEditorState(tabA);

        // Tab B is loaded next, with no known texture size at all -- this clears/replaces
        // ProjectManager's private _knownTextureSizes field.
        _ctx.ProjectManager.LoadAnimationChain(
            new FilePath("B.achx"),
            preParsed: BuildAcls("ChainB", "TexB.png"));
        _ctx.AppCommands.CaptureTabEditorState(tabB);

        // Switching back to tab A via the cache path must restore tab A's own known texture
        // size, not leave tab B's (null) behind.
        Assert.True(_ctx.AppCommands.TryActivateTabFromCache(tabA));

        using var stream = new MemoryStream();
        _ctx.ProjectManager.SaveAnimationChainList(stream);

        stream.Position = 0;
        var saved = AnimationChainListSave.FromString(new StreamReader(stream).ReadToEnd());
        Assert.Equal(TextureCoordinateType.Pixel, saved.CoordinateType);
        var frame = saved.AnimationChains[0].Frames[0];
        Assert.Equal(0f, frame.LeftCoordinate);
        Assert.Equal(64f, frame.RightCoordinate);
        Assert.Equal(0f, frame.TopCoordinate);
        Assert.Equal(64f, frame.BottomCoordinate);
    }
}
