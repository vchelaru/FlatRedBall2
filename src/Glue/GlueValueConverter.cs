using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace FlatRedBall2.Glue;

/// <summary>
/// Converts a raw Glue JSON value into a target CLR type. Glue stores values untyped, so the target
/// property's declared type is what drives the conversion.
/// </summary>
internal static class GlueValueConverter
{
    private static readonly Dictionary<string, XnaColor> NamedColors = BuildNamedColors();

    /// <summary>
    /// Converts an authored <c>CustomVariable</c> value, honouring the variable's declared
    /// overriding type and named converter before handing off to <see cref="TryConvert"/>.
    /// </summary>
    /// <remarks>
    /// When a variable declares an <c>OverridingPropertyType</c>, the stored value is in
    /// <em>that</em> type rather than in the target member's, and a converter bridges the two —
    /// Beefball's score labels store an <c>int</c> for a <c>string</c> member. Converting straight
    /// to the target type would either fail or, worse, format differently than the author chose.
    /// </remarks>
    internal static bool TryConvertVariable(
        JsonElement value,
        string? overridingType,
        string? converterName,
        Type targetType,
        out object? converted)
    {
        Type? overriding = ResolveGlueTypeName(overridingType);

        // No overriding type, or it already matches the target: nothing to bridge.
        if (overriding is null || overriding == (Nullable.GetUnderlyingType(targetType) ?? targetType))
            return TryConvert(value, targetType, out converted);

        converted = null;

        if (!TryConvert(value, overriding, out object? authored) || authored is null)
            return false;

        return TryApplyConverter(authored, converterName, targetType, out converted);
    }

    /// <summary>Maps Glue's own type spelling onto a CLR type, for the subset that can be bridged.</summary>
    private static Type? ResolveGlueTypeName(string? glueTypeName) => glueTypeName switch
    {
        "int" or "Int32" or "System.Int32" => typeof(int),
        "long" or "Int64" or "System.Int64" => typeof(long),
        "float" or "Single" or "System.Single" => typeof(float),
        "double" or "Double" or "System.Double" => typeof(double),
        "decimal" or "Decimal" or "System.Decimal" => typeof(decimal),
        "byte" or "Byte" or "System.Byte" => typeof(byte),
        "bool" or "Boolean" or "System.Boolean" => typeof(bool),
        "string" or "String" or "System.String" => typeof(string),
        _ => null,
    };

    /// <summary>
    /// Applies one of Glue's named converters. Only the two that appear in real projects are
    /// implemented; anything else fails so the caller reports it rather than guessing a format.
    /// </summary>
    private static bool TryApplyConverter(
        object authored, string? converterName, Type targetType, out object? converted)
    {
        converted = null;
        Type target = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Glue ships four converters and only these two appear in real projects. An unrecognised
        // name must fail rather than fall through to default formatting: "Minutes:Seconds" would
        // otherwise silently render 125 as "125" instead of "2:05", which reads as working.
        if (converterName is not (null or "" or "<default>" or "Comma Separating"))
            return false;

        // "Comma Separating" is a display format, so it only makes sense heading into a string.
        bool commaSeparating = converterName == "Comma Separating";

        if (target == typeof(string))
        {
            converted = authored switch
            {
                IFormattable f when commaSeparating => f.ToString("n0", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => authored.ToString(),
            };
            return true;
        }

        if (authored is string text)
        {
            try
            {
                converted = Convert.ChangeType(text, target, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
            {
                return false;
            }
        }

        try
        {
            converted = Convert.ChangeType(authored, target, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    /// <summary>Attempts to convert <paramref name="value"/> to <paramref name="targetType"/>.</summary>
    /// <returns>False if the value cannot represent that type; the caller reports it.</returns>
    public static bool TryConvert(JsonElement value, Type targetType, out object? converted)
    {
        converted = null;
        Type target = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (target == typeof(string))
        {
            if (value.ValueKind != JsonValueKind.String)
                return false;

            converted = value.GetString();
            return true;
        }

        if (target == typeof(bool))
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.True: converted = true; return true;
                case JsonValueKind.False: converted = false; return true;
                default: return false;
            }
        }

        if (target == typeof(XnaColor))
            return TryConvertColor(value, out converted);

        if (target.IsEnum)
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int enumValue))
            {
                converted = Enum.ToObject(target, enumValue);
                return true;
            }

            // Some Glue values arrive as the member's name rather than its number.
            if (value.ValueKind == JsonValueKind.String)
                return Enum.TryParse(target, value.GetString(), ignoreCase: true, out converted);

            return false;
        }

        if (value.ValueKind != JsonValueKind.Number)
            return false;

        if (target == typeof(float) && value.TryGetSingle(out float f)) { converted = f; return true; }
        if (target == typeof(double) && value.TryGetDouble(out double d)) { converted = d; return true; }
        if (target == typeof(int) && value.TryGetInt32(out int i)) { converted = i; return true; }
        if (target == typeof(long) && value.TryGetInt64(out long l)) { converted = l; return true; }
        if (target == typeof(decimal) && value.TryGetDecimal(out decimal m)) { converted = m; return true; }
        if (target == typeof(byte) && value.TryGetByte(out byte b)) { converted = b; return true; }

        return false;
    }

