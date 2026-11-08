using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AnimationEditor.App.Services;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// A double-click in Finder opens the editor only if the .app's Info.plist declares the file types; without
/// <c>CFBundleDocumentTypes</c> macOS offers it for nothing. The Windows installer's registrations
/// (<see cref="WindowsFileAssociations.FileTypes"/>) are the list of what the editor opens, so the bundle must declare
/// the same extensions.
/// </summary>
public class MacOSBundleDocumentTypesTests
{
    private static string InfoPlistPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "AnimationEditor.App", "macos", "Info.plist"));

    /// <summary>The plist's top-level dict as key → value element pairs.</summary>
    private static Dictionary<string, XElement> TopLevel()
    {
        XElement dict = XDocument.Load(InfoPlistPath, LoadOptions.None).Root!.Element("dict")!;
        var elements = dict.Elements().ToList();
        var result = new Dictionary<string, XElement>();
        for (int i = 0; i + 1 < elements.Count; i += 2)
            result[elements[i].Value] = elements[i + 1];
        return result;
    }

    [Fact]
    public void InfoPlist_DeclaresEveryExtensionTheWindowsInstallerRegisters_AsAnEditor()
    {
        var plist = TopLevel();
        plist.ShouldContainKey("CFBundleDocumentTypes");

        var declared = new List<string>();
        foreach (XElement type in plist["CFBundleDocumentTypes"].Elements("dict"))
        {
            var elements = type.Elements().ToList();
            var entries = new Dictionary<string, XElement>();
            for (int i = 0; i + 1 < elements.Count; i += 2)
                entries[elements[i].Value] = elements[i + 1];

            entries["CFBundleTypeRole"].Value.ShouldBe("Editor");
            declared.AddRange(entries["CFBundleTypeExtensions"].Elements("string").Select(extension => extension.Value));
        }

        string[] registered = WindowsFileAssociations.FileTypes
            .SelectMany(type => type.Extensions)
            .Select(extension => extension.TrimStart('.'))
            .ToArray();
        declared.OrderBy(name => name).ShouldBe(registered.OrderBy(name => name));
    }
}
