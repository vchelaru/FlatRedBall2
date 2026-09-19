using AnimationEditor.Core.Data;
using System.IO;
using System.Xml.Serialization;
using Xunit;

namespace AnimationEditor.Core.Tests;

[Collection("SequentialSingletons")]
public class AESettingsSaveRoundTripTests
{
    // ── Serialization helpers ─────────────────────────────────────────────────

    private static string Serialize(AESettingsSave s)
    {
        var xs = new XmlSerializer(typeof(AESettingsSave));
        using var sw = new StringWriter();
        xs.Serialize(sw, s);
        return sw.ToString();
    }

    private static AESettingsSave Deserialize(string xml)
    {
        var xs = new XmlSerializer(typeof(AESettingsSave));
        using var sr = new StringReader(xml);
        return (AESettingsSave)xs.Deserialize(sr)!;
    }

    // ── Guides ────────────────────────────────────────────────────────────────

    [Fact]
    public void HorizontalAndVertical_RoundTrip_StoredIndependently()
    {
        var s = new AESettingsSave();
        s.HorizontalGuides.Add(10f);
        s.VerticalGuides.Add(20f);

        var loaded = Deserialize(Serialize(s));

        Assert.Single(loaded.HorizontalGuides);
        Assert.Single(loaded.VerticalGuides);
        Assert.Equal(10f, loaded.HorizontalGuides[0]);
        Assert.Equal(20f, loaded.VerticalGuides[0]);
    }

    [Fact]
    public void HorizontalGuides_RoundTrip_PreservesValuesAndOrder()
    {
        var s = new AESettingsSave();
        s.HorizontalGuides.Add(10.5f);
        s.HorizontalGuides.Add(25.0f);
        s.HorizontalGuides.Add(100f);

        var loaded = Deserialize(Serialize(s));

        Assert.Equal(3,     loaded.HorizontalGuides.Count);
        Assert.Equal(10.5f, loaded.HorizontalGuides[0]);
        Assert.Equal(25.0f, loaded.HorizontalGuides[1]);
        Assert.Equal(100f,  loaded.HorizontalGuides[2]);
    }

    [Fact]
    public void VerticalGuides_RoundTrip_PreservesValuesAndOrder()
    {
        var s = new AESettingsSave();
        s.VerticalGuides.Add(50f);
        s.VerticalGuides.Add(75.25f);

        var loaded = Deserialize(Serialize(s));

        Assert.Equal(2,      loaded.VerticalGuides.Count);
        Assert.Equal(50f,    loaded.VerticalGuides[0]);
        Assert.Equal(75.25f, loaded.VerticalGuides[1]);
    }

    // ── Expanded nodes ────────────────────────────────────────────────────────

    [Fact]
    public void ExpandedNodes_RoundTrip_PreservesAllNames()
    {
        var s = new AESettingsSave();
        s.ExpandedNodes.Add("Walk");
        s.ExpandedNodes.Add("Run");
        s.ExpandedNodes.Add("Idle");

        var loaded = Deserialize(Serialize(s));

        Assert.Equal(3, loaded.ExpandedNodes.Count);
        Assert.Contains("Walk", loaded.ExpandedNodes);
        Assert.Contains("Run",  loaded.ExpandedNodes);
        Assert.Contains("Idle", loaded.ExpandedNodes);
    }

    [Fact]
    public void ExpandedNodes_RoundTrip_PreservesInsertionOrder()
    {
        var s = new AESettingsSave();
        s.ExpandedNodes.Add("C");
        s.ExpandedNodes.Add("A");
        s.ExpandedNodes.Add("B");

        var loaded = Deserialize(Serialize(s));

        Assert.Equal("C", loaded.ExpandedNodes[0]);
        Assert.Equal("A", loaded.ExpandedNodes[1]);
        Assert.Equal("B", loaded.ExpandedNodes[2]);
    }

    // ── Grid settings ─────────────────────────────────────────────────────────

    [Fact]
    public void GridSize_DefaultIs16()
    {
        Assert.Equal(16, new AESettingsSave().GridSize);
    }

    [Fact]
    public void GridSize_NonDefault_RoundTrip()
    {
        var s = new AESettingsSave { GridSize = 32 };
        Assert.Equal(32, Deserialize(Serialize(s)).GridSize);
    }

