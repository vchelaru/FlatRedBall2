using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

/// <summary>
/// Fresh-eyes pass #17 (plan/1147-tsx-sync-hardening/phase-01-bug-sweep.md): a Tiled tile
/// animation holds only a rect sequence and durations. Everything else an achx frame can carry
/// (flips, offsets, color, collision shapes) and a non-looping chain are dropped on a native-tsx
/// save without a word -- and several UI paths still let a user create them in a tsx project
/// (frame context menu "Add Rectangle"/"Add Circle", "Duplicate flipped", the Loop toggle, paste).
/// </summary>
public class TsxLossyDataCheckTests
{
    private static AnimationChainListSave WithChain(AnimationChainSave chain)
    {
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);
        return acls;
    }

    private static AnimationChainSave CleanChain(string name = "Walk")
    {
        var chain = new AnimationChainSave { Name = name };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f, RightCoordinate = 16f, BottomCoordinate = 16f });
        return chain;
    }

    [Fact]
    public void Warnings_CleanChain_ReturnsNone()
    {
        Assert.Empty(TsxLossyDataCheck.Warnings(WithChain(CleanChain())));
    }

    public static TheoryData<string, Action<AnimationFrameSave>, string> FrameEdits => new()
    {
        { "flip horizontal", f => f.FlipHorizontal = true, "flip" },
        { "flip vertical", f => f.FlipVertical = true, "flip" },
        { "flip diagonal", f => f.FlipDiagonal = true, "flip" },
        { "relative x", f => f.RelativeX = 3f, "offset" },
        { "relative y", f => f.RelativeY = -2f, "offset" },
        { "red", f => f.Red = 255, "color" },
        { "alpha", f => f.Alpha = 128, "color" },
        { "color operation", f => f.ColorOperation = FlatRedBall2.Animation.ColorOperation.Add, "color" },
        { "rectangle shape", f => { f.ShapesSave = new ShapesSave(); f.ShapesSave.Add(new AARectSave { Name = "Hit" }); }, "shape" },
        { "event", f => f.Events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = "Footstep" }), "event" },
        { "circle shape", f => { f.ShapesSave = new ShapesSave(); f.ShapesSave.Add(new CircleSave { Name = "Hurt" }); }, "shape" },
    };

    [Theory]
    [MemberData(nameof(FrameEdits))]
    public void Warnings_FrameCarriesAchxOnlyData_NamesChainAndKind(string _, Action<AnimationFrameSave> edit, string expectedKind)
    {
        var chain = CleanChain("Walk");
        edit(chain.Frames[0]);

        var warning = Assert.Single(TsxLossyDataCheck.Warnings(WithChain(chain)));

        Assert.Contains("\"Walk\"", warning);
        Assert.Contains(expectedKind, warning);
    }

    // What a tsx keeps. Everything else a chain, frame or event stores must be reported when it is set, because a cut
    // pasted into a tsx removes its source unless something is reported.
    private static readonly string[] KeptByATsx =
    [
        "AnimationChainSave.Name", "AnimationChainSave.Frames",
        "AnimationChainSave.IsLocked", // editor state, accepted as lost (#2002)
        "AnimationFrameSave.TextureName", "AnimationFrameSave.FrameLength",
        "AnimationFrameSave.LeftCoordinate", "AnimationFrameSave.RightCoordinate",
        "AnimationFrameSave.TopCoordinate", "AnimationFrameSave.BottomCoordinate",
    ];

    public static TheoryData<string, string> StoredMembersATsxDrops()
    {
        var data = new TheoryData<string, string>();
        foreach (var type in new[] { typeof(AnimationChainSave), typeof(AnimationFrameSave), typeof(FlatRedBall2.Animation.AnimationFrameEvent) })
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
                if (member is FieldInfo or PropertyInfo && !KeptByATsx.Contains($"{type.Name}.{member.Name}"))
                    data.Add(type.Name, member.Name);
        return data;
    }

    [Theory]
    [MemberData(nameof(StoredMembersATsxDrops))]
    public void Warnings_AnyStoredMemberATsxDrops_IsReported(string typeName, string memberName)
    {
        var chain = CleanChain();
        var frameEvent = new FlatRedBall2.Animation.AnimationFrameEvent();
        object owner = typeName switch
        {
            nameof(AnimationChainSave) => chain,
            nameof(AnimationFrameSave) => chain.Frames[0],
            _ => frameEvent,
        };
        if (owner == frameEvent) chain.Frames[0].Events.Add(frameEvent);

        SetToNonDefault(owner, memberName);

        TsxLossyDataCheck.Warnings(WithChain(chain)).Count.ShouldBe(1,
            $"{typeName}.{memberName} is lost in a .tsx save: report it in TsxLossyDataCheck, or add it to KeptByATsx if the save keeps it");
    }

    private static void SetToNonDefault(object owner, string memberName)
    {
        var member = owner.GetType().GetMember(memberName, BindingFlags.Public | BindingFlags.Instance).Single();
        var field = member as FieldInfo;
        var property = member as PropertyInfo;
        var type = field?.FieldType ?? property!.PropertyType;
        var current = field != null ? field.GetValue(owner) : property!.GetValue(owner);
        if (current is List<FlatRedBall2.Animation.AnimationFrameEvent> events)
        {
            events.Add(new FlatRedBall2.Animation.AnimationFrameEvent { Name = "Footstep" });
            return;
        }
        var shapes = new ShapesSave();
        shapes.Add(new CircleSave { Name = "Hit" });
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        object value =
            underlying == typeof(bool) ? !(bool)current!
            : underlying == typeof(int) ? 5
            : underlying == typeof(float) ? 3f
            : underlying == typeof(string) ? "Linear"
            : underlying.IsEnum ? Enum.GetValues(underlying).GetValue(1)!
            : underlying == typeof(ShapesSave) ? shapes
            : throw new NotSupportedException($"Give {memberName} ({type.Name}) a non-default value here");
        if (field != null) field.SetValue(owner, value);
        else property!.SetValue(owner, value);
    }

    [Theory]
    [InlineData(0.0004f, TimeMeasurementUnit.Second, 1)]
    [InlineData(0.4f, TimeMeasurementUnit.Millisecond, 1)]
    [InlineData(0.0167f, TimeMeasurementUnit.Second, 0)] // rounding to a whole millisecond is accepted
    [InlineData(0f, TimeMeasurementUnit.Second, 0)]
    public void Warnings_FrameLengthThatRoundsToZeroMilliseconds_IsReported(float length, TimeMeasurementUnit unit, int expected)
    {
        var acls = WithChain(CleanChain());
        acls.TimeMeasurementUnit = unit;
        acls.AnimationChains[0].Frames[0].FrameLength = length;

        TsxLossyDataCheck.Warnings(acls).Count.ShouldBe(expected);
    }

    [Fact]
    public void Warnings_EmptyShapesSave_ReturnsNone()
    {
        var chain = CleanChain();
        chain.Frames[0].ShapesSave = new ShapesSave();

        Assert.Empty(TsxLossyDataCheck.Warnings(WithChain(chain)));
    }

    [Fact]
    public void Warnings_NonLoopingChain_NamesChainAndLoop()
    {
        var chain = CleanChain("Die");
        chain.Loop = false;

        var warning = Assert.Single(TsxLossyDataCheck.Warnings(WithChain(chain)));

        Assert.Contains("\"Die\"", warning);
        Assert.Contains("loop", warning);
    }

    [Fact]
    public void Warnings_PingPongOrReverseChain_NamesThem()
    {
        var chain = CleanChain("Bob");
        chain.PingPong = true;
        chain.Reverse = true;

        var warning = Assert.Single(TsxLossyDataCheck.Warnings(WithChain(chain)));

        Assert.Contains("ping-pong", warning);
        Assert.Contains("reverse", warning);
    }

    // One warning per chain, however many frames or kinds of data it carries.
    [Fact]
    public void Warnings_SeveralKindsAcrossFrames_OneWarningPerChain()
    {
        var chain = CleanChain("Walk");
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png", FrameLength = 0.1f, RightCoordinate = 16f, BottomCoordinate = 16f });
        chain.Frames[0].FlipHorizontal = true;
        chain.Frames[1].RelativeX = 1f;
        chain.Loop = false;

        var warning = Assert.Single(TsxLossyDataCheck.Warnings(WithChain(chain)));

        Assert.Contains("flip", warning);
        Assert.Contains("offset", warning);
        Assert.Contains("loop", warning);
    }
}
