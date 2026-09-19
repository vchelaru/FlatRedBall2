using AnimationEditor.Core;
using System;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class ProjectManagerTsxValidationIssuesTests : IDisposable
{
    private readonly TestHelpers.TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    // 4 columns, 16x16 tiles. Tile 8 is the anchor of a 2-tile group; tile 9's second frame
    // (14) is hand-edited out of lockstep with the anchor's second frame (12), which should
    // be column 1 of that row (13), not 14.
    private const string InconsistentGroupFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="64" columns="4">
         <image source="Heroes.png" width="64" height="256"/>
         <tile id="8">
          <animation>
           <frame tileid="8" duration="150"/>
           <frame tileid="12" duration="150"/>
          </animation>
         </tile>
         <tile id="9">
          <properties>
           <property name="ParentId" type="int" value="8"/>
          </properties>
          <animation>
           <frame tileid="9" duration="150"/>
           <frame tileid="14" duration="150"/>
          </animation>
         </tile>
        </tileset>
        """;

    private const string ConsistentFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    private string WriteFixture(string xml, string fileName)
    {
        var path = Path.Combine(_dir.Path, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    [Fact]
    public void GetChainNamesWithTsxIssues_InconsistentGroup_ReturnsAnchorChainName()
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(WriteFixture(InconsistentGroupFixtureXml, "Heroes.tsx")));

        var issueChainNames = pm.GetChainNamesWithTsxIssues();

        Assert.Contains("ID:8", issueChainNames);
    }

    [Fact]
    public void GetChainNamesWithTsxIssues_ConsistentProject_ReturnsEmpty()
    {
        var pm = new ProjectManager();
        pm.LoadTsxProject(new FilePath(WriteFixture(ConsistentFixtureXml, "Heroes.tsx")));

        var issueChainNames = pm.GetChainNamesWithTsxIssues();

        Assert.Empty(issueChainNames);
    }

    [Fact]
    public void GetChainNamesWithTsxIssues_NotATsxProject_ReturnsEmpty()
    {
        var pm = new ProjectManager();

        Assert.Empty(pm.GetChainNamesWithTsxIssues());
    }
}
