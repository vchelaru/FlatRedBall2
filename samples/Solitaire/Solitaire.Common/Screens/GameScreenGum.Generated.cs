//Code for GameScreenGum
using Gum.Converters;
using Gum.DataTypes;
using Gum.Managers;
using Gum.Wireframe;
using GumRuntime;
using MonoGameGum;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using Solitaire.Components;
using System.Linq;
namespace Solitaire.Screens;
partial class GameScreenGum : global::Gum.Forms.Controls.FrameworkElement
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void RegisterRuntimeType()
    {
        var template = new global::Gum.Forms.VisualTemplate((vm, createForms) =>
        {
            var visual = new global::MonoGameGum.GueDeriving.ContainerRuntime();
            var element = ObjectFinder.Self.GetElementSave("GameScreenGum");
#if DEBUG
if(element == null) throw new System.InvalidOperationException("Could not find an element named GameScreenGum - did you forget to load a Gum project?");
#endif
            element.SetGraphicalUiElement(visual, RenderingLibrary.SystemManagers.Default);
            if(createForms) visual.FormsControlAsObject = new GameScreenGum(visual);
            visual.Width = 0;
            visual.WidthUnits = global::Gum.DataTypes.DimensionUnitType.RelativeToParent;
            visual.Height = 0;
            visual.HeightUnits = global::Gum.DataTypes.DimensionUnitType.RelativeToParent;
            return visual;
        });
        global::Gum.Forms.Controls.FrameworkElement.DefaultFormsTemplates[typeof(GameScreenGum)] = template;
        ElementSaveExtensions.RegisterGueInstantiation("GameScreenGum", () => 
        {
            var gue = template.CreateContent(null, true) as InteractiveGue;
            return gue;
        });
    }
    public FoundationSlot Foundation0 { get; protected set; }
    public FoundationSlot Foundation1 { get; protected set; }
    public FoundationSlot Foundation2 { get; protected set; }
    public FoundationSlot Foundation3 { get; protected set; }
    public FoundationSlot Foundation4 { get; protected set; }
    public RoundedRectangleRuntime Tableau0 { get; protected set; }
    public RoundedRectangleRuntime Tableau1 { get; protected set; }
    public RoundedRectangleRuntime Tableau2 { get; protected set; }
    public RoundedRectangleRuntime Tableau3 { get; protected set; }
    public RoundedRectangleRuntime Tableau4 { get; protected set; }
    public RoundedRectangleRuntime Tableau5 { get; protected set; }
    public RoundedRectangleRuntime Tableau6 { get; protected set; }
    public ColoredRectangleRuntime Felt { get; protected set; }
    public ContainerRuntime FoundationContainer { get; protected set; }
    public RoundedRectangleRuntime StockSlot { get; protected set; }
    public RoundedRectangleRuntime WasteSlot { get; protected set; }
    public ContainerRuntime TableauContainer { get; protected set; }

    public GameScreenGum(InteractiveGue visual) : base(visual)
    {
    }
    public GameScreenGum()
    {



    }
    protected override void ReactToVisualChanged()
    {
        base.ReactToVisualChanged();
        Foundation0 = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<FoundationSlot>(this.Visual,"Foundation0");
        Foundation1 = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<FoundationSlot>(this.Visual,"Foundation1");
        Foundation2 = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<FoundationSlot>(this.Visual,"Foundation2");
        Foundation3 = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<FoundationSlot>(this.Visual,"Foundation3");
        Foundation4 = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<FoundationSlot>(this.Visual,"Foundation4");
        Tableau0 = this.Visual?.GetGraphicalUiElementByName("Tableau0") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        Tableau1 = this.Visual?.GetGraphicalUiElementByName("Tableau1") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        Tableau2 = this.Visual?.GetGraphicalUiElementByName("Tableau2") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        Tableau3 = this.Visual?.GetGraphicalUiElementByName("Tableau3") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        Tableau4 = this.Visual?.GetGraphicalUiElementByName("Tableau4") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        Tableau5 = this.Visual?.GetGraphicalUiElementByName("Tableau5") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        Tableau6 = this.Visual?.GetGraphicalUiElementByName("Tableau6") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        Felt = this.Visual?.GetGraphicalUiElementByName("Felt") as global::MonoGameGum.GueDeriving.ColoredRectangleRuntime;
        FoundationContainer = this.Visual?.GetGraphicalUiElementByName("FoundationContainer") as global::MonoGameGum.GueDeriving.ContainerRuntime;
        StockSlot = this.Visual?.GetGraphicalUiElementByName("StockSlot") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        WasteSlot = this.Visual?.GetGraphicalUiElementByName("WasteSlot") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        TableauContainer = this.Visual?.GetGraphicalUiElementByName("TableauContainer") as global::MonoGameGum.GueDeriving.ContainerRuntime;
        CustomInitialize();
    }
    //Not assigning variables because Object Instantiation Type is set to By Name rather than Fully In Code
    partial void CustomInitialize();
}
