using FlatRedBall2.AnimationEditorCommon;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace AnimationEditor.Core.IO;

// Thin wrapper around System.Xml.Serialization.XmlSerializer that replaces FRB1's
// FileManager.XmlSerialize/XmlDeserialize. Used by AE companion .aeproperties
// files — AOT is not a concern in that code path.
internal static class XmlFile
{
    public static void Serialize<T>(T obj, string path)
    {
        var serializer = new XmlSerializer(typeof(T));
        AtomicFile.Write(path, stream => serializer.Serialize(stream, obj));
    }

    public static T Deserialize<T>(string path)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var stream = File.OpenRead(path);
        return (T)serializer.Deserialize(stream)!;
    }
}
