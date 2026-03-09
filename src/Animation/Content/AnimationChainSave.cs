using System.Collections.Generic;
using System.Xml.Serialization;

namespace FlatRedBall2.Animation.Content;

[System.Serializable]
public class AnimationChainSave
{
    public string Name = string.Empty;

    [XmlElement("Frame")]
    public List<AnimationFrameSave> Frames = new();
}
