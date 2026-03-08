//Code for PauseMenuScreenGum
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
partial class PauseMenuScreenGum : global::Gum.Forms.Controls.FrameworkElement
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void RegisterRuntimeType()
    {
        var template = new global::Gum.Forms.VisualTemplate((vm, createForms) =>
        {
            var visual = new global::MonoGameGum.GueDeriving.ContainerRuntime();
            var element = ObjectFinder.Self.GetElementSave("PauseMenuScreenGum");
#if DEBUG
if(element == null) throw new System.InvalidOperationException("Could not find an element named PauseMenuScreenGum - did you forget to load a Gum project?");
#endif
            element.SetGraphicalUiElement(visual, RenderingLibrary.SystemManagers.Default);
            if(createForms) visual.FormsControlAsObject = new PauseMenuScreenGum(visual);
            visual.Width = 0;
            visual.WidthUnits = global::Gum.DataTypes.DimensionUnitType.RelativeToParent;
            visual.Height = 0;
            visual.HeightUnits = global::Gum.DataTypes.DimensionUnitType.RelativeToParent;
            return visual;
        });
        global::Gum.Forms.Controls.FrameworkElement.DefaultFormsTemplates[typeof(PauseMenuScreenGum)] = template;
        ElementSaveExtensions.RegisterGueInstantiation("PauseMenuScreenGum", () => 
        {
            var gue = template.CreateContent(null, true) as InteractiveGue;
            return gue;
        });
    }
    public ColoredRectangleRuntime Overlay { get; protected set; }
    public TextRuntime TitleText { get; protected set; }
    public ButtonStandard ResumeButton { get; protected set; }
    public ButtonStandard ExitToMenuButton { get; protected set; }

    public PauseMenuScreenGum(InteractiveGue visual) : base(visual)
    {
    }
    public PauseMenuScreenGum()
    {



    }
    protected override void ReactToVisualChanged()
    {
        base.ReactToVisualChanged();
        Overlay = this.Visual?.GetGraphicalUiElementByName("Overlay") as global::MonoGameGum.GueDeriving.ColoredRectangleRuntime;
        TitleText = this.Visual?.GetGraphicalUiElementByName("TitleText") as global::MonoGameGum.GueDeriving.TextRuntime;
        ResumeButton = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ButtonStandard>(this.Visual,"ResumeButton");
        ExitToMenuButton = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ButtonStandard>(this.Visual,"ExitToMenuButton");
        CustomInitialize();
    }
    //Not assigning variables because Object Instantiation Type is set to By Name rather than Fully In Code
    partial void CustomInitialize();
}
