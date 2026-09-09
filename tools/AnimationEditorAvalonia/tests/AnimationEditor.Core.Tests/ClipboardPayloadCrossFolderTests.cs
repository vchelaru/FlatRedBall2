using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Cross-tab/cross-folder copy-paste (#1026): a frame's <c>TextureName</c> is stored relative to
/// its own <c>.achx</c> folder, so copying it as-is into a document that lives in a different
/// folder would resolve against the wrong base and silently break the reference.
/// <see cref="ClipboardPayload.SerializeFromPayload(CopySelectionPayload, string?)"/> resolves
/// <c>TextureName</c> to an absolute path at copy time so the same physical file is still found
/// regardless of which folder it ends up pasted into.
/// </summary>
public class ClipboardPayloadCrossFolderTests
{
    [Fact]
    public void SerializeFromPayload_Chain_WithSourceFolder_ResolvesEveryFrameTextureNameToAbsolute()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "a.png" });
        chain.Frames.Add(new AnimationFrameSave { TextureName = "b.png" });
        var payload = new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = new[] { chain } };
        string sourceFolder = TestPaths.AbsDir("Project", "Animations", "Player");

        var text = ClipboardPayload.SerializeFromPayload(payload, sourceFolder);
        ClipboardPayload.TryDeserialize(text, out var chains, out _, out _, out _);

        var expected = new[]
        {
            TexturePathHelper.ResolveDisplayPath("a.png", sourceFolder),
            TexturePathHelper.ResolveDisplayPath("b.png", sourceFolder),
        };
        Assert.Equal(expected, chains!.Single().Frames.Select(f => f.TextureName));
    }

    [Fact]
    public void SerializeFromPayload_Frame_WithSourceFolder_ResolvesRelativeTextureNameToAbsolute()
    {
        var frame = new AnimationFrameSave { TextureName = "sheet.png" };
        var payload = new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = new[] { frame } };
        string sourceFolder = TestPaths.AbsDir("Project", "Animations", "Player");

        var text = ClipboardPayload.SerializeFromPayload(payload, sourceFolder);
        ClipboardPayload.TryDeserialize(text, out _, out var frames, out _, out _);

        Assert.Equal(TexturePathHelper.ResolveDisplayPath("sheet.png", sourceFolder), frames!.Single().TextureName);
    }

    [Fact]
    public void SerializeFromPayload_Frame_WithoutSourceFolder_LeavesTextureNameUnchanged()
    {
        var frame = new AnimationFrameSave { TextureName = "sheet.png" };
        var payload = new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = new[] { frame } };

        var text = ClipboardPayload.SerializeFromPayload(payload);
        ClipboardPayload.TryDeserialize(text, out _, out var frames, out _, out _);

        Assert.Equal("sheet.png", frames!.Single().TextureName);
    }

    [Fact]
    public void SerializeFromPayload_Frame_AlreadyAbsoluteTextureName_UnaffectedBySourceFolder()
    {
        var absolutePath = TestPaths.Abs("Project", "Animations", "Enemy", "sheet.png");
        var frame = new AnimationFrameSave { TextureName = absolutePath };
        var payload = new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = new[] { frame } };

        var text = ClipboardPayload.SerializeFromPayload(payload, TestPaths.AbsDir("Project", "Animations", "Player"));
        ClipboardPayload.TryDeserialize(text, out _, out var frames, out _, out _);

        Assert.Equal(absolutePath, frames!.Single().TextureName);
    }
}
