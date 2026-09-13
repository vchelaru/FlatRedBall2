using System.IO;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Packaging;

public class TemplateNamespaceParameterTests
{
    [Theory]
    [InlineData("templates/frb2-desktop/.template.config/template.json")]
    [InlineData("templates/frb2-multiplatform/.template.config/template.json")]
    public void TemplateJson_NamespaceSymbol_CoalescesFromProjectNameWhenOmitted(string relativeTemplateJsonPath)
    {
        var json = File.ReadAllText(Path.Combine(RepoRoot, relativeTemplateJsonPath));
        using var document = JsonDocument.Parse(json);
        var symbols = document.RootElement.GetProperty("symbols");

        symbols.GetProperty("namespace").GetProperty("type").GetString().ShouldBe("parameter");

        var replacer = symbols.GetProperty("namespaceReplacer");
        replacer.GetProperty("generator").GetString().ShouldBe("coalesce");
        replacer.GetProperty("replaces").GetString().ShouldBe("GameNamespace");
        replacer.GetProperty("parameters").GetProperty("sourceVariableName").GetString().ShouldBe("namespace");
        replacer.GetProperty("parameters").GetProperty("fallbackVariableName").GetString().ShouldBe("name");
    }

    // Guards the split between the C# namespace (driven by --namespace, defaulting to the project
    // name) and the project/folder name (always driven by -n). Each pattern here must survive as the
    // "GameNamespace" placeholder so a future edit can't silently re-couple it to the project name.
    [Theory]
    [InlineData("templates/frb2-desktop/MyGame.Common/MyGame.Common.csproj", "<RootNamespace>GameNamespace</RootNamespace>")]
    [InlineData("templates/frb2-desktop/MyGame.Desktop/MyGame.Desktop.csproj", "<RootNamespace>GameNamespace</RootNamespace>")]
    [InlineData("templates/frb2-desktop/MyGame.Common/Game1.cs", "namespace GameNamespace;")]
    [InlineData("templates/frb2-desktop/MyGame.Common/Game1.cs", "using GameNamespace.Screens;")]
    [InlineData("templates/frb2-desktop/MyGame.Common/Screens/GameScreen.cs", "namespace GameNamespace.Screens;")]
    [InlineData("templates/frb2-desktop/MyGame.Desktop/Program.cs", "new GameNamespace.Game1()")]
    [InlineData("templates/frb2-multiplatform/MyGame.Common/MyGame.Common.csproj", "<RootNamespace>GameNamespace</RootNamespace>")]
    [InlineData("templates/frb2-multiplatform/MyGame.Common/Kni/MyGame.Common.Kni.csproj", "<RootNamespace>GameNamespace</RootNamespace>")]
    [InlineData("templates/frb2-multiplatform/MyGame.Desktop/MyGame.Desktop.csproj", "<RootNamespace>GameNamespace</RootNamespace>")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/MyGame.BlazorGL.csproj", "<RootNamespace>GameNamespace.BlazorGL</RootNamespace>")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/Program.cs", "namespace GameNamespace.BlazorGL")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/Program.cs", "new GameNamespace.Game1()")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/Pages/Index.razor.cs", "namespace GameNamespace.BlazorGL.Pages;")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/_Imports.razor", "@using GameNamespace.BlazorGL")]
    public void RootNamespaceOccurrence_UsesNamespacePlaceholder(string relativeFilePath, string expectedText)
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, relativeFilePath));

        content.ShouldContain(expectedText);
    }

    // Project/folder identity (paths, AssemblyName, the .slnx, launch profile names, the window
    // title) must stay driven by -n regardless of --namespace, so these must still say "MyGame"
    // (the sourceName placeholder) rather than the new "GameNamespace" namespace placeholder.
    [Theory]
    [InlineData("templates/frb2-desktop/MyGame.Desktop/MyGame.Desktop.csproj", @"ProjectReference Include=""..\MyGame.Common\MyGame.Common.csproj""")]
    [InlineData("templates/frb2-desktop/MyGame.slnx", @"Path=""MyGame.Desktop/MyGame.Desktop.csproj""")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/MyGame.BlazorGL.csproj", "<AssemblyName>MyGame.BlazorGL</AssemblyName>")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/Properties/launchSettings.json", "\"MyGame.BlazorGL\": {")]
    [InlineData("templates/frb2-multiplatform/MyGame.BlazorGL/wwwroot/index.html", "<title>MyGame (Web)</title>")]
    [InlineData("templates/frb2-multiplatform/MyGame.slnx", @"Path=""MyGame.BlazorGL/MyGame.BlazorGL.csproj""")]
    public void ProjectIdentityOccurrence_StaysTiedToProjectName(string relativeFilePath, string expectedText)
    {
        var content = File.ReadAllText(Path.Combine(RepoRoot, relativeFilePath));

        content.ShouldContain(expectedText);
    }

    private static string RepoRoot => TemplatePackageReferenceTests.RepoRootForTests;
}
