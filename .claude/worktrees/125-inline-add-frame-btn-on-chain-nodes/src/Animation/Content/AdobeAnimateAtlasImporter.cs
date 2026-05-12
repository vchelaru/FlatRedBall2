using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using XnaTitleContainer = Microsoft.Xna.Framework.TitleContainer;

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
/// Pivot attributes (<c>pivotX</c>, <c>pivotY</c>) are converted into per-frame
/// <see cref="AnimationFrame.RelativeX"/>/<see cref="AnimationFrame.RelativeY"/> so the pivot
/// pixel lands at the entity's origin — keeps a multi-size character (e.g. one with a "feet"
/// pivot) anchored across frames.
/// </remarks>
public class AdobeAnimateAtlasSave
{
    /// <summary>Relative path to the atlas PNG, as authored in the Adobe Animate export. Resolved against the atlas XML's directory at load time.</summary>
    public string ImagePath = string.Empty;

    /// <summary>All sub-texture entries from the atlas XML, in document order. Each one becomes a single <see cref="AnimationFrame"/> during <see cref="ToAnimationChainList"/>.</summary>
    public List<AdobeAnimateSubTexture> SubTextures = new();

    /// <summary>Absolute path of the source XML file, populated by <see cref="FromFile"/>. Used to resolve <see cref="ImagePath"/> relative to the atlas directory.</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>
    /// Loads an Adobe Animate TextureAtlas XML file via manual parsing (AOT-safe). Production
    /// code should prefer <c>ContentLoader.LoadAdobeAnimateAtlas(path, frameRate)</c>, which
    /// routes the read through the service's stream seam. This overload exists for tooling and
    /// tests that work without a <see cref="FlatRedBall2.ContentLoader"/>.
    /// </summary>
    /// <param name="filePath">Path to the atlas XML, relative to the title container.</param>
    /// <param name="streamProvider">Optional byte source. Defaults to <c>TitleContainer.OpenStream</c>.</param>
    public static AdobeAnimateAtlasSave FromFile(string filePath, Func<string, Stream>? streamProvider = null)
    {
        streamProvider ??= XnaTitleContainer.OpenStream;
        using var stream = streamProvider(filePath);
        var doc = XDocument.Load(stream);
        var root = doc.Root!;

        var result = new AdobeAnimateAtlasSave
        {
            ImagePath = (string?)root.Attribute("imagePath") ?? string.Empty
        };

        foreach (var subEl in root.Elements("SubTexture"))
        {
            var sub = new AdobeAnimateSubTexture
            {
                Name = (string?)subEl.Attribute("name") ?? string.Empty,
                X = (int)subEl.Attribute("x")!,
                Y = (int)subEl.Attribute("y")!,
                Width = (int)subEl.Attribute("width")!,
                Height = (int)subEl.Attribute("height")!,
            };

            var pivotXAttr = subEl.Attribute("pivotX");
            if (pivotXAttr != null)
                sub.PivotX = float.Parse(pivotXAttr.Value, CultureInfo.InvariantCulture);

            var pivotYAttr = subEl.Attribute("pivotY");
            if (pivotYAttr != null)
                sub.PivotY = float.Parse(pivotYAttr.Value, CultureInfo.InvariantCulture);

            result.SubTextures.Add(sub);
        }

        result.FileName = filePath;
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
    public AnimationChainList ToAnimationChainList(FlatRedBall2.ContentLoader contentManager, float frameRate = 30f)
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
                // Adobe pivot is in source-rect pixels from top-left (Y-down). Sprites render
                // with the source-rect center as the anchor and world Y+ up, so converting pivot
                // → RelativeX/Y so the pivot pixel lands at the entity origin:
                //   RelativeX = srcW/2 - pivotX        (X axes agree)
                //   RelativeY = pivotY - srcH/2        (Adobe Y-down → world Y-up flips sign)
                float relativeX = 0f;
                float relativeY = 0f;
                if (!float.IsNaN(sub.PivotX))
                    relativeX = sub.Width / 2f - sub.PivotX;
                if (!float.IsNaN(sub.PivotY))
                    relativeY = sub.PivotY - sub.Height / 2f;

                chain.Add(new AnimationFrame
                {
                    TextureName = ImagePath,
                    Texture = texture,
                    FrameLength = frameLength,
                    SourceRectangle = new Rectangle(sub.X, sub.Y, sub.Width, sub.Height),
                    RelativeX = relativeX,
                    RelativeY = relativeY,
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

/// <summary>
/// A single sub-texture (frame) within an <see cref="AdobeAnimateAtlasSave"/>. Parsed from one
/// <c>&lt;SubTexture&gt;</c> element in the atlas XML. Content-interchange model — not a runtime type.
/// </summary>
public class AdobeAnimateSubTexture
{
    /// <summary>Frame name as authored by Adobe Animate (e.g. <c>Eyeball_Idle0000</c>). Trailing digits are stripped to group frames into an <see cref="AnimationChain"/>.</summary>
    public string Name = string.Empty;
    /// <summary>Left edge of this frame within the atlas, in pixels.</summary>
    public int X;
    /// <summary>Top edge of this frame within the atlas, in pixels.</summary>
    public int Y;
    /// <summary>Width of this frame within the atlas, in pixels.</summary>
    public int Width;
    /// <summary>Height of this frame within the atlas, in pixels.</summary>
    public int Height;

    /// <summary>
    /// X pivot (origin) for this frame as authored in Adobe Animate, in source-rect pixels from the
    /// left, or <see cref="float.NaN"/> if unspecified. Converted into <see cref="AnimationFrame.RelativeX"/>
    /// at <see cref="AdobeAnimateAtlasSave.ToAnimationChainList"/> time.
    /// </summary>
    public float PivotX = float.NaN;
    /// <summary>
    /// Y pivot (origin) for this frame as authored in Adobe Animate, in source-rect pixels from the
    /// top (Y-down), or <see cref="float.NaN"/> if unspecified. Converted into
    /// <see cref="AnimationFrame.RelativeY"/> at <see cref="AdobeAnimateAtlasSave.ToAnimationChainList"/>
    /// time, with the sign flip from Adobe's Y-down to FRB2's world Y-up baked in.
    /// </summary>
    public float PivotY = float.NaN;
}
