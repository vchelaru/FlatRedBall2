using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>What a cut into another tab carries.</summary>
public class LeavingAndCutInteractionScenarioTests
{
    private const string Frb1Fields = """
        <?xml version="1.0" encoding="utf-8"?>
        <AnimationChainArraySave>
          <FileRelativeTextures>true</FileRelativeTextures>
          <TimeMeasurementUnit>Second</TimeMeasurementUnit>
          <CoordinateType>Pixel</CoordinateType>
          <AnimationChain>
            <Name>Walk</Name>
            <ColorKey>7</ColorKey>
            <ParentFile>walk.gif</ParentFile>
            <Frame>
              <TextureName>sheet.png</TextureName>
              <FrameLength>0.1</FrameLength>
              <LeftCoordinate>0</LeftCoordinate>
              <RightCoordinate>16</RightCoordinate>
              <TopCoordinate>0</TopCoordinate>
              <BottomCoordinate>16</BottomCoordinate>
              <ShapeCollectionSave>
                <AxisAlignedRectangleSaves>
                  <AxisAlignedRectangleSave>
                    <X>0</X>
                    <Y>0</Y>
                    <ScaleX>8</ScaleX>
                    <ScaleY>8</ScaleY>
                    <Name>Hit</Name>
                    <Parent>Body</Parent>
                  </AxisAlignedRectangleSave>
                </AxisAlignedRectangleSaves>
                <AxisAlignedCubeSaves><AxisAlignedCubeSave><ScaleX>1</ScaleX></AxisAlignedCubeSave></AxisAlignedCubeSaves>
                <PolygonSaves />
                <CircleSaves />
                <SphereSaves><SphereSave><Radius>1</Radius></SphereSave></SphereSaves>
              </ShapeCollectionSave>
            </Frame>
          </AnimationChain>
        </AnimationChainArraySave>
        """;

    [AvaloniaTheory]
    [InlineData("chain", "ColorKey,ParentFile,Parent>Body,AxisAlignedCubeSave>,SphereSave>")]
    [InlineData("frame", "Parent>Body,AxisAlignedCubeSave>,SphereSave>")]
    [InlineData("shape", "Parent>Body")]
    public async Task CutIntoAnotherTab_KeepsTheFrb1Fields(string what, string kept)
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string source = Path.Combine(editor.ProjectFolder, "source.achx");
        File.WriteAllText(source, Frb1Fields);
        string target = editor.WriteAchx("target.achx", AnimationEditorHarness.Chain("Target", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenAsync(target);
        await editor.OpenAsync(source);
        var walk = editor.ChainNamed("Walk");
        if (what != "chain") editor.Expand(walk);
        if (what == "shape") editor.Expand(walk.Frames[0]);
        editor.ClickRow(what switch { "chain" => walk, "frame" => walk.Frames[0], _ => walk.Frames[0].ShapesSave!.Shapes[0] });

        editor.Press(Key.X, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(50));
        editor.ClickTab("target.achx");
        var to = editor.ChainNamed("Target");
        if (what == "shape") editor.Expand(to);
        editor.ClickRow(what == "shape" ? to.Frames[0] : to);
        editor.Press(Key.V, RawInputModifiers.Control);
        editor.Wait(TimeSpan.FromMilliseconds(100));

        string text = File.ReadAllText(target);
        text.ShouldContain(what == "chain" ? "Walk" : "<Frame>");
        kept.Split(',').Where(part => !text.Contains(part)).ShouldBeEmpty();
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClosingTheProjectFolder_WithATabWhoseEditCannotBeSaved_AsksFirst(bool inFront)
    {
        using var editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string hero = editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16)));
        string other = editor.WriteAchx("other.achx", AnimationEditorHarness.Chain("Other", "sheet.png", (0, 0, 16, 16)));
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(other);
        await editor.OpenAsync(hero);
        UnusualProjectScenarioTests.MakeUnwritable(hero);
        try
        {
            editor.Services.AppCommands.AddAnimationChainWithName("Unsaved");
            editor.Wait(TimeSpan.FromMilliseconds(100));
            if (!inFront) editor.ClickTab("other.achx");
            editor.Dialogs.AnswerNextSaveDiscardCancel(AnimationEditor.Views.Dialogs.SaveDiscardCancelChoice.Cancel);

            editor.Window.CloseProjectFolder();
            await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

            editor.TabLabels.ShouldContain(label => label.Contains("hero.achx"));
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(hero);
        }
    }

    [AvaloniaFact]
    public async Task ClosingTheProjectFolder_WithASequenceTabWhoseEditCannotBeSaved_AsksFirst()
    {
        using var editor = new AnimationEditorHarness();
        string fight = editor.WriteSeqj("fight.seqj", "One");
        await editor.OpenProjectFolderAsync();
        await editor.OpenAsync(fight);
        UnusualProjectScenarioTests.MakeUnwritable(fight);
        try
        {
            editor.Click(editor.Control<Button>("AddSequenceButton"));
            editor.ProblemText.ShouldNotBeNull().ShouldStartWith("Auto save failed");
            editor.Dialogs.AnswerNextSaveDiscardCancel(AnimationEditor.Views.Dialogs.SaveDiscardCancelChoice.Cancel);

            editor.Window.CloseProjectFolder();
            await editor.WaitAsync(TimeSpan.FromMilliseconds(300));

            editor.TabLabels.ShouldContain(label => label.Contains("fight.seqj"));
        }
        finally
        {
            UnusualProjectScenarioTests.MakeWritable(fight);
        }
    }
}
