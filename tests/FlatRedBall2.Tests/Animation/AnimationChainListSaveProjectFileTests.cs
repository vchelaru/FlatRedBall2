using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FlatRedBall2.Animation.Content;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// ProjectFile is a tooling-only field that points back at the .gluj/project file the .achx
// belongs to. The engine ignores it at runtime; it exists so AE can round-trip files without
// dropping the field. Real FRB1 .achx files carry it (see KidDefense corpus).
public class AnimationChainListSaveProjectFileTests
{
    [Fact]
    public void FromFile_NoProjectFileElement_LeavesProjectFileNull()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>X</Name>" +
            "    <Frame><TextureName>a.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
            "  </AnimationChain>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        save.ProjectFile.ShouldBeNull();
    }

    [Fact]
    public void FromFile_WithProjectFileElement_PreservesValue()
    {
        string xml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
            "<AnimationChainArraySave>" +
            "  <AnimationChain><Name>X</Name>" +
            "    <Frame><TextureName>a.png</TextureName><FrameLength>0.1</FrameLength></Frame>" +
            "  </AnimationChain>" +
            "  <ProjectFile>../../../game.gluj</ProjectFile>" +
            "</AnimationChainArraySave>";

        var save = AnimationChainListSave.FromFile("any.achx",
            _ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));

        save.ProjectFile.ShouldBe("../../../game.gluj");
    }

    [Fact]
    public void Save_NullProjectFile_OmitsElement()
    {
        var save = new AnimationChainListSave { ProjectFile = null };
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".achx");
        try
        {
            save.Save(tempPath);
            var doc = XDocument.Load(tempPath);
            doc.Descendants("ProjectFile").ShouldBeEmpty();
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }

    [Fact]
    public void Save_WithProjectFile_EmitsAsLastChildOfRoot()
    {
        var save = new AnimationChainListSave { ProjectFile = "../../../game.gluj" };
        save.AnimationChains.Add(new AnimationChainSave { Name = "X" });

        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".achx");
        try
        {
            save.Save(tempPath);
            var doc = XDocument.Load(tempPath);
            var last = doc.Root!.Elements().Last();
            last.Name.LocalName.ShouldBe("ProjectFile");
            last.Value.ShouldBe("../../../game.gluj");
        }
        finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
    }
}
