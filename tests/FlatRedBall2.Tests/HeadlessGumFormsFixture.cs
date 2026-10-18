using System;
using System.Text;
using Gum;
using Gum.Forms;
using Gum.GueDeriving;
using Gum.Wireframe;
using RenderingLibrary;
using RenderingLibrary.Graphics;
using RenderingLibrary.Graphics.Fonts;
using Xunit;

namespace FlatRedBall2.Tests;

/// <summary>
/// Boots Gum Forms with no <c>GraphicsDevice</c>, so real controls (<c>Button</c>, <c>TextBox</c>)
/// can be constructed and driven through <c>FormsUtilities.Update</c> — Gum's own dispatch, not a
/// stand-in. Same recipe as Gum's <c>TestAssemblyInitializeBase</c>, minus the pieces only its
/// layout tests need.
/// </summary>
/// <remarks>
/// A real <see cref="Screen"/> boot does not reach Gum's update in a headless engine (it sits behind
/// the sprite-batch guard in <see cref="FlatRedBallService.Update"/>), so tests call
/// <c>FormsUtilities.Update</c> themselves with roots from <see cref="CreateRoot"/>.
/// <para>
/// Everything here is Gum static state. The fixture deliberately leaves
/// <c>SystemManagers.Default</c> alone — the roots resolve their managers through
/// <c>AttachManagersOnly</c> instead — because tests in <see cref="GraphicsDeviceCollection"/> read
/// that static to decide whether Gum is already owned by another test. What
/// <c>FormsUtilities.InitializeDefaults</c> and the font stub do overwrite (<c>IGumService.Default</c>,
/// the Forms cursor/keyboard, popup/modal roots, active style, over-behavior, the default bitmap
/// font, the renderable property hook, canvas size) is captured on construction and restored on
/// <see cref="Dispose"/>. Not restorable: the lazily created <c>GumService.Default</c> singleton and
/// <c>FormsUtilities.Gamepads</c>, both harmless to leave. Tests that focus a control must null
/// <c>InteractiveGue.CurrentInputReceiver</c> before they return, or the next test types into it.
/// </para>
/// </remarks>
public sealed class HeadlessGumFormsFixture : IDisposable
{
    private readonly IGumService? _previousService;
    private readonly Gum.Wireframe.ICursor? _previousCursor;
    private readonly IInputReceiverKeyboard? _previousKeyboard;
    private readonly BitmapFont? _previousFont;
    private readonly InteractiveGue? _previousPopupRoot = Gum.Forms.Controls.FrameworkElement.PopupRoot;
    private readonly InteractiveGue? _previousModalRoot = Gum.Forms.Controls.FrameworkElement.ModalRoot;
    private readonly Gum.Forms.DefaultVisuals.V3.Styling? _previousStyle = Gum.Forms.DefaultVisuals.V3.Styling.ActiveStyle;
    private readonly VisualOverBehavior _previousVisualOverBehavior = Gum.Wireframe.ICursor.VisualOverBehavior;
    private readonly Func<IRenderableIpso, GraphicalUiElement, string, object?, bool>? _previousSetProperty = GraphicalUiElement.SetPropertyOnRenderable;
    private readonly float _previousCanvasWidth = GraphicalUiElement.CanvasWidth;
    private readonly float _previousCanvasHeight = GraphicalUiElement.CanvasHeight;

    /// <summary>Canvas size every root from <see cref="CreateRoot"/> fills; controls must be placed inside it to be hit.</summary>
    public const float CanvasWidth = 800;

    /// <inheritdoc cref="CanvasWidth"/>
    public const float CanvasHeight = 600;

    public HeadlessGumFormsFixture()
    {
        // KNI's native gamepad poll, which InitializeDefaults triggers, crashes the host unless SDL
        // has been initialized by a Game first.
        _ = SharedGameHost.Game;

        _previousService = IGumService.Default;
        _previousCursor = FormsUtilities.Cursor;
        _previousKeyboard = FormsUtilities.Keyboard;
        _previousFont = Text.DefaultBitmapFont;

        Managers = new SystemManagers { Renderer = new Renderer() };
        Managers.Renderer.AddLayer(new Layer());

        // InitializeDefaults creates Gum's own cursor and keyboard through IGumService.Default, in
        // the same order the real runtime's Initialize does.
        IGumService.Default = GumService.Default;
        FormsUtilities.InitializeDefaults(Managers);

        // Zero until a windowed Gum initialization sizes it. Gum's in-window test and every root from
        // CreateRoot read it, and a zero-sized root is "over" nothing, so no child is ever hit-tested.
        GraphicalUiElement.CanvasWidth = CanvasWidth;
        GraphicalUiElement.CanvasHeight = CanvasHeight;

        // Focusing a TextBox places its caret, which measures text; with no font loaded that path
        // dereferences a null SpriteFont. A fixed-advance stub is enough for measurement.
        Text.DefaultBitmapFont = CreateStubFont();

        // Installed by SystemManagers.Initialize, which needs a device. Without it a property set on
        // a visual (a TextBox writing its Text) is dropped, and the control reads back empty.
        GraphicalUiElement.SetPropertyOnRenderable = CustomSetPropertyOnRenderable.SetPropertyOnRenderable;
    }

