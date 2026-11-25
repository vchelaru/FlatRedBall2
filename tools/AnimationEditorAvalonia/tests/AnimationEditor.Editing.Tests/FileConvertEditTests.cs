using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Editing.Tests;

public class FileConvertEditTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AnimationEditorEditingTests", Guid.NewGuid().ToString("N"));

    public FileConvertEditTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Write(string name)
    {
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel, TimeMeasurementUnit = TimeMeasurementUnit.Millisecond };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "hero.png", FrameLength = 250f, LeftCoordinate = 16, RightCoordinate = 32, TopCoordinate = 0, BottomCoordinate = 16 });
        acls.AnimationChains.Add(chain);
        var path = Path.Combine(_root, name);
        if (name.EndsWith(".achj")) acls.SaveJson(path); else acls.Save(path);
        return path;
    }

    [Fact]
    public void AchjToAchx_NameXmlCannotStore_FailsNamingTheCharacterAndWritesNothing()
    {
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Bad\u0001" });
        var src = Path.Combine(_root, "bad.achj");
        acls.SaveJson(src);

        var r = FileConvertEdit.Convert(src, "achx");

        r.Errors.ShouldBe(["Could not convert bad.achj: \"Bad\uFFFD\" contains the character U+0001, which XML cannot store."]);
        Directory.GetFiles(_root).ShouldBe([src]);
    }

    [Fact]
    public void AchxToAchj_WritesNewFileAndKeepsSource()
    {
        var src = Write("hero.achx");
        var before = File.ReadAllBytes(src);

        var r = FileConvertEdit.Convert(src, "achj");

        r.Success.ShouldBeTrue(string.Join(";", r.Errors));
        r.ChangedItems.ShouldBe([Path.Combine(_root, "hero.achj")]);
        File.ReadAllBytes(src).ShouldBe(before);
        var back = AnimationChainListSave.FromJsonFile(Path.Combine(_root, "hero.achj"));
        back.AnimationChains[0].Frames[0].FrameLength.ShouldBe(250f);
        back.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Millisecond);
    }

    [Fact]
    public void AchjToAchx_WritesNewFileAndKeepsSource()
    {
        var src = Write("hero.achj");
        var before = File.ReadAllBytes(src);

        var r = FileConvertEdit.Convert(src, "achx");

        r.Success.ShouldBeTrue(string.Join(";", r.Errors));
        r.ChangedItems.ShouldBe([Path.Combine(_root, "hero.achx")]);
        File.ReadAllBytes(src).ShouldBe(before);
        var back = AnimationChainListSave.FromFile(Path.Combine(_root, "hero.achx"));
        back.AnimationChains[0].Frames[0].RightCoordinate.ShouldBe(32f);
        back.TimeMeasurementUnit.ShouldBe(TimeMeasurementUnit.Millisecond);
    }

    [Fact]
    public void TargetExists_FailsAndLeavesItUntouched()
    {
        var src = Write("hero.achx");
        File.WriteAllText(Path.Combine(_root, "hero.achj"), "keep me");

        var r = FileConvertEdit.Convert(src, "achj");

        r.Success.ShouldBeFalse();
        r.HasError(EditErrorCode.AlreadyExists).ShouldBeTrue();
        File.ReadAllText(Path.Combine(_root, "hero.achj")).ShouldBe("keep me");
    }

    [Theory]
    [InlineData("hero.achx", "achx")]
    [InlineData("hero.achj", "achj")]
    public void SameFormat_Fails(string file, string to)
    {
        var src = Write(file);

        FileConvertEdit.Convert(src, to).Success.ShouldBeFalse();
    }

    [Fact]
    public void UnknownTarget_Fails()
    {
        FileConvertEdit.Convert(Write("hero.achx"), "png").Success.ShouldBeFalse();
    }

    [Fact]
    public void MissingFile_Fails()
    {
        FileConvertEdit.Convert(Path.Combine(_root, "nope.achx"), "achj").Success.ShouldBeFalse();
    }

    [Fact]
    public void UnreadableSource_FailsAndWritesNothing()
    {
        var src = Path.Combine(_root, "bad.achj");
        File.WriteAllText(src, "{ not json");

        var r = FileConvertEdit.Convert(src, "achx");

        r.Success.ShouldBeFalse();
        File.Exists(Path.Combine(_root, "bad.achx")).ShouldBeFalse();
    }
}
