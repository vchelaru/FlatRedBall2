using System;
using System.IO;
using System.Text;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>A hot reload in the middle of a hand edit: the file reads as XML or JSON, but a value in it does not parse.</summary>
public class HotReloadParseFailureTests
{
    private sealed class Frame : AnimationFrameBase { }

    [Theory]
    [InlineData("a.achx", "<AnimationChainArraySave><AnimationChain><Frame><FrameLength></FrameLength></Frame></AnimationChain></AnimationChainArraySave>")]
    [InlineData("a.achx", "<AnimationChainArraySave><AnimationChain><Frame><ColorOperation>Mult</ColorOperation></Frame></AnimationChain></AnimationChainArraySave>")]
    [InlineData("a.achx", "<AnimationChainArraySave><AnimationChain><Loop>fals</Loop></AnimationChain></AnimationChainArraySave>")]
    [InlineData("a.achx", "<AnimationChainArraySave><AnimationChain><Frame><Red>99999999999</Red></Frame></AnimationChain></AnimationChainArraySave>")]
    [InlineData("a.achx", "<AnimationChainArraySave><AnimationChain>")]
    [InlineData("a.achj", "{\"animationChains\":[{\"name\":5}]}")]
    [InlineData("a.achj", "{\"animationChains\":[null]}")]
    [InlineData("a.achj", "{\"animationChains\":[],\"animationChains\":[]}")]
    [InlineData("a.achj", "{\"animationChains\":[{\"name\":\"@d800\"}]}")] // @ stands for a backslash and u: the test runner decodes those
    [InlineData("a.achj", "[]")]
    [InlineData("a.achj", "null")]
    [InlineData("a.achj", "{\"animationChains\":[")]
    public void TryReloadFrom_ValueThatDoesNotParse_ReturnsFalse(string path, string text)
    {
        var list = new AnimationChainList<Frame> { new AnimationChain<Frame> { Name = "Walk" } };

        bool? result = null;
        Should.NotThrow(() => result = list.TryReloadFrom(path, _ => new MemoryStream(Encoding.UTF8.GetBytes(text.Replace("@", @"\u"))), _ => new Frame()));

        result.ShouldBe(false);
        list.Count.ShouldBe(1);
    }
}
