using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Animation.Content;

/// <summary>
/// Deserialized Adobe Animate TextureAtlas XML. Produced by Adobe Animate's
/// "Generate sprite sheet" export, paired with a single atlas PNG.
/// </summary>
/// <remarks>
/// The format has no per-frame duration, so <see cref="ToAnimationChainList"/>
/// takes a frame rate. SubTextures are grouped into <see cref="AnimationChain"/>s
/// by stripping the trailing digit block from each name: <c>Eyeball_Idle0000</c>
/// and <c>Eyeball_Idle0001</c> both belong to chain <c>Eyeball_Idle</c>.
/// Pivot attributes (<c>pivotX</c>, <c>pivotY</c>) are parsed but not yet applied —
/// <see cref="AnimationFrame"/> has no per-frame pivot/origin field yet.
/// </remarks>
[XmlRoot("TextureAtlas")]
public class AdobeAnimateAtlasSave
{
    [XmlAttribute("imagePath")]
    public string ImagePath = string.Empty;

    [XmlElement("SubTexture")]
    public List<AdobeAnimateSubTexture> SubTextures = new();

    [XmlIgnore]
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Deserializes an Adobe Animate TextureAtlas XML file from disk.</summary>
    public static AdobeAnimateAtlasSave FromFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var serializer = new XmlSerializer(typeof(AdobeAnimateAtlasSave));
        var result = (AdobeAnimateAtlasSave)serializer.Deserialize(stream)!;
        result.FileName = Path.GetFullPath(filePath);
        return result;
    }

    /// <summary>
    /// Converts this atlas into a runtime <see cref="AnimationChainList"/>. The atlas
    /// PNG is loaded once via <paramref name="contentManager"/> and shared by every frame.
    /// SubTextures are grouped into chains by name prefix (trailing digits stripped),
    /// and each chain's frames are sorted by name so <c>0000, 0001, 0002...</c> play in order.
    /// </summary>
    /// <param name="contentManager">Content manager used to load the atlas texture.</param>
    /// <param name="frameRate">Frames per second applied to every frame. Defaults to 30.</param>
    public AnimationChainList ToAnimationChainList(FlatRedBall2.ContentManagerService contentManager, float frameRate = 30f)
    {
        string atlasDir = string.IsNullOrEmpty(FileName) ? "" : Path.GetDirectoryName(FileName) ?? "";
        string texPath = !string.IsNullOrEmpty(atlasDir)
            ? Path.Combine(atlasDir, ImagePath)
            : ImagePath;

        Texture2D? texture = contentManager.Load<Texture2D>(texPath.Replace('\\', '/'));
        return BuildList(texture, frameRate);
    }

    // Internal hook so tests can build the chain list without loading a real texture.
    internal AnimationChainList BuildList(Texture2D? texture, float frameRate)
    {
        if (frameRate <= 0f)
            throw new ArgumentOutOfRangeException(nameof(frameRate), "frameRate must be positive.");

        TimeSpan frameLength = TimeSpan.FromSeconds(1.0 / frameRate);
        var list = new AnimationChainList { Name = FileName };
        var chainsByName = new Dictionary<string, List<AdobeAnimateSubTexture>>(StringComparer.Ordinal);
        var chainOrder = new List<string>();

        foreach (var sub in SubTextures)
        {
            string chainName = StripTrailingDigits(sub.Name);
            if (!chainsByName.TryGetValue(chainName, out var bucket))
            {
                bucket = new List<AdobeAnimateSubTexture>();
                chainsByName.Add(chainName, bucket);
                chainOrder.Add(chainName);
            }
            bucket.Add(sub);
        }

        foreach (var chainName in chainOrder)
        {
            var bucket = chainsByName[chainName];
            bucket.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            var chain = new AnimationChain { Name = chainName };
            foreach (var sub in bucket)
            {
                chain.Add(new AnimationFrame
                {
                    TextureName = ImagePath,
                    Texture = texture,
                    FrameLength = frameLength,
                    SourceRectangle = new Rectangle(sub.X, sub.Y, sub.Width, sub.Height),
                });
            }
            list.Add(chain);
        }

        return list;
    }

    private static string StripTrailingDigits(string name)
    {
        int end = name.Length;
        while (end > 0 && char.IsDigit(name[end - 1])) end--;
        return end == name.Length ? name : name.Substring(0, end);
    }
}

public class AdobeAnimateSubTexture
{
    [XmlAttribute("name")] public string Name = string.Empty;
    [XmlAttribute("x")] public int X;
    [XmlAttribute("y")] public int Y;
    [XmlAttribute("width")] public int Width;
    [XmlAttribute("height")] public int Height;

    // Pivot attributes are optional per frame; NaN indicates "not specified."
    // Parsed but not yet applied — AnimationFrame has no pivot field yet.
    [XmlIgnore] public float PivotX = float.NaN;
    [XmlIgnore] public float PivotY = float.NaN;

    [XmlAttribute("pivotX")]
    public string PivotXText
    {
        get => float.IsNaN(PivotX) ? "" : PivotX.ToString(CultureInfo.InvariantCulture);
        set => PivotX = string.IsNullOrEmpty(value) ? float.NaN : float.Parse(value, CultureInfo.InvariantCulture);
    }

    [XmlAttribute("pivotY")]
    public string PivotYText
    {
        get => float.IsNaN(PivotY) ? "" : PivotY.ToString(CultureInfo.InvariantCulture);
        set => PivotY = string.IsNullOrEmpty(value) ? float.NaN : float.Parse(value, CultureInfo.InvariantCulture);
    }
}
