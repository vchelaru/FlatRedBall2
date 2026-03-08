//Code for MainMenuScreenGum
using Gum.Converters;
using Gum.DataTypes;
using Gum.Managers;
using Gum.Wireframe;
using GumRuntime;
using MonoGameGum;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using System.Linq;
using TopDownMenuSampleWithCodeGen.Components.Controls;
namespace TopDownMenuSampleWithCodeGen.Screens;
partial class MainMenuScreenGum : global::Gum.Forms.Controls.FrameworkElement
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void RegisterRuntimeType()
    {
        var template = new global::Gum.Forms.VisualTemplate((vm, createForms) =>
        {
            var visual = new global::MonoGameGum.GueDeriving.ContainerRuntime();
            var element = ObjectFinder.Self.GetElementSave("MainMenuScreenGum");
#if DEBUG
if(element == null) throw new System.InvalidOperationException("Could not find an element named MainMenuScreenGum - did you forget to load a Gum project?");
#endif
            element.SetGraphicalUiElement(visual, RenderingLibrary.SystemManagers.Default);
            if(createForms) visual.FormsControlAsObject = new MainMenuScreenGum(visual);
            visual.Width = 0;
            visual.WidthUnits = global::Gum.DataTypes.DimensionUnitType.RelativeToParent;
            visual.Height = 0;
            visual.HeightUnits = global::Gum.DataTypes.DimensionUnitType.RelativeToParent;
            return visual;
        });
        global::Gum.Forms.Controls.FrameworkElement.DefaultFormsTemplates[typeof(MainMenuScreenGum)] = template;
        ElementSaveExtensions.RegisterGueInstantiation("MainMenuScreenGum", () => 
        {
            var gue = template.CreateContent(null, true) as InteractiveGue;
            return gue;
        });
    }
    public ColoredRectangleRuntime Background { get; protected set; }
    public TextRuntime TitleText { get; protected set; }
    public ButtonStandard StartGameButton { get; protected set; }
    public ButtonStandard ExitButton { get; protected set; }

    public MainMenuScreenGum(InteractiveGue visual) : base(visual)
    {
    }
    public MainMenuScreenGum()
    {



    }
    protected override void ReactToVisualChanged()
    {
        base.ReactToVisualChanged();
        Background = this.Visual?.GetGraphicalUiElementByName("Background") as global::MonoGameGum.GueDeriving.ColoredRectangleRuntime;
        TitleText = this.Visual?.GetGraphicalUiElementByName("TitleText") as global::MonoGameGum.GueDeriving.TextRuntime;
        StartGameButton = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ButtonStandard>(this.Visual,"StartGameButton");
        ExitButton = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ButtonStandard>(this.Visual,"ExitButton");
        CustomInitialize();
    }
    //Not assigning variables because Object Instantiation Type is set to By Name rather than Fully In Code
    partial void CustomInitialize();
}
