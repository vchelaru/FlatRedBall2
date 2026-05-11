using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// Per-test service graph for AnimationEditor.Core tests. Each call to
/// <see cref="SetupFreshAcls"/> builds a brand-new set of services so tests
/// are fully isolated — no static state.
/// </summary>
internal sealed class TestServices
{
    public AnimationChainListSave Acls { get; }
    public ProjectManager ProjectManager { get; }
    public ApplicationEvents ApplicationEvents { get; }
    public SelectedState SelectedState { get; }
    public AppState AppState { get; }
    public IoManager IoManager { get; }
    public ObjectFinder ObjectFinder { get; }
    public UndoManager UndoManager { get; }
    public AppCommands AppCommands { get; }

    public TestServices()
    {
        ProjectManager    = new ProjectManager();
        ApplicationEvents = new ApplicationEvents();
        SelectedState     = new SelectedState(ProjectManager);
        AppState          = new AppState(ApplicationEvents, SelectedState);
        IoManager         = new IoManager(AppState);
        ObjectFinder      = new ObjectFinder(ProjectManager);
        UndoManager       = new UndoManager();
        AppCommands       = new AppCommands(ProjectManager, SelectedState, ApplicationEvents,
                                            IoManager, ObjectFinder, UndoManager);

        Acls = new AnimationChainListSave();
        ProjectManager.AnimationChainListSave = Acls;
        ProjectManager.FileName = null;

        SelectedState.SelectedChain = null;
        SelectedState.SelectedNodes = new List<object>();

        AppCommands.ConfirmAsync       = (msg, title) => Task.FromResult(true);
        AppCommands.PromptStringAsync  = (title, prompt, initial) => Task.FromResult<string?>(initial);
        AppCommands.DoOnUiThread       = action => action();
        AppCommands.FileDialogService  = NullFileDialogService.Instance;

        AppState.GridSize           = 16;
        AppState.IsSnapToGridChecked = false;
        AppState.WireframeZoomValue = 100;
        AppState.UnitType           = AnimationEditor.Core.Data.UnitType.Pixel;
        AppState.ProjectFolder      = null;
    }
}

internal static class TestHelpers
{
    /// <summary>
    /// Builds a fresh service graph for a test. Returns a context whose
    /// properties expose every service; tests address them directly instead
    /// of relying on global state.
    /// </summary>
    public static TestServices SetupFreshAcls() => new TestServices();

    /// <summary>Creates a frame with an initialized ShapeCollectionSave.</summary>
    public static AnimationFrameSave MakeFrame(string textureName = "Tex.png")
    {
        return new AnimationFrameSave
        {
            TextureName      = textureName,
            LeftCoordinate   = 0f,
            RightCoordinate  = 1f,
            TopCoordinate    = 0f,
            BottomCoordinate = 1f,
            FrameLength      = 0.1f,
            ShapeCollectionSave = new ShapeCollectionSave()
        };
    }

    /// <summary>Creates a named chain, adds <paramref name="frameCount"/> frames, and adds it to <paramref name="acls"/>.</summary>
    public static AnimationChainSave MakeChain(AnimationChainListSave acls, string name, int frameCount = 0)
    {
        var chain = new AnimationChainSave { Name = name };
        for (int i = 0; i < frameCount; i++)
            chain.Frames.Add(MakeFrame($"frame{i}.png"));
        acls.AnimationChains.Add(chain);
        return chain;
    }

    /// <summary>Disposable helper that creates a temp directory and cleans it up.</summary>
    public sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "AnimationEditorCoreTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
