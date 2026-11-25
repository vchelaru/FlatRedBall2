using AnimationEditor.Core.IO;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Every stored value survives a copy and paste, and a duplicate, including one added to the format later.</summary>
public class ClipboardTravelTests
{
    private static readonly FrameLengthUnit Seconds = new(TimeMeasurementUnit.Second);

    private static string Dump(object item) =>
        JsonSerializer.Serialize(item, item.GetType(), new JsonSerializerOptions { IncludeFields = true, WriteIndented = true });

    private static int _next = 3;

    // Gives every public field and settable property a value that is not its default.
    private static T Fill<T>(T item) where T : notnull
    {
        foreach (var member in item.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance))
        {
            var (type, get, set) = member switch
            {
                FieldInfo f => (f.FieldType, (Func<object?>)(() => f.GetValue(item)), (Action<object?>)(v => f.SetValue(item, v))),
                PropertyInfo { CanWrite: true, SetMethod.IsPublic: true } p => (p.PropertyType, () => p.GetValue(item), v => p.SetValue(item, v)),
                _ => (typeof(void), () => null, _ => { }),
            };
            type = Nullable.GetUnderlyingType(type) ?? type;
            _next++;
            if (type == typeof(string)) set(member.Name.EndsWith("Interpolation") ? "Linear" : member.Name.EndsWith("Easing") ? "QuadraticIn" : member.Name + _next);
            else if (type == typeof(float)) set(_next + 0.5f);
            else if (type == typeof(int)) set(_next);
            else if (type == typeof(bool)) set(!(get() as bool? ?? false));
            else if (type.IsEnum) set(Enum.GetValues(type).GetValue(1));
        }
        return item;
    }

    private static AnimationFrameSave FullFrame()
    {
        var frame = Fill(new AnimationFrameSave());
        frame.TextureName = "C:/art/sheet.png";
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Add(Fill(new AARectSave()));
        frame.ShapesSave.Add(Fill(new CircleSave()));
        var polygon = Fill(new PolygonSave());
        polygon.Points = [Fill(new Vector2Save()), Fill(new Vector2Save()), Fill(new Vector2Save())];
        frame.ShapesSave.Add(polygon);
        frame.Events.Add(Fill(new AnimationFrameEvent()));
        frame.Events.Add(new AnimationFrameEvent { Name = "NoData" });
        return frame;
    }

    private static AnimationChainSave FullChain()
    {
        var chain = Fill(new AnimationChainSave());
        chain.Frames = [FullFrame(), FullFrame()];
        return chain;
    }

    private static (List<AnimationChainSave>? Chains, List<AnimationFrameSave>? Frames, List<object>? Shapes) Paste(CopySelectionPayload payload)
    {
        string text = ClipboardPayload.SerializeFromPayload(payload, Seconds);
        ClipboardPayload.TryDeserialize(text, Seconds, _ => (64, 64), out var chains, out var frames, out var shapes).ShouldBeTrue();
        return (chains, frames, shapes);
    }

    [Fact]
    public void Chain_EveryField_TravelsThroughTheClipboardAndDuplicate()
    {
        var chain = FullChain();

        var pasted = Paste(new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = [chain] }).Chains!.Single();

        Dump(pasted).ShouldBe(Dump(chain));
        Dump(AnimationCloneHelper.CloneChain(chain)).ShouldBe(Dump(chain));
    }

    [Fact]
    public void Frame_EveryField_TravelsThroughTheClipboard()
    {
        var frame = FullFrame();

        var pasted = Paste(new CopySelectionPayload { Kind = CopySelectionKind.Frame, Frames = [frame] }).Frames!.Single();

        Dump(pasted).ShouldBe(Dump(frame));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Shape_EveryField_TravelsThroughTheClipboard_AloneAndWithOthers(int index)
    {
        var shapes = FullFrame().ShapesSave!.Shapes;

        Dump(Paste(new CopySelectionPayload { Kind = CopySelectionKind.Shape, Shapes = [shapes[index]] }).Shapes!.Single()).ShouldBe(Dump(shapes[index]));
        Dump(Paste(new CopySelectionPayload { Kind = CopySelectionKind.Shape, Shapes = shapes }).Shapes![index]).ShouldBe(Dump(shapes[index]));
    }

    [Theory]
    [InlineData("<a & b> ]]> \"q\" 'x'")]
    [InlineData(" lead and trail ")]
    [InlineData("a\nb")]
    [InlineData("a\tb")]
    [InlineData("\U0001F600 日本")]
    [InlineData("a:b")]
    public void Text_TravelsUnchanged(string text)
    {
        var frame = new AnimationFrameSave { TextureName = "s.png" };
        frame.Events.Add(new AnimationFrameEvent { Name = text, Data = text });
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Add(new AARectSave { Name = text });
        var chain = new AnimationChainSave { Name = text, Frames = [frame] };

        var pasted = Paste(new CopySelectionPayload { Kind = CopySelectionKind.Chain, Chains = [chain] }).Chains!.Single();

        Dump(pasted).ShouldBe(Dump(chain));
        Dump(Paste(new CopySelectionPayload { Kind = CopySelectionKind.Shape, Shapes = frame.ShapesSave.Shapes }).Shapes!.Single()).ShouldBe(Dump(frame.ShapesSave.Shapes[0]));
    }

    [Theory]
    [InlineData("List<AnimationChainSave>:")]
    [InlineData("List<AnimationChainSave>:<tileset/>")]
    [InlineData("List<AnimationChainSave>:{\"a\":1}")]
    [InlineData("List<AnimationChainSave>:null")]
    [InlineData("List<AnimationFrameSave>:<AnimationChainArraySave><AnimationChain><Frame><FrameLength>abc</FrameLength></Frame></AnimationChain></AnimationChainArraySave>")]
    [InlineData("ShapesSave:<AnimationChainArraySave/>")]
    [InlineData("AARectSave:<CircleSave/>")]
    [InlineData("CircleSave:")]
    [InlineData("https://example.com")]
    public void MalformedText_PastesNothing(string text)
    {
        ClipboardPayload.TryDeserialize(text, Seconds, _ => (64, 64), out var chains, out var frames, out var shapes);

        ((chains?.Count ?? 0) + (frames?.Count ?? 0) + (shapes?.Count ?? 0)).ShouldBe(0);
    }
}
