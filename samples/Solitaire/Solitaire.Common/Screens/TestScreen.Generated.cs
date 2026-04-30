//Code for TestScreen
using Gum.Converters;
using Gum.DataTypes;
using Gum.Managers;
using Gum.Wireframe;
using GumRuntime;
using MonoGameGum;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using System.Linq;
namespace Solitaire.Screens;
partial class TestScreen : global::Gum.Forms.Controls.FrameworkElement
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void RegisterRuntimeType()
    {
        var template = new global::Gum.Forms.VisualTemplate((vm, createForms) =>
        {
            var visual = new global::MonoGameGum.GueDeriving.ContainerRuntime();
            var element = ObjectFinder.Self.GetElementSave("TestScreen");
#if DEBUG
if(element == null) throw new System.InvalidOperationException("Could not find an element named TestScreen - did you forget to load a Gum project?");
#endif
            element.SetGraphicalUiElement(visual, RenderingLibrary.SystemManagers.Default);
            if(createForms) visual.FormsControlAsObject = new TestScreen(visual);
            visual.Width = 0;
            visual.WidthUnits = global::Gum.DataTypes.DimensionUnitType.RelativeToParent;
            visual.Height = 0;
            visual.HeightUnits = global::Gum.DataTypes.DimensionUnitType.RelativeToParent;
            return visual;
        });
        global::Gum.Forms.Controls.FrameworkElement.DefaultFormsTemplates[typeof(TestScreen)] = template;
        ElementSaveExtensions.RegisterGueInstantiation("TestScreen", () => 
        {
            var gue = template.CreateContent(null, true) as InteractiveGue;
            return gue;
        });
    }
    public RoundedRectangleRuntime Background2 { get; protected set; }
    public RoundedRectangleRuntime Background1 { get; protected set; }
    public RoundedRectangleRuntime Background3 { get; protected set; }
    public ColoredCircleRuntime ColoredCircleInstance { get; protected set; }

    public TestScreen(InteractiveGue visual) : base(visual)
    {
    }
    public TestScreen()
    {



    }
    protected override void ReactToVisualChanged()
    {
        base.ReactToVisualChanged();
        Background2 = this.Visual?.GetGraphicalUiElementByName("Background2") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        Background1 = this.Visual?.GetGraphicalUiElementByName("Background1") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        Background3 = this.Visual?.GetGraphicalUiElementByName("Background3") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        ColoredCircleInstance = this.Visual?.GetGraphicalUiElementByName("ColoredCircleInstance") as global::MonoGameGum.GueDeriving.ColoredCircleRuntime;
        CustomInitialize();
    }
    //Not assigning variables because Object Instantiation Type is set to By Name rather than Fully In Code
    partial void CustomInitialize();
}