    /// <summary>The managers every root from <see cref="CreateRoot"/> resolves; never the process-wide default.</summary>
    public SystemManagers Managers { get; }

    /// <summary>
    /// A full-canvas, input-transparent root to parent controls under and pass to
    /// <c>FormsUtilities.Update</c>. Shaped like <see cref="Screen.OverlayRoot"/>.
    /// </summary>
    public GraphicalUiElement CreateRoot()
    {
        var root = new ContainerRuntime
        {
            Name = "HeadlessGumFormsFixture.Root",
            HasEvents = false,
            WidthUnits = global::Gum.DataTypes.DimensionUnitType.Absolute,
            HeightUnits = global::Gum.DataTypes.DimensionUnitType.Absolute,
            Width = CanvasWidth,
            Height = CanvasHeight,
        };
        root.AttachManagersOnly(Managers);
        return root;
    }

    private static BitmapFont CreateStubFont()
    {
        var pattern = new StringBuilder()
            .AppendLine("info face=\"Arial\" size=-18 bold=0 italic=0 charset=\"\" unicode=1 stretchH=100 smooth=1 aa=1 padding=0,0,0,0 spacing=1,1 outline=0")
            .AppendLine("common lineHeight=21 base=17 scaleW=256 scaleH=256 pages=1 packed=0 alphaChnl=0 redChnl=4 greenChnl=4 blueChnl=4")
            .AppendLine("chars count=223");
        for (var i = 32; i < 255; i++)
            pattern.AppendLine($"char id={i} x=0 y=0 width=10 height=8 xoffset=0 yoffset=7 xadvance=10 page=0 chnl=15");

        var font = new BitmapFont(fontTextureGraphic: null!, fontPattern: pattern.ToString());
        // No texture means the parser leaves every character null; measurement only needs advances.
        for (var i = 0; i < font.Characters.Length; i++)
            font.Characters[i] = new BitmapCharacterInfo { XAdvance = 8 };
        return font;
    }

    public void Dispose()
    {
        InteractiveGue.CurrentInputReceiver = null;
        InteractiveGue.ClearNextClickActions();
        InteractiveGue.ClearNextPushActions();
        GraphicalUiElement.SetPropertyOnRenderable = _previousSetProperty!;
        Gum.Wireframe.ICursor.VisualOverBehavior = _previousVisualOverBehavior;
        Gum.Forms.DefaultVisuals.V3.Styling.ActiveStyle = _previousStyle!;
        Gum.Forms.Controls.FrameworkElement.PopupRoot = _previousPopupRoot!;
        Gum.Forms.Controls.FrameworkElement.ModalRoot = _previousModalRoot!;
        GraphicalUiElement.CanvasWidth = _previousCanvasWidth;
        GraphicalUiElement.CanvasHeight = _previousCanvasHeight;
        Text.DefaultBitmapFont = _previousFont!;
        FormsUtilities.SetCursor(_previousCursor!);
        // Gum has no public way back to "no keyboard", so a run that started without one keeps ours.
        if (_previousKeyboard != null)
        {
            FormsUtilities.SetKeyboard(_previousKeyboard);
        }

        IGumService.Default = _previousService!;
    }
}

/// <summary>
/// Home for every test class that boots Gum Forms headless or installs its own cursor/keyboard into
/// Gum, so they share one bootstrap and tear down together. Cross-collection parallelism is off
/// assembly-wide (see AssemblyInfo.cs); this collection groups, it does not itself serialize.
/// </summary>
[CollectionDefinition(Name)]
public sealed class HeadlessGumFormsCollection : ICollectionFixture<HeadlessGumFormsFixture>
{
    public const string Name = "HeadlessGumForms";
}