    /// <summary>
    /// Glue writes colours as a name (<c>"White"</c>), and occasionally as a packed integer. FRB2 has
    /// no named-colour lookup of its own, so this reads XNA's own static colour properties.
    /// </summary>
    private static bool TryConvertColor(JsonElement value, out object? converted)
    {
        converted = null;

        if (value.ValueKind == JsonValueKind.String)
        {
            string? name = value.GetString();
            if (name is not null && NamedColors.TryGetValue(name, out var namedColor))
            {
                converted = namedColor;
                return true;
            }

            // "#RRGGBB" / "#RRGGBBAA"
            if (name is not null && name.StartsWith('#') && TryParseHexColor(name, out var hexColor))
            {
                converted = hexColor;
                return true;
            }

            return false;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out uint packed))
        {
            converted = new XnaColor(
                (byte)(packed & 0xFF),
                (byte)((packed >> 8) & 0xFF),
                (byte)((packed >> 16) & 0xFF),
                (byte)((packed >> 24) & 0xFF));
            return true;
        }

        return false;
    }

    private static bool TryParseHexColor(string text, out XnaColor color)
    {
        color = default;
        ReadOnlySpan<char> digits = text.AsSpan(1);

        if (digits.Length is not (6 or 8))
            return false;

        if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint raw))
            return false;

        if (digits.Length == 6)
        {
            color = new XnaColor((byte)(raw >> 16), (byte)(raw >> 8), (byte)raw);
            return true;
        }

        color = new XnaColor((byte)(raw >> 24), (byte)(raw >> 16), (byte)(raw >> 8), (byte)raw);
        return true;
    }

    /// <summary>
    /// Every colour name Glue can author, as data rather than reflection.
    /// </summary>
    /// <remarks>
    /// Reading these off <c>Color</c>'s static properties would be far shorter, but reflection over
    /// a type in another assembly is not trim-safe, and rooting it by attribute fails too: the
    /// backing assembly is <c>MonoGame.Framework</c> on desktop and an <c>nkast.Xna.Framework</c>
    /// assembly on the web target, so no single <c>DynamicDependency</c> resolves on both. Under
    /// trimming the table would come back empty and every named colour would silently fall back to
    /// the engine default — a failure visible only after publish.
    /// <para>The list is the XNA standard set, which both backends implement. Backend-specific names
    /// are deliberately excluded: one that exists on only one of them breaks that target's build,
    /// and Glue never writes them. A name outside this list converts to nothing and warns, which is
    /// loud and fixable — extend the list rather than reaching back for reflection.</para>
    /// </remarks>
    private static Dictionary<string, XnaColor> BuildNamedColors() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["AliceBlue"] = XnaColor.AliceBlue,
            ["AntiqueWhite"] = XnaColor.AntiqueWhite,
            ["Aqua"] = XnaColor.Aqua,
            ["Aquamarine"] = XnaColor.Aquamarine,
            ["Azure"] = XnaColor.Azure,
            ["Beige"] = XnaColor.Beige,
            ["Bisque"] = XnaColor.Bisque,
            ["Black"] = XnaColor.Black,
            ["BlanchedAlmond"] = XnaColor.BlanchedAlmond,
            ["Blue"] = XnaColor.Blue,
            ["BlueViolet"] = XnaColor.BlueViolet,
            ["Brown"] = XnaColor.Brown,
            ["BurlyWood"] = XnaColor.BurlyWood,
            ["CadetBlue"] = XnaColor.CadetBlue,
            ["Chartreuse"] = XnaColor.Chartreuse,
            ["Chocolate"] = XnaColor.Chocolate,
            ["Coral"] = XnaColor.Coral,
            ["CornflowerBlue"] = XnaColor.CornflowerBlue,
            ["Cornsilk"] = XnaColor.Cornsilk,
            ["Crimson"] = XnaColor.Crimson,
            ["Cyan"] = XnaColor.Cyan,
            ["DarkBlue"] = XnaColor.DarkBlue,
            ["DarkCyan"] = XnaColor.DarkCyan,
            ["DarkGoldenrod"] = XnaColor.DarkGoldenrod,
            ["DarkGray"] = XnaColor.DarkGray,
            ["DarkGreen"] = XnaColor.DarkGreen,
            ["DarkKhaki"] = XnaColor.DarkKhaki,
            ["DarkMagenta"] = XnaColor.DarkMagenta,
            ["DarkOliveGreen"] = XnaColor.DarkOliveGreen,
            ["DarkOrange"] = XnaColor.DarkOrange,
            ["DarkOrchid"] = XnaColor.DarkOrchid,
            ["DarkRed"] = XnaColor.DarkRed,
            ["DarkSalmon"] = XnaColor.DarkSalmon,
            ["DarkSeaGreen"] = XnaColor.DarkSeaGreen,
            ["DarkSlateBlue"] = XnaColor.DarkSlateBlue,
            ["DarkSlateGray"] = XnaColor.DarkSlateGray,
            ["DarkTurquoise"] = XnaColor.DarkTurquoise,
            ["DarkViolet"] = XnaColor.DarkViolet,
            ["DeepPink"] = XnaColor.DeepPink,
            ["DeepSkyBlue"] = XnaColor.DeepSkyBlue,
            ["DimGray"] = XnaColor.DimGray,
            ["DodgerBlue"] = XnaColor.DodgerBlue,
            ["Firebrick"] = XnaColor.Firebrick,
            ["FloralWhite"] = XnaColor.FloralWhite,
            ["ForestGreen"] = XnaColor.ForestGreen,
            ["Fuchsia"] = XnaColor.Fuchsia,
            ["Gainsboro"] = XnaColor.Gainsboro,
            ["GhostWhite"] = XnaColor.GhostWhite,
            ["Gold"] = XnaColor.Gold,
            ["Goldenrod"] = XnaColor.Goldenrod,
            ["Gray"] = XnaColor.Gray,
            // Glue's picker uses the XNA spelling; the British one is a courtesy for hand-edited files.
            ["Grey"] = XnaColor.Gray,
            ["Green"] = XnaColor.Green,
            ["GreenYellow"] = XnaColor.GreenYellow,
            ["Honeydew"] = XnaColor.Honeydew,
            ["HotPink"] = XnaColor.HotPink,
            ["IndianRed"] = XnaColor.IndianRed,
            ["Indigo"] = XnaColor.Indigo,
            ["Ivory"] = XnaColor.Ivory,
            ["Khaki"] = XnaColor.Khaki,
            ["Lavender"] = XnaColor.Lavender,
            ["LavenderBlush"] = XnaColor.LavenderBlush,
            ["LawnGreen"] = XnaColor.LawnGreen,
            ["LemonChiffon"] = XnaColor.LemonChiffon,
            ["LightBlue"] = XnaColor.LightBlue,
            ["LightCoral"] = XnaColor.LightCoral,
            ["LightCyan"] = XnaColor.LightCyan,
            ["LightGoldenrodYellow"] = XnaColor.LightGoldenrodYellow,
            ["LightGray"] = XnaColor.LightGray,
            ["LightGreen"] = XnaColor.LightGreen,
            ["LightPink"] = XnaColor.LightPink,
            ["LightSalmon"] = XnaColor.LightSalmon,
            ["LightSeaGreen"] = XnaColor.LightSeaGreen,
            ["LightSkyBlue"] = XnaColor.LightSkyBlue,
            ["LightSlateGray"] = XnaColor.LightSlateGray,
            ["LightSteelBlue"] = XnaColor.LightSteelBlue,
            ["LightYellow"] = XnaColor.LightYellow,
            ["Lime"] = XnaColor.Lime,
            ["LimeGreen"] = XnaColor.LimeGreen,
            ["Linen"] = XnaColor.Linen,
            ["Magenta"] = XnaColor.Magenta,
            ["Maroon"] = XnaColor.Maroon,
            ["MediumAquamarine"] = XnaColor.MediumAquamarine,
            ["MediumBlue"] = XnaColor.MediumBlue,
            ["MediumOrchid"] = XnaColor.MediumOrchid,
            ["MediumPurple"] = XnaColor.MediumPurple,
            ["MediumSeaGreen"] = XnaColor.MediumSeaGreen,
            ["MediumSlateBlue"] = XnaColor.MediumSlateBlue,
            ["MediumSpringGreen"] = XnaColor.MediumSpringGreen,
            ["MediumTurquoise"] = XnaColor.MediumTurquoise,
            ["MediumVioletRed"] = XnaColor.MediumVioletRed,
            ["MidnightBlue"] = XnaColor.MidnightBlue,
            ["MintCream"] = XnaColor.MintCream,
            ["MistyRose"] = XnaColor.MistyRose,
            ["Moccasin"] = XnaColor.Moccasin,
            ["NavajoWhite"] = XnaColor.NavajoWhite,
            ["Navy"] = XnaColor.Navy,
            ["OldLace"] = XnaColor.OldLace,
            ["Olive"] = XnaColor.Olive,
            ["OliveDrab"] = XnaColor.OliveDrab,
            ["Orange"] = XnaColor.Orange,
            ["OrangeRed"] = XnaColor.OrangeRed,
            ["Orchid"] = XnaColor.Orchid,
            ["PaleGoldenrod"] = XnaColor.PaleGoldenrod,
            ["PaleGreen"] = XnaColor.PaleGreen,
            ["PaleTurquoise"] = XnaColor.PaleTurquoise,
            ["PaleVioletRed"] = XnaColor.PaleVioletRed,
            ["PapayaWhip"] = XnaColor.PapayaWhip,
            ["PeachPuff"] = XnaColor.PeachPuff,
            ["Peru"] = XnaColor.Peru,
            ["Pink"] = XnaColor.Pink,
            ["Plum"] = XnaColor.Plum,
            ["PowderBlue"] = XnaColor.PowderBlue,
            ["Purple"] = XnaColor.Purple,
            ["Red"] = XnaColor.Red,
            ["RosyBrown"] = XnaColor.RosyBrown,
            ["RoyalBlue"] = XnaColor.RoyalBlue,
            ["SaddleBrown"] = XnaColor.SaddleBrown,
            ["Salmon"] = XnaColor.Salmon,
            ["SandyBrown"] = XnaColor.SandyBrown,
            ["SeaGreen"] = XnaColor.SeaGreen,
            ["SeaShell"] = XnaColor.SeaShell,
            ["Sienna"] = XnaColor.Sienna,
            ["Silver"] = XnaColor.Silver,
            ["SkyBlue"] = XnaColor.SkyBlue,
            ["SlateBlue"] = XnaColor.SlateBlue,
            ["SlateGray"] = XnaColor.SlateGray,
            ["Snow"] = XnaColor.Snow,
            ["SpringGreen"] = XnaColor.SpringGreen,
            ["SteelBlue"] = XnaColor.SteelBlue,
            ["Tan"] = XnaColor.Tan,
            ["Teal"] = XnaColor.Teal,
            ["Thistle"] = XnaColor.Thistle,
            ["Tomato"] = XnaColor.Tomato,
            ["Transparent"] = XnaColor.Transparent,
            ["TransparentBlack"] = XnaColor.Transparent,
            ["Turquoise"] = XnaColor.Turquoise,
            ["Violet"] = XnaColor.Violet,
            ["Wheat"] = XnaColor.Wheat,
            ["White"] = XnaColor.White,
            ["WhiteSmoke"] = XnaColor.WhiteSmoke,
            ["Yellow"] = XnaColor.Yellow,
            ["YellowGreen"] = XnaColor.YellowGreen,
        };
}