    [Fact]
    public void SnapToGrid_True_RoundTrip()
    {
        var s = new AESettingsSave { SnapToGrid = true };
        Assert.True(Deserialize(Serialize(s)).SnapToGrid);
    }

    // ── Empty round-trip ──────────────────────────────────────────────────────

    [Fact]
    public void EmptySettings_RoundTrip_AllCollectionsEmpty()
    {
        var loaded = Deserialize(Serialize(new AESettingsSave()));

        Assert.Empty(loaded.HorizontalGuides);
        Assert.Empty(loaded.VerticalGuides);
        Assert.Empty(loaded.ExpandedNodes);
    }

    // ── Zoom fields ───────────────────────────────────────────────────────────

    [Fact]
    public void WireframeZoomPercent_DefaultIs100()
    {
        Assert.Equal(100, new AESettingsSave().WireframeZoomPercent);
    }

    [Fact]
    public void PreviewZoomPercent_DefaultIs100()
    {
        Assert.Equal(100, new AESettingsSave().PreviewZoomPercent);
    }

    [Fact]
    public void WireframeZoomPercent_NonDefault_RoundTrip()
    {
        var s = new AESettingsSave { WireframeZoomPercent = 200 };
        Assert.Equal(200, Deserialize(Serialize(s)).WireframeZoomPercent);
    }

    [Fact]
    public void PreviewZoomPercent_NonDefault_RoundTrip()
    {
        var s = new AESettingsSave { PreviewZoomPercent = 150 };
        Assert.Equal(150, Deserialize(Serialize(s)).PreviewZoomPercent);
    }

    // ── Pan fields ────────────────────────────────────────────────────────────

    [Fact]
    public void WireframePanX_DefaultIsZero()
    {
        Assert.Equal(0f, new AESettingsSave().WireframePanX);
    }

    [Fact]
    public void WireframePanY_DefaultIsZero()
    {
        Assert.Equal(0f, new AESettingsSave().WireframePanY);
    }

    [Fact]
    public void PreviewPanX_DefaultIsZero()
    {
        Assert.Equal(0f, new AESettingsSave().PreviewPanX);
    }

    [Fact]
    public void PreviewPanY_DefaultIsZero()
    {
        Assert.Equal(0f, new AESettingsSave().PreviewPanY);
    }

    [Fact]
    public void PanFields_NonDefault_RoundTrip()
    {
        var s = new AESettingsSave
        {
            WireframePanX = 123.5f,
            WireframePanY = -45.0f,
            PreviewPanX   = 0.5f,
            PreviewPanY   = 99.9f,
        };

        var loaded = Deserialize(Serialize(s));

        Assert.Equal(123.5f, loaded.WireframePanX);
        Assert.Equal(-45.0f, loaded.WireframePanY);
        Assert.Equal(0.5f,   loaded.PreviewPanX);
        Assert.Equal(99.9f,  loaded.PreviewPanY);
    }

    // ── Combined ─────────────────────────────────────────────────────────────

    [Fact]
    public void AllFieldsPopulated_RoundTrip_IntegrityCheck()
    {
        var s = new AESettingsSave
        {
            SnapToGrid           = true,
            GridSize             = 8,
            OffsetMultiplier     = 2f,
            WireframeZoomPercent = 300,
            PreviewZoomPercent   = 50,
            WireframePanX        = 10f,
            WireframePanY        = -20f,
            PreviewPanX          = 5f,
            PreviewPanY          = 15f,
        };
        s.HorizontalGuides.Add(50f);
        s.VerticalGuides.Add(75f);
        s.ExpandedNodes.Add("Walk");

        var loaded = Deserialize(Serialize(s));

        Assert.True(loaded.SnapToGrid);
        Assert.Equal(8,    loaded.GridSize);
        Assert.Equal(300,  loaded.WireframeZoomPercent);
        Assert.Equal(50,   loaded.PreviewZoomPercent);
        Assert.Equal(10f,  loaded.WireframePanX);
        Assert.Equal(-20f, loaded.WireframePanY);
        Assert.Equal(5f,   loaded.PreviewPanX);
        Assert.Equal(15f,  loaded.PreviewPanY);
        Assert.Single(loaded.HorizontalGuides);
        Assert.Single(loaded.VerticalGuides);
        Assert.Single(loaded.ExpandedNodes);
    }
}
