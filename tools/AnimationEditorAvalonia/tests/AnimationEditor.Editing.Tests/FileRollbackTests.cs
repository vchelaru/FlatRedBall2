using AnimationEditor.Editing;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class FileRollbackTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("FileRollbackTests").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task ApplyAsync_FailingBatch_FileInUseCannotBeRestored_WarnsWhereItsOldContentIs()
    {
        var achx = Path.Combine(_dir, "hero.achx");
        new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel }.Save(achx);
        var texture = Path.Combine(_dir, "far.png");
        File.WriteAllText(texture, "old");
        var session = (await EditSession.OpenAsync(achx)).Session!;
        session.Rollback.Remember(texture);
        File.WriteAllText(texture, "new");

        EditRunResult run;
        using (new FileStream(texture, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            run = await EditOps.ApplyAsync(session, EditOps.Parse("""{"ops":[{"op":"delete-chain","chain":"Nope"}]}""").Ops!);

        run.Success.ShouldBeFalse();
        var warning = run.Result.Warnings.ShouldHaveSingleItem();
        warning.ShouldStartWith(texture);
        var saved = warning[(warning.IndexOf("Its old content is in ") + "Its old content is in ".Length)..].TrimEnd('.');
        File.ReadAllText(saved).ShouldBe("old");
        Directory.Delete(Path.GetDirectoryName(saved)!, recursive: true);
    }

    [Fact]
    public void Restore_PathRememberedTwice_GoesBackToTheFirstContent()
    {
        var path = Path.Combine(_dir, "far.png");
        File.WriteAllText(path, "first");
        var rollback = new FileRollback();
        rollback.Remember(path);
        File.WriteAllText(path, "second");
        rollback.Remember(path);
        File.WriteAllText(path, "third");

        rollback.Restore().ShouldBeEmpty();

        File.ReadAllText(path).ShouldBe("first");
    }
}
