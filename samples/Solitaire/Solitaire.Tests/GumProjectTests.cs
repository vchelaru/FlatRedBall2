using System;
using System.IO;
using System.Linq;
using Gum.DataTypes;
using Gum.DataTypes.Variables;
using Shouldly;
using Xunit;

namespace Solitaire.Tests;

public class GumProjectTests
{
    private static GumProjectSave LoadProject()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Solitaire.Common")))
            dir = dir.Parent;
        dir.ShouldNotBeNull();
        var path = Path.Combine(dir.FullName, "Solitaire.Common", "Content", "GumProject", "GumProject.gumj");
        return GumProjectSave.Load(path);
    }

    // Dashed strokes make Apos.Shapes compile its Dashes technique on the first frame (#1564).
    [Fact]
    public void GumProject_ComponentsAndScreens_DoNotSetStrokeDash()
    {
        var project = LoadProject();
        ElementSave[] elements = [.. project.Components, .. project.Screens];

        var dashed = elements
            .SelectMany(e => e.States.Concat(e.Categories.SelectMany(c => c.States))
                .SelectMany(s => s.Variables)
                .Select(v => (Element: e.Name, v.Name, v.Value)))
            .Where(v => v.Name.EndsWith("StrokeDashLength") || v.Name.EndsWith("StrokeGapLength"))
            .Where(v => v.Value is float f && f > 0)
            .Select(v => $"{v.Element}: {v.Name}")
            .ToList();

        dashed.ShouldBeEmpty();
    }
}
