using System;
using System.IO;
using System.Text;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

// Every input is written byte-exact from code, because git's autocrlf can rewrite a checked-in fixture.
public class SaveLineEndingTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("SaveLineEndingTests").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Theory]
    [InlineData("achx", "\n", false)]
    [InlineData("achx", "\n", true)]
    [InlineData("achx", "\r\n", false)]
    [InlineData("achx", "\r\n", true)]
    [InlineData("achj", "\n", false)]
    [InlineData("achj", "\n", true)]
    [InlineData("achj", "\r\n", false)]
    [InlineData("achj", "\r\n", true)]
    [InlineData("seqj", "\n", false)]
    [InlineData("seqj", "\n", true)]
    [InlineData("seqj", "\r\n", false)]
    [InlineData("seqj", "\r\n", true)]
    public void Save_OverExistingFile_KeepsItsLineEndingAndFinalNewline(string extension, string newLine, bool finalNewLine)
    {
        string final = finalNewLine ? newLine : "";
        string path = WriteFile(extension, Lines(extension, "Walk").ReplaceLineEndings(newLine) + final);

        LoadRenameAndSave(path, "Stride");

        ReadExact(path).ShouldBe(Lines(extension, "Stride").ReplaceLineEndings(newLine) + final);
    }

    // The bytes main wrote for such a file, so a CRLF file on Windows is untouched.
    [Theory]
    [InlineData("achx", "\r\n")]
    [InlineData("achj", null)]
    [InlineData("seqj", null)]
    public void Save_NewFile_WritesTheDefaultLineEndingAndNoFinalNewline(string extension, string? newLine)
    {
        string path = Path.Combine(_dir, "new." + extension);

        SaveNew(path, "Stride");

        ReadExact(path).ShouldBe(Lines(extension, "Stride").ReplaceLineEndings(newLine ?? Environment.NewLine));
    }

    [Theory]
    [InlineData("\n", "\r\n")]
    [InlineData("\r\n", "\n")]
    public void Save_OverMixedLineEndings_KeepsTheFirstLines(string first, string rest)
    {
        string text = Lines("achj", "Walk").ReplaceLineEndings(rest);
        int firstBreak = text.IndexOf(rest, StringComparison.Ordinal);
        string path = WriteFile("achj", text[..firstBreak] + first + text[(firstBreak + rest.Length)..]);

        LoadRenameAndSave(path, "Stride");

        ReadExact(path).ShouldBe(Lines("achj", "Stride").ReplaceLineEndings(first));
    }

    [Fact]
    public void Save_OverSingleLineFile_WritesTheDefaultLineEnding()
    {
        string path = WriteFile("achj", """{"fileRelativeTextures":true,"timeMeasurementUnit":"Undefined","coordinateType":"Pixel","animationChains":[{"name":"Walk","frames":[]}]}""");

        LoadRenameAndSave(path, "Stride");

        ReadExact(path).ShouldBe(Lines("achj", "Stride").ReplaceLineEndings(Environment.NewLine));
    }

    // \u0085, \u2028 and \f are line breaks to string.ReplaceLineEndings but plain characters in both formats.
    [Theory]
    [InlineData("achx", "\n", "a\nb\u0085c\u2028d")]
    [InlineData("achx", "\r\n", "a\nb\u0085c\u2028d")]
    [InlineData("achj", "\n", "a\r\nb\nc\u0085d\u2028e\ff")]
    [InlineData("achj", "\r\n", "a\r\nb\nc\u0085d\u2028e\ff")]
    public void Save_OverExistingFile_KeepsLineBreaksInsideValues(string extension, string newLine, string name)
    {
        string path = WriteFile(extension, Lines(extension, "Walk").ReplaceLineEndings(newLine));

        LoadRenameAndSave(path, name);

        AnimationChainListSave.FromString(ReadExact(path)).AnimationChains[0].Name.ShouldBe(name);
    }

    // Windows refuses to replace the locked file and Linux allows it; reading its line ending must change neither.
    [Fact]
    public void Save_OverUnreadableFile_EndsLikeAPlainWrite()
    {
        string plain = WriteFile("achx", Lines("achx", "Walk"));
        string saved = WriteFile("achj", Lines("achj", "Walk"));
        using var plainLock = new FileStream(plain, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var savedLock = new FileStream(saved, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Type? expected = Record.Exception(() => AtomicFile.Write(plain, stream => stream.WriteByte(1)))?.GetType();

        Exception? actual = Record.Exception(() => SaveNew(saved, "Stride"));

        actual?.GetType().ShouldBe(expected);
    }

    private static string Lines(string extension, string name) => extension switch
    {
        "achx" => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <AnimationChainArraySave xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <FileRelativeTextures>true</FileRelativeTextures>
              <TimeMeasurementUnit>Undefined</TimeMeasurementUnit>
              <CoordinateType>Pixel</CoordinateType>
              <AnimationChain>
                <Name>{name}</Name>
              </AnimationChain>
            </AnimationChainArraySave>
            """,
        "achj" => $$"""
            {
              "fileRelativeTextures": true,
              "timeMeasurementUnit": "Undefined",
              "coordinateType": "Pixel",
              "animationChains": [
                {
                  "name": "{{name}}",
                  "frames": []
                }
              ]
            }
            """,
        _ => $$"""
            {
              "version": 1,
              "sources": [],
              "sequences": [
                {
                  "name": "{{name}}",
                  "loop": true,
                  "tracks": []
                }
              ]
            }
            """,
    };

    private string WriteFile(string extension, string text)
    {
        string path = Path.Combine(_dir, "file." + extension);
        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
        return path;
    }

    // GetString keeps every byte visible, including a byte-order mark.
    private static string ReadExact(string path) => Encoding.UTF8.GetString(File.ReadAllBytes(path));

    private static void LoadRenameAndSave(string path, string name)
    {
        if (path.EndsWith(".seqj", StringComparison.Ordinal))
        {
            var sequences = SequenceListSave.FromFile(path);
            sequences.Sequences[0].Name = name;
            sequences.Save(path);
            return;
        }
        var chains = path.EndsWith(".achj", StringComparison.Ordinal)
            ? AnimationChainListSave.FromJsonFile(path)
            : AnimationChainListSave.FromFile(path);
        chains.AnimationChains[0].Name = name;
        SaveChains(chains, path);
    }

    private static void SaveNew(string path, string name)
    {
        if (path.EndsWith(".seqj", StringComparison.Ordinal))
        {
            new SequenceListSave { Sequences = { new SequenceSave { Name = name } } }.Save(path);
            return;
        }
        SaveChains(new AnimationChainListSave
        {
            FileRelativeTextures = true,
            TimeMeasurementUnit = TimeMeasurementUnit.Undefined,
            CoordinateType = TextureCoordinateType.Pixel,
            AnimationChains = { new AnimationChainSave { Name = name } },
        }, path);
    }

    private static void SaveChains(AnimationChainListSave chains, string path)
    {
        if (path.EndsWith(".achj", StringComparison.Ordinal)) chains.SaveJson(path);
        else chains.Save(path);
    }
}
