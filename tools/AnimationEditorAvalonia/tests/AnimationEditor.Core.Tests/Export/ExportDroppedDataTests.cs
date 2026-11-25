using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.Core.Export;
using AnimationEditor.SaveModel;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests.Export;

/// <summary>Every stored member of a chain, frame, event or shape type is either in the exported file or named in a warning.</summary>
public class ExportDroppedDataTests
{
    private static readonly Dictionary<string, ExportFormat> Formats = new()
    {
        ["PixiJS"] = ExportFormat.PixiJs,
        ["Godot"] = ExportFormat.Godot,
    };

    // What each exported file stores. A member that is not here must be reported when it is set.
    private static readonly Dictionary<string, string[]> Kept = new()
    {
        ["PixiJS"] =
        [
            "AnimationChainSave.Name", "AnimationChainSave.Frames", "AnimationFrameSave.TextureName",
            "AnimationFrameSave.LeftCoordinate", "AnimationFrameSave.RightCoordinate",
            "AnimationFrameSave.TopCoordinate", "AnimationFrameSave.BottomCoordinate",
        ],
        ["Godot"] =
        [
            "AnimationChainSave.Name", "AnimationChainSave.Frames", "AnimationChainSave.Loop",
            "AnimationFrameSave.TextureName", "AnimationFrameSave.FrameLength",
            "AnimationFrameSave.LeftCoordinate", "AnimationFrameSave.RightCoordinate",
            "AnimationFrameSave.TopCoordinate", "AnimationFrameSave.BottomCoordinate",
        ],
    };

    private const string EditorState = "AnimationChainSave.IsLocked"; // accepted as lost (#2002)

    private static AnimationChainSave OneFrameChain()
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", RightCoordinate = 16f, BottomCoordinate = 16f });
        return chain;
    }

    private static ExportResult Export(string format, params AnimationChainSave[] chains)
    {
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        acls.AnimationChains.AddRange(chains);
        return Formats[format].Export(acls, _ => null);
    }

    public static TheoryData<string, string, string> StoredMembers()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var format in Formats.Keys)
        {
            foreach (var type in new[] { typeof(AnimationChainSave), typeof(AnimationFrameSave), typeof(AnimationFrameEvent) })
                foreach (var member in SaveModelFill.StoredMembers(type))
                    data.Add(format, type.Name, member.Name);
            foreach (var shapeType in SaveModelFill.ShapeTypes)
                data.Add(format, shapeType.Name, "");
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(StoredMembers))]
    public void Export_StoredMemberNonDefault_IsInTheFileOrReported(string format, string typeName, string memberName)
    {
        int counter = 100;
        var chain = OneFrameChain();
        var frame = chain.Frames[0];
        object owner = chain;
        if (typeName == nameof(AnimationFrameSave))
            owner = frame;
        else if (typeName == nameof(AnimationFrameEvent))
            frame.Events.Add((AnimationFrameEvent)(owner = new AnimationFrameEvent()));
        else if (SaveModelFill.ShapeTypes.FirstOrDefault(t => t.Name == typeName) is { } shapeType)
            (frame.ShapesSave = new ShapesSave()).Add(SaveModelFill.CreateFilled(shapeType, ref counter));
        if (memberName != "")
            SaveModelFill.SetNonDefault(owner, SaveModelFill.StoredMembers(owner.GetType()).Single(m => m.Name == memberName), ref counter);

        var before = Export(format, OneFrameChain());
        var after = Export(format, chain);

        string member = $"{typeName}.{memberName}";
        bool kept = Kept[format].Contains(member);
        (after.Text != before.Text).ShouldBe(kept, $"{member}: Kept must list exactly what the {format} export stores");
        if (!kept && member != EditorState)
            after.Warnings.ShouldNotBe(before.Warnings, $"{member} is lost in a {format} export: report it, or add it to Kept if the file stores it");
    }

    [Theory]
    [InlineData("PixiJS", "flip, sprite offset, color, collision shape, event, keyframe, frame length, loop off, ping-pong, reverse can't be stored in a PixiJS spritesheet and was not exported.")]
    [InlineData("Godot", "flip, sprite offset, color, collision shape, event, keyframe, ping-pong, reverse can't be stored in a Godot SpriteFrames and was not exported. Flip the AnimatedSprite2D (flip_h/flip_v) instead.")]
    public void Export_EverythingNonDefaultInTwoChains_NamesEachDroppedKindOnce(string format, string expected)
    {
        int counter = 100;
        var first = (AnimationChainSave)SaveModelFill.CreateFilled(typeof(AnimationChainSave), ref counter);
        var second = (AnimationChainSave)SaveModelFill.CreateFilled(typeof(AnimationChainSave), ref counter);
        second.Frames[0].TextureName = first.Frames[0].TextureName;

        Export(format, first, second).Warnings.ShouldBe([expected]);
    }

    [Fact]
    public void Export_PixiJsDuplicateChainNames_WarnsAboutTheRename()
    {
        Export("PixiJS", OneFrameChain(), OneFrameChain()).Warnings
            .ShouldBe(["Another animation is already named 'Walk', so this one was exported as 'Walk_2'."]);
    }
}
