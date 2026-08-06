using System.Collections.Generic;
using System.Text.Json;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace FlatRedBall2.Tests.Glue;

// Turns parsed NamedObjectSave data into real, configured FRB2 objects. This is the first phase
// whose output is visible, so "it exists, it is the right size, it is in the right place" is the bar.
public class GlueObjectBuilderTests
{
    private static NamedObjectSave Save(string json) =>
        JsonSerializer.Deserialize(json, GlueJsonContext.Default.NamedObjectSave)!;

    private static (GlueObjectBuilder Builder, List<GlueLoadDiagnostic> Diagnostics) NewBuilder()
    {
        var diagnostics = new List<GlueLoadDiagnostic>();
        return (new GlueObjectBuilder(diagnostics), diagnostics);
    }

    [Fact]
    public void Create_AbsolutePositionOnAnAttachedObject_BecomesTheOffset()
    {
        // Glue's own codegen emits `instance.CopyAbsoluteToRelative()` before attaching
        // (NamedObjectSaveCodeGenerator.cs:1148), and CopyAbsoluteToRelative is
        // `RelativePosition = Position` (PositionedObject.cs:1607). So an authored absolute X on an
        // attached object *becomes* the relative offset — X and RelativeX are the same thing here.
        // Dropping it would misplace real authored objects: DoorsDemo's player collision box and
        // every Beefball ScoreHud label are authored exactly this way.
        var (builder, diagnostics) = NewBuilder();
        var save = Save(@"{
            ""InstanceName"": ""Shape"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
            ""AttachToContainer"": true,
            ""InstructionSaves"": [ { ""Member"": ""X"", ""Value"": 500.0 } ]
        }");

        var circle = (Circle)builder.Create(save)!;

        circle.X.ShouldBe(500f);
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Create_Circle_ProducesARealCircle()
    {
        var (builder, _) = NewBuilder();
        var save = Save(@"{
            ""InstanceName"": ""CircleInstance"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle""
        }");

        builder.Create(save).ShouldBeOfType<Circle>();
    }

    [Fact]
    public void Create_ColorNamedAsAString_ConvertsToTheNamedColor()
    {
        var (builder, _) = NewBuilder();
        var save = Save(@"{
            ""InstanceName"": ""Shape"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
            ""InstructionSaves"": [ { ""Member"": ""Color"", ""Type"": ""Color"", ""Value"": ""Red"" } ]
        }");

        var circle = (Circle)builder.Create(save)!;

        circle.Color.ShouldBe(XnaColor.Red);
    }

    [Fact]
    public void Create_RadiusInstruction_IsApplied()
    {
        var (builder, _) = NewBuilder();
        var save = Save(@"{
            ""InstanceName"": ""CircleInstance"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
            ""InstructionSaves"": [ { ""Type"": ""float"", ""Member"": ""Radius"", ""Value"": 16.0 } ]
        }");

        var circle = (Circle)builder.Create(save)!;

        circle.Radius.ShouldBe(16f);
    }

    [Fact]
    public void Create_Shape_IsVisibleByDefault()
    {
        // FRB2 shapes default to invisible because they are primarily collision volumes. A shape
        // authored in Glue is meant to be seen, so leaving the engine default would make every
        // loaded project render nothing — the exact failure this phase exists to fix.
        var (builder, _) = NewBuilder();
        var save = Save(@"{
            ""InstanceName"": ""Shape"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.AxisAlignedRectangle""
        }");

        ((AARect)builder.Create(save)!).IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void Create_UnconvertibleValue_WarnsAndLeavesTheDefault()
    {
        var (builder, diagnostics) = NewBuilder();
        var save = Save(@"{
            ""InstanceName"": ""CircleInstance"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
            ""InstructionSaves"": [ { ""Member"": ""Radius"", ""Value"": ""not a number"" } ]
        }");

        var circle = (Circle)builder.Create(save)!;

        circle.Radius.ShouldBe(16f); // the engine default, untouched
        diagnostics.ShouldContain(d => d.Severity == GlueDiagnosticSeverity.Warning);
    }

    [Fact]
    public void Create_UnknownMemberName_WarnsAndDoesNotThrow()
    {
        var (builder, diagnostics) = NewBuilder();
        var save = Save(@"{
            ""InstanceName"": ""CircleInstance"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
            ""InstructionSaves"": [ { ""Member"": ""NoSuchMember"", ""Value"": 1.0 } ]
        }");

        builder.Create(save).ShouldNotBeNull();
        diagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Warning && d.Message.Contains("NoSuchMember"));
    }

    [Fact]
    public void Create_UnmappedType_ProducesNothingAndWarns()
    {
        var (builder, diagnostics) = NewBuilder();
        var save = Save(@"{
            ""InstanceName"": ""SolidCollision"",
            ""SourceClassType"": ""FlatRedBall.TileCollisions.TileShapeCollection""
        }");

        builder.Create(save).ShouldBeNull();
        diagnostics.ShouldContain(d => d.Message.Contains("TileShapeCollection"));
    }

    [Fact]
    public void Create_VisibleInstruction_MapsOntoIsVisible()
    {
        // Glue's member is "Visible"; FRB2's property is "IsVisible".
        var (builder, diagnostics) = NewBuilder();
        var save = Save(@"{
            ""InstanceName"": ""Shape"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
            ""InstructionSaves"": [ { ""Type"": ""bool"", ""Member"": ""Visible"", ""Value"": false } ]
        }");

        var circle = (Circle)builder.Create(save)!;

        circle.IsVisible.ShouldBeFalse();
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Create_WidthAndHeight_AreAppliedToARectangle()
    {
        var (builder, _) = NewBuilder();
        var save = Save(@"{
            ""InstanceName"": ""Rect"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.AxisAlignedRectangle"",
            ""InstructionSaves"": [
                { ""Type"": ""float"", ""Member"": ""Width"", ""Value"": 64.0 },
                { ""Type"": ""float"", ""Member"": ""Height"", ""Value"": 48.0 }
            ]
        }");

        var rect = (AARect)builder.Create(save)!;

        rect.Width.ShouldBe(64f);
        rect.Height.ShouldBe(48f);
    }

    [Fact]
    public void AddTo_AttachedObjectWithRelativeOffsets_ParentsAndOffsets()
    {
        // Glue's RelativeX/Y and FRB2's X/Y are the same thing: FRB2 treats X as an offset from
        // Parent whenever one is set, and exposes the world value as AbsoluteX.
        var (builder, _) = NewBuilder();
        var parent = new Entity { X = 100f, Y = 200f };
        var save = Save(@"{
            ""InstanceName"": ""CooldownCircle"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
            ""AttachToContainer"": true,
            ""InstructionSaves"": [
                { ""Type"": ""float"", ""Member"": ""RelativeX"", ""Value"": 10.0 },
                { ""Type"": ""float"", ""Member"": ""RelativeY"", ""Value"": -5.0 }
            ]
        }");

        var circle = (Circle)builder.AddTo(parent, save)!;

        circle.Parent.ShouldBe(parent);
        circle.X.ShouldBe(10f);
        circle.AbsoluteX.ShouldBe(110f);
        circle.AbsoluteY.ShouldBe(195f);
    }

    [Fact]
    public void AddTo_ShapeExcludedFromCollidable_IsAttachedButNotInDefaultCollision()
    {
        // Glue lets a shape ride along for position and rendering without joining the entity's
        // collision. FRB2's plain Add opts every shape in, so the flag has to be honoured.
        var (builder, _) = NewBuilder();
        var parent = new Entity();
        var save = Save(@"{
            ""InstanceName"": ""VisualOnly"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
            ""AttachToContainer"": true,
            ""IncludeInICollidable"": false
        }");

        var circle = (Circle)builder.AddTo(parent, save)!;

        circle.Parent.ShouldBe(parent);
        parent.Shapes.ShouldNotContain(circle);
    }

    [Fact]
    public void Create_PolygonWithNoPoints_WarnsThatItWillNotRender()
    {
        // A Polygon starts empty and its draw call bails below two points, so without this it would
        // be present, positioned, and invisible with nothing explaining why.
        var (builder, diagnostics) = NewBuilder();
        var save = Save(@"{
            ""InstanceName"": ""Poly"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.Polygon""
        }");

        builder.Create(save).ShouldNotBeNull();
        diagnostics.ShouldContain(d => d.Message.Contains("will not render"));
    }

    [Fact]
    public void AddTo_UnattachedObject_IsNotParented()
    {
        var (builder, _) = NewBuilder();
        var parent = new Entity { X = 100f };
        var save = Save(@"{
            ""InstanceName"": ""Free"",
            ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
            ""InstructionSaves"": [ { ""Type"": ""float"", ""Member"": ""X"", ""Value"": 7.0 } ]
        }");

        var circle = (Circle)builder.AddTo(parent, save)!;

        circle.Parent.ShouldBeNull();
        circle.AbsoluteX.ShouldBe(7f);
    }
}
