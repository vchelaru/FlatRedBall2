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
            new[] { Cmd.RevealFolder },
        });
    }

    [Fact]
    public void Folder_WithNothingToConvert_DropsTheConvertGroupEntirely()
    {
        var groups = ProjectTreeMenuLayout.ForFolder(hasConvertibleAchx: false);

        groups.Select(g => g.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.NewAnimationFile, Cmd.NewSequenceFile },
            new[] { Cmd.RevealFolder },
        });
    }

    [Fact]
    public void AchxFile_SeparatesLocationItems_EditItems_AndDelete()
    {
        var groups = ProjectTreeMenuLayout.ForFile("hero.achx");

        groups.Select(g => g.ToArray()).ShouldBe(new[]
        {
            new[] { Cmd.OpenContainingFolder, Cmd.CopyFullPath },
            new[] { Cmd.Duplicate, Cmd.ConvertFileToAchj },
            new[] { Cmd.Delete },
        });
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
            new[] { Cmd.OpenContainingFolder, Cmd.CopyFullPath },
            new[] { Cmd.Duplicate },
            new[] { Cmd.Delete },
        });
    }
}
