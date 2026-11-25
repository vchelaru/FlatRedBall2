using System.Linq;
using System.Xml;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>Whether a name or other text can be stored in an XML animation file (.achx, .tsx).</summary>
internal static class StorableText
{
    /// <summary>
    /// Why <paramref name="text"/> cannot be stored, naming its first bad character as <c>U+0001</c>; null when it can.
    /// Control characters other than tab and line breaks, half a surrogate pair, U+FFFE and U+FFFF cannot, not even escaped.
    /// </summary>
    public static string? Problem(string? text)
    {
        for (int i = 0; i < text?.Length; i++)
        {
            char c = text[i];
            if (XmlConvert.IsXmlChar(c)) continue;
            if (i + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[i + 1], c))
            {
                i++;
                continue;
            }
            // EnumerateRunes already turns half a surrogate pair into U+FFFD.
            string shown = string.Concat(text.EnumerateRunes().Select(r => r.IsBmp && !XmlConvert.IsXmlChar((char)r.Value) ? "\uFFFD" : r.ToString()));
            return $"\"{shown}\" contains the character U+{(int)c:X4}, which XML cannot store.";
        }
        return null;
    }
}
