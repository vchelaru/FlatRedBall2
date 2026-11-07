using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests.Sequences;

/// <summary>
/// A <see cref="SequenceDocument"/> reads its source files once; what it shows must follow the files, so the preview
/// never draws a chain the source no longer has.
/// </summary>
public class SequenceSourceReloadTests
{
    private static void WriteSource(string folder, string chainName, float frameLength = 0.1f)
    {
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png", FrameLength = frameLength });
        list.AnimationChains.Add(chain);
        string path = Path.Combine(folder, "hero.achj");
        list.SaveJson(path);
        // Two writes inside one timestamp tick must still look different.
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(nextStamp++));
    }

    private static int nextStamp = 1;

    private static SequenceDocument OpenDocument(string folder, bool withSource = true)
    {
        var save = new SequenceListSave();
        if (withSource) save.Sources.Add("hero.achj");
        save.Sequences.Add(new SequenceSave { Name = "Attack" });
        string path = Path.Combine(folder, "boss.seqj");
        save.Save(path);
        return SequenceDocument.Load(path);
    }

    [Fact]
    public void ReloadChangedSources_SourceEditedOnDisk_ReloadsItAndRaisesChanged()
    {
        using var dir = new TestHelpers.TempDir();
        WriteSource(dir.Path, "Walk");
        var document = OpenDocument(dir.Path);
        int raised = 0;
        document.Changed += () => raised++;

        WriteSource(dir.Path, "Run");
        bool changed = document.ReloadChangedSources();

        changed.ShouldBeTrue();
        raised.ShouldBe(1);
        document.SourceChains("hero.achj")!.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Run" });
    }

    [Fact]
    public void ReloadChangedSources_NothingChanged_DoesNothing()
    {
        using var dir = new TestHelpers.TempDir();
        WriteSource(dir.Path, "Walk");
        var document = OpenDocument(dir.Path);
        int raised = 0;
        document.Changed += () => raised++;

        document.ReloadChangedSources().ShouldBeFalse();

        raised.ShouldBe(0);
    }

    [Fact]
    public void ReloadChangedSources_MissingSourceThatAppears_LoadsIt()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenDocument(dir.Path);
        document.IsSourceMissing("hero.achj").ShouldBeTrue();

        WriteSource(dir.Path, "Walk");

        document.ReloadChangedSources().ShouldBeTrue();
        document.IsSourceMissing("hero.achj").ShouldBeFalse();
    }

    [Fact]
    public void ReloadChangedSources_SourceThatVanishes_IsFlaggedMissing()
    {
        using var dir = new TestHelpers.TempDir();
        WriteSource(dir.Path, "Walk");
        var document = OpenDocument(dir.Path);

        File.Delete(Path.Combine(dir.Path, "hero.achj"));

        document.ReloadChangedSources().ShouldBeTrue();
        document.IsSourceMissing("hero.achj").ShouldBeTrue();
    }

    [Fact]
    public void AnyEdit_PicksUpASourceChangedOnDisk()
    {
        using var dir = new TestHelpers.TempDir();
        WriteSource(dir.Path, "Walk");
        var document = OpenDocument(dir.Path);

        WriteSource(dir.Path, "Run");
        document.AddSequence(new UndoManager());

        document.SourceChains("hero.achj")!.AnimationChains.Select(chain => chain.Name).ShouldBe(new[] { "Run" });
    }

    [Fact]
    public void RemovingASourceAndAddingItBack_ReadsTheFileAgain()
    {
        using var dir = new TestHelpers.TempDir();
        var document = OpenDocument(dir.Path);
        var undo = new UndoManager();
        document.IsSourceMissing("hero.achj").ShouldBeTrue();
        document.RemoveSource(undo, "hero.achj");

        WriteSource(dir.Path, "Walk");
        document.AddSources(undo, new[] { Path.Combine(dir.Path, "hero.achj") });

        document.IsSourceMissing("hero.achj").ShouldBeFalse();
    }
}
