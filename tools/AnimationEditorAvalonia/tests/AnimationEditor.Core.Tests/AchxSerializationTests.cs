using AnimationEditor.Core;
using FlatRedBall2.Animation.Content;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Full round-trip tests for .achx file serialization using
/// <see cref="AnimationChainListSave.FromFile"/> and
/// <see cref="AnimationChainListSave.Save"/>.
/// XML root element is <c>&lt;AnimationChainArraySave&gt;</c> (XmlType attribute on the class).
/// </summary>
[Collection("SequentialSingletons")]
public class AchxSerializationTests
{
    // ── Chain / Frame basics ──────────────────────────────────────────────────

    [Fact]
    public void SaveThenLoad_PreservesChainName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/test.achx";
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "WalkRight" });

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);

        Assert.Equal("WalkRight", loaded.AnimationChains[0].Name);
    }

    [Fact]
    public void SaveThenLoad_PreservesFrameTextureName()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/tex.achx";
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Idle" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "hero_sheet.png" });
        acls.AnimationChains.Add(chain);

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);

        Assert.Equal("hero_sheet.png", loaded.AnimationChains[0].Frames[0].TextureName);
    }

    [Fact]
    public void SaveThenLoad_PreservesFrameUvCoordinates()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/uv.achx";
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(new AnimationFrameSave
        {
            LeftCoordinate = 0.25f,
            RightCoordinate = 0.5f,
            TopCoordinate = 0.0f,
            BottomCoordinate = 0.5f
        });
        acls.AnimationChains.Add(chain);

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);
        var frame = loaded.AnimationChains[0].Frames[0];

        Assert.Equal(0.25f, frame.LeftCoordinate);
        Assert.Equal(0.5f, frame.RightCoordinate);
        Assert.Equal(0.0f, frame.TopCoordinate);
        Assert.Equal(0.5f, frame.BottomCoordinate);
    }

    [Fact]
    public void SaveThenLoad_PreservesFrameLength()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/fl.achx";
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Anim" };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.25f });
        acls.AnimationChains.Add(chain);

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);

        Assert.Equal(0.25f, loaded.AnimationChains[0].Frames[0].FrameLength);
    }

    // ── FlipHorizontal / FlipVertical ─────────────────────────────────────────

    [Fact]
    public void SaveThenLoad_WhenFlipHorizontalTrue_Preserved()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/flip.achx";
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Flip" };
        chain.Frames.Add(new AnimationFrameSave { FlipHorizontal = true });
        acls.AnimationChains.Add(chain);

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);

        Assert.True(loaded.AnimationChains[0].Frames[0].FlipHorizontal);
    }

    [Fact]
    public void SaveThenLoad_WhenFlipVerticalTrue_Preserved()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/flipV.achx";
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "FlipV" };
        chain.Frames.Add(new AnimationFrameSave { FlipVertical = true });
        acls.AnimationChains.Add(chain);

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);

        Assert.True(loaded.AnimationChains[0].Frames[0].FlipVertical);
    }

    // ── RelativeX / RelativeY ─────────────────────────────────────────────────

    [Fact]
    public void SaveThenLoad_PreservesNonZeroRelativeXY()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/rel.achx";
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Offset" };
        chain.Frames.Add(new AnimationFrameSave { RelativeX = 5f, RelativeY = -3f });
        acls.AnimationChains.Add(chain);

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);
        var frame = loaded.AnimationChains[0].Frames[0];

        Assert.Equal(5f, frame.RelativeX);
        Assert.Equal(-3f, frame.RelativeY);
    }

    // ── Shape data ────────────────────────────────────────────────────────────

    [Fact]
    public void SaveThenLoad_PreservesAxisAlignedRectangleInFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/rect.achx";
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "WithRect" };
        var frame = TestHelpers.MakeFrame();
        var rect = new AARectSave { Name = "HitBox", ScaleX = 8, ScaleY = 8, X = 2, Y = -1 };
        frame.ShapesSave!.Shapes.Add(rect);
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);
        var loadedRect = loaded.AnimationChains[0].Frames[0]
            .ShapesSave?.AARectSaves.First();

        Assert.NotNull(loadedRect);
        Assert.Equal("HitBox", loadedRect!.Name);
        Assert.Equal(8f, loadedRect.ScaleX);
        Assert.Equal(2f, loadedRect.X);
    }

    [Fact]
    public void SaveThenLoad_PreservesCircleInFrame()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/circle.achx";
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "WithCircle" };
        var frame = TestHelpers.MakeFrame();
        var circle = new CircleSave { Name = "AttackRange", Radius = 20, X = 1, Y = 2 };
        frame.ShapesSave!.Shapes.Add(circle);
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);
        var loadedCircle = loaded.AnimationChains[0].Frames[0]
            .ShapesSave?.CircleSaves.First();

        Assert.NotNull(loadedCircle);
        Assert.Equal("AttackRange", loadedCircle!.Name);
        Assert.Equal(20f, loadedCircle.Radius);
    }

    [Fact]
    public void Save_FrameWithNullShapes_OmitsShapeCollectionElement()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/noshapes.achx";
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Idle" };
        // Mirrors an old (FRB1-authored) frame loaded with no shape data.
        chain.Frames.Add(new AnimationFrameSave { TextureName = "hero.png", ShapesSave = null });
        acls.AnimationChains.Add(chain);

        acls.Save(path);
        var xml = File.ReadAllText(path);

        Assert.DoesNotContain("ShapeCollectionSave", xml);
    }

    [Fact]
    public void Save_FrameWithEmptyNonNullShapes_WritesEmptyWrapper()
    {
        // Presence is preserved: a non-null (but empty) ShapesSave means the source frame had a
        // <ShapeCollectionSave> element, so re-save must keep it. Some FRB1 files write an empty
        // wrapper for shapeless frames; dropping it would diff those files. Null is the omit case.
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/emptyshapes.achx";
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Idle" };
        chain.Frames.Add(TestHelpers.MakeFrame()); // non-null but zero shapes
        acls.AnimationChains.Add(chain);

        acls.Save(path);
        var xml = File.ReadAllText(path);

        Assert.Contains("<ShapeCollectionSave>", xml);
        Assert.Contains("<CircleSaves />", xml); // empty typed lists, no shape entries
    }

    [Fact]
    public void LoadThenSave_Frb1FileWithShapes_IsByteIdentical()
    {
        // FRB1-shaped .achx (16 chains, 40 frames, per-frame CircleSave shapes with
        // Z/Alpha/Red/Green/Blue), normalized to FRB2's canonical output: FRB1's typed shape
        // lists, element order, floats as shortest round-trippable text ("R"), no BOM. Opening
        // and re-saving must reproduce the file byte-for-byte (the writer is idempotent, #503).
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Frb1WeaponAnimations.achx");
        var outPath = dir.Path + "/out.achx";

        AnimationChainListSave.FromFile(fixturePath).Save(outPath);

        Assert.Equal(File.ReadAllBytes(fixturePath), File.ReadAllBytes(outPath));
    }

    [Fact]
    public void LoadThenSave_OldFrameWithNoShapeWrapper_DoesNotInjectShapeCollection()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/old.achx";

        // Hand-authored FRB1-era frame with no <ShapeCollectionSave> wrapper at all.
        var oldXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <FileRelativeTextures>true</FileRelativeTextures>
              <TimeMeasurementUnit>Second</TimeMeasurementUnit>
              <CoordinateType>Pixel</CoordinateType>
              <AnimationChain>
                <Name>Walk</Name>
                <Frame>
                  <TextureName>a.png</TextureName>
                  <FrameLength>0.1</FrameLength>
                  <LeftCoordinate>0</LeftCoordinate>
                  <RightCoordinate>1</RightCoordinate>
                  <TopCoordinate>0</TopCoordinate>
                  <BottomCoordinate>1</BottomCoordinate>
                </Frame>
              </AnimationChain>
            </AnimationChainArraySave>
            """;
        File.WriteAllText(path, oldXml);

        // Open-and-save with no edits should not inject the empty shapes wrapper.
        AnimationChainListSave.FromFile(path).Save(path);
        var resaved = File.ReadAllText(path);

        Assert.DoesNotContain("ShapeCollectionSave", resaved);
    }

    // ── Multiple chains ───────────────────────────────────────────────────────

    [Fact]
    public void SaveThenLoad_MultipleChains_AllPreserved()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/multi.achx";
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Idle" });
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Run" });
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Jump" });

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);

        Assert.Equal(3, loaded.AnimationChains.Count);
        Assert.Contains(loaded.AnimationChains, c => c.Name == "Idle");
        Assert.Contains(loaded.AnimationChains, c => c.Name == "Run");
        Assert.Contains(loaded.AnimationChains, c => c.Name == "Jump");
    }

    [Fact]
    public void SaveThenLoad_MultipleChains_OrderPreserved()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/order.achx";
        var acls = new AnimationChainListSave();
        var names = new[] { "Alpha", "Beta", "Gamma", "Delta" };
        foreach (var n in names)
            acls.AnimationChains.Add(new AnimationChainSave { Name = n });

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);

        for (int i = 0; i < names.Length; i++)
            Assert.Equal(names[i], loaded.AnimationChains[i].Name);
    }

    // ── Empty file ────────────────────────────────────────────────────────────

    [Fact]
    public void SaveThenLoad_EmptyAcls_LoadsWithNoChains()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/empty.achx";
        var acls = new AnimationChainListSave();

        acls.Save(path);
        var loaded = AnimationChainListSave.FromFile(path);

        Assert.Empty(loaded.AnimationChains);
    }

    // ── XML structure ─────────────────────────────────────────────────────────

    [Fact]
    public void Save_XmlRootElementIsAnimationChainArraySave()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/rootcheck.achx";
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Test" });

        acls.Save(path);
        var xml = File.ReadAllText(path);

        Assert.Contains("AnimationChainArraySave", xml);
    }

    [Fact]
    public void Save_EachChainSerializesAsAnimationChainElement()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        using var dir = new TestHelpers.TempDir();
        var path = dir.Path + "/chainelem.achx";
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(new AnimationChainSave { Name = "Run" });

        acls.Save(path);
        var xml = File.ReadAllText(path);

        // Per [XmlElementAttribute("AnimationChain")], chains serialize as <AnimationChain>
        Assert.Contains("<AnimationChain>", xml);
    }
}
