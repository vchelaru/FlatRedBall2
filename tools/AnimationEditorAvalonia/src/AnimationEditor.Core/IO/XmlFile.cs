using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace AnimationEditor.Core.IO;

// Thin wrapper around System.Xml.Serialization.XmlSerializer that replaces FRB1's
// FileManager.XmlSerialize/XmlDeserialize. Used by AE companion .aeproperties
// files and clipboard payloads — AOT is not a concern in those code paths.
internal static class XmlFile
{
    public static void Serialize<T>(T obj, string path)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var stream = File.Create(path);
        serializer.Serialize(stream, obj);
    }

    public static void SerializeToString<T>(T obj, out string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var writer = new StringWriter();
        serializer.Serialize(writer, obj);
        xml = writer.ToString();
    }

    public static T Deserialize<T>(string path)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var stream = File.OpenRead(path);
        return (T)serializer.Deserialize(stream)!;
    }

    public static T DeserializeFromString<T>(string xml)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var reader = new StringReader(xml);
        return (T)serializer.Deserialize(reader)!;
    }
}
