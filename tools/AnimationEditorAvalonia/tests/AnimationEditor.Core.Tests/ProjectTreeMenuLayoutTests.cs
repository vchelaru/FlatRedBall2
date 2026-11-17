using AnimationEditor.Core.IO;
using Shouldly;
using System.Linq;
using Xunit;
using Cmd = AnimationEditor.Core.IO.ProjectTreeMenuCommand;

namespace AnimationEditor.Core.Tests;

/// <summary>Which commands the Animations tab's right-click menu offers, and how they group.</summary>
public class ProjectTreeMenuLayoutTests
{
    [Fact]
    public void Folder_GroupsNewItems_ThenConvert_ThenReveal()
    {
        var groups = ProjectTreeMenuLayout.ForFolder(hasConvertibleAchx: true);

        groups.Select(g => g.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.NewAnimationFile, Cmd.NewSequenceFile },
            new[] { Cmd.ConvertFolderToAchj },
            new[] { Cmd.RevealFolder, Cmd.CopyFullPath },
        });
    }

    [Fact]
    public void Folder_WithNothingToConvert_DropsTheConvertGroupEntirely()
    {
        var groups = ProjectTreeMenuLayout.ForFolder(hasConvertibleAchx: false);

        groups.Select(g => g.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.NewAnimationFile, Cmd.NewSequenceFile },
            new[] { Cmd.RevealFolder, Cmd.CopyFullPath },
        });
    }

    [Fact]
    public void AchxFile_SeparatesEditItems_LocationItems_AndDelete()
    {
        var groups = ProjectTreeMenuLayout.ForFile("hero.achx");

        groups.Select(g => g.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.Duplicate, Cmd.ConvertFileToAchj },
            new[] { Cmd.OpenContainingFolder, Cmd.CopyFullPath },
            new[] { Cmd.Delete },
        });
    }

    [Theory]
    [InlineData(true, Cmd.RevealFolder)]
    [InlineData(false, Cmd.OpenContainingFolder)]
    public void ImageRow_OffersTheAnimationsTabLocationGroup(bool isFolder, Cmd reveal) =>
        ProjectTreeMenuLayout.ForImageRow(isFolder).ToArray().ShouldBe(new[] { reveal, Cmd.CopyFullPath });

    [Fact]
    public void EveryCommand_HasAnIcon()
    {
        foreach (var command in System.Enum.GetValues<Cmd>())
            Should.NotThrow(() => ProjectTreeMenuLayout.Icon(command));
    }

    [Theory]
    [InlineData("hero.achj")]
    [InlineData("tiles.tsx")]
    [InlineData("boss.seqj")]
    public void NonAchxFile_HasNoConvertItem(string fileName)
    {
        var groups = ProjectTreeMenuLayout.ForFile(fileName);

        groups.Select(g => g.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.Duplicate },
            new[] { Cmd.OpenContainingFolder, Cmd.CopyFullPath },
            new[] { Cmd.Delete },
        });
    }
}
