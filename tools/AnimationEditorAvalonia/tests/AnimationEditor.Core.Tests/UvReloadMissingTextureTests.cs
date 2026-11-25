using AnimationEditor.DocumentMatrix;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class UvReloadMissingTextureTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();
    private readonly TestServices _ctx = new();

    public void Dispose() => _dir.Dispose();

    // A checkout writes the .achx and its texture one after the other.
    [Fact]
    public async Task ReloadAchxFromDisk_UvFileWhoseTextureIsThereOnlyLater_NextSaveKeepsTheWholeTextureFrame()
    {
        string achx = Documents.Write(_dir.Path, DocType.Achx, "hero", Documents.PlainChain("hero"));
        string png = Path.Combine(_dir.Path, "sheet.png");
        byte[] bytes = File.ReadAllBytes(png);
        await _ctx.AppCommands.OpenProjectWorkflowAsync(achx);
        var uv = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "sheet.png", FrameLength = 0.1f, RightCoordinate = 1, BottomCoordinate = 1 });
        uv.AnimationChains.Add(chain);
        uv.Save(achx);
        File.Delete(png);
        _ctx.AppCommands.ReloadAchxFromDisk(achx);
        File.WriteAllBytes(png, bytes);

        _ctx.AppCommands.AddAnimationChainWithName("Idle");

        var saved = AnimationChainListSave.FromFile(achx);
        saved.CoordinateType.ShouldBe(TextureCoordinateType.UV);
        saved.AnimationChains[0].Frames.Single().RightCoordinate.ShouldBe(1);
    }
}
