using AnimationEditor.SaveModel;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class SaveModelRoundTripTests
{
    // Guards against a stored member with no writer or no reader: it would be lost on every save without a word.
    [Theory]
    [InlineData("achx")]
    [InlineData("achj")]
    public void Save_EveryStoredMemberNonDefault_ReadsBackEveryStoredMember(string extension)
    {
        int counter = 100;
        var source = (AnimationChainListSave)SaveModelFill.CreateFilled(typeof(AnimationChainListSave), ref counter);
        source.FileName = string.Empty; // where the file is, which the file itself does not store

        using var stream = new MemoryStream();
        if (extension == "achj") source.SaveJson(stream); else source.Save(stream);
        stream.Position = 0;
        var readBack = AnimationChainListSave.FromStream(stream);

        var differences = new List<string>();
        SaveModelFill.Compare(source, readBack, extension, differences);
        differences.ShouldBeEmpty();
    }
}
