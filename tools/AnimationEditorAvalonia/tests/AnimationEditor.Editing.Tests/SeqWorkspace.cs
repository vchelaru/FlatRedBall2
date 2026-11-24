using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing.Tests;

/// <summary>
/// A temp folder holding a real source animation file (<c>hero.achj</c>: Walk 0.4 s, Idle 0.5 s) and a generated
/// <c>sheet.seqj</c>: sequence Run (loops) with tracks Body [Walk@0 x2, Walk@1 x1] and Shadow [Idle@0], and
/// sequence Rest (no loop) with track Body [Idle@0].
/// </summary>
public sealed class SeqWorkspace : IDisposable
{
    private const string Hero = """
        {
          "fileRelativeTextures": true,
          "timeMeasurementUnit": "Second",
          "coordinateType": "Pixel",
          "animationChains": [
            { "name": "Walk", "frames": [
              { "textureName": "sheet.png", "frameLength": 0.1, "leftCoordinate": 0, "rightCoordinate": 16, "topCoordinate": 0, "bottomCoordinate": 32 },
              { "textureName": "sheet.png", "frameLength": 0.1, "leftCoordinate": 16, "rightCoordinate": 32, "topCoordinate": 0, "bottomCoordinate": 32 },
              { "textureName": "sheet.png", "frameLength": 0.2, "leftCoordinate": 32, "rightCoordinate": 48, "topCoordinate": 0, "bottomCoordinate": 32 } ] },
            { "name": "Idle", "frames": [
              { "textureName": "sheet.png", "frameLength": 0.5, "leftCoordinate": 0, "rightCoordinate": 16, "topCoordinate": 0, "bottomCoordinate": 32 } ] }
          ]
        }
        """;

    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "AnimationEditorEditingTests", Guid.NewGuid().ToString("N"));
    public string SeqPath => Path.Combine(Dir, "sheet.seqj");
    public string HeroPath => Path.Combine(Dir, "hero.achj");

    public SeqWorkspace()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(HeroPath, Hero);
        File.WriteAllText(Path.Combine(Dir, "sheet.png"), ""); // hero.achj's texture; only its presence is checked
        File.WriteAllText(Path.Combine(Dir, "villain.achj"), Hero);
        File.WriteAllText(Path.Combine(Dir, "notes.txt"), "not an animation");
        Build().Save(SeqPath);
    }

    public static SequenceListSave Build()
    {
        var save = new SequenceListSave();
        save.Sources.Add("hero.achj");
        var run = new SequenceSave { Name = "Run" };
        run.Tracks.Add(new SequenceTrackSave
        {
            Name = "Body",
            Chains =
            {
                new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 0, Length = 0.8 },
                new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Walk", Start = 1 },
            },
        });
        run.Tracks.Add(new SequenceTrackSave
        {
            Name = "Shadow",
            Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Idle", Start = 0 } },
        });
        save.Sequences.Add(run);
        var rest = new SequenceSave { Name = "Rest", Loop = false };
        rest.Tracks.Add(new SequenceTrackSave
        {
            Name = "Body",
            Chains = { new SequenceChainReferenceSave { Source = "hero.achj", Chain = "Idle", Start = 0 } },
        });
        save.Sequences.Add(rest);
        return save;
    }

    public string Json => File.ReadAllText(SeqPath);
    public byte[] Bytes => File.ReadAllBytes(SeqPath);

    public SequenceSession Open()
    {
        var open = SequenceSession.Open(SeqPath);
        if (open.Session is null) throw new InvalidOperationException(string.Join("; ", open.Result.Errors));
        return open.Session;
    }

    public void Dispose()
    {
        if (Directory.Exists(Dir)) Directory.Delete(Dir, recursive: true);
    }
}
