using System.IO;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

/// <summary>A file whose content is not read opens as empty, and the next save erases it, so it is refused (#2122).</summary>
public class NotAnAnimationFileTests
{
    [Theory]
    [InlineData("<tileset name=\"x\" />", "<tileset>")]
    [InlineData("<animationchainarraysave />", "<animationchainarraysave>")]
    [InlineData("<AnimationChainArraySave xmlns=\"urn:other\" />", "'urn:other'")]
    [InlineData("<AnimationChain><Name>Walk</Name></AnimationChain>", "<AnimationChain>")]
    [InlineData("{\"sequences\":[]}", "\"animationChains\"")]
    [InlineData("{\"AnimationChains\":[{\"name\":\"Walk\"}]}", "\"animationChains\"")]
    [InlineData("{\"animationChains\":{}}", "animationChains must be an array")]
    [InlineData("{\"animationChains\":[5]}", "animationChains[0] must be an object")]
    [InlineData("{\"animationChains\":[{\"frames\":{}}]}", "animationChains[0].frames must be an array")]
    [InlineData("{\"animationChains\":[{\"loop\":{}}]}", "animationChains[0].loop must be true or false")]
    [InlineData("{\"animationChains\":[{\"frames\":[{\"frameLength\":[1]}]}]}", "frames[0].frameLength must be a number")]
    [InlineData("{\"animationChains\":[{\"frames\":[{\"textureName\":5}]}]}", "frames[0].textureName must be a string")]
    [InlineData("{\"animationChains\":[{\"frames\":[{\"red\":{}}]}]}", "frames[0].red must be a whole number")]
    [InlineData("{\"animationChains\":[{\"frames\":[{\"shapes\":[]}]}]}", "frames[0].shapes must be an object")]
    [InlineData("{\"animationChains\":[{\"frames\":[{\"shapes\":{\"circles\":{}}}]}]}", "shapes.circles must be an array")]
    [InlineData("{\"animationChains\":[{\"frames\":[{\"shapes\":{\"polygons\":[{\"points\":{}}]}}]}]}", "polygons[0].points must be an array")]
    [InlineData("{\"animationChains\":[{\"frames\":[{\"events\":[\"Hit\"]}]}]}", "events[0] must be an object")]
    public void FromString_ContentThatIsNotRead_IsRefusedSayingWhatIsWrong(string text, string problem) =>
        Should.Throw<InvalidDataException>(() => AnimationChainListSave.FromString(text)).Message.ShouldContain(problem);

    [Theory]
    [InlineData("<AnimationChainArraySave><AnimationChain><Name>Walk</Name><Frame /></AnimationChain></AnimationChainArraySave>")]
    [InlineData("<AnimationChainArraySave xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" other=\"1\"><Unknown /><AnimationChain><Name>Walk</Name><Frame><HasCustomName>true</HasCustomName></Frame></AnimationChain></AnimationChainArraySave>")]
    [InlineData("{\"other\":1,\"animationChains\":[{\"name\":\"Walk\",\"locked\":null,\"frames\":[{\"textureName\":null,\"red\":null,\"shapes\":null,\"events\":null}]}]}")]
    [InlineData("{\"animationChains\":[{\"frames\":[{\"events\":[null,{\"name\":\"Hit\"}]}]}]}")]
    public void FromString_PartsMissingNullOrUnknown_IsRead(string text) =>
        AnimationChainListSave.FromString(text).AnimationChains.ShouldHaveSingleItem().Frames.Count.ShouldBe(1);
}
