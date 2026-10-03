using System.Collections.Generic;
using System.Xml.Serialization;

namespace AnimationEditor.Core.Data
{
    public class TextureSettings
    {
    }

    /// <summary>A frame is identified by its chain's name plus its index in that chain; frames have no name of their own.</summary>
    public class ExpandedFrameSave
    {
        public string ChainName { get; set; } = string.Empty;
        public int FrameIndex { get; set; }
    }

    public class AESettingsSave
    {
        public float OffsetMultiplier = 1;

        [XmlElement("HorizontalGuide")]
        public List<float> HorizontalGuides = new List<float>();

        [XmlElement("VerticalGuide")]
        public List<float> VerticalGuides = new List<float>();

        [XmlElement("Texture")]
        public List<TextureSettings> TextureSettings = new List<TextureSettings>();

        [XmlElement("ExpandedNode")]
        public List<string> ExpandedNodes { get; set; } = new List<string>();

        /// <summary>Frame nodes (those with shape children) the user left expanded.</summary>
        [XmlElement("ExpandedFrame")]
        public List<ExpandedFrameSave> ExpandedFrames { get; set; } = new List<ExpandedFrameSave>();

        public bool SnapToGrid { get; set; }
        public int GridSize { get; set; } = 16;
        public int WireframeZoomPercent { get; set; } = 100;
        public int PreviewZoomPercent { get; set; } = 100;
        public float WireframePanX { get; set; }
        public float WireframePanY { get; set; }
        public float PreviewPanX { get; set; }
        public float PreviewPanY { get; set; }
    }
}
