//Code for FoundationSlot (Container)
using Gum.Converters;
using Gum.DataTypes;
using Gum.Managers;
using Gum.Wireframe;
using GumRuntime;
using MonoGameGum;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using System.Linq;
namespace Solitaire.Components;
partial class FoundationSlot : global::Gum.Forms.Controls.FrameworkElement
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void RegisterRuntimeType()
    {
        var template = new global::Gum.Forms.VisualTemplate((vm, createForms) =>
        {
            var visual = new global::MonoGameGum.GueDeriving.ContainerRuntime();
            var element = ObjectFinder.Self.GetElementSave("FoundationSlot");
#if DEBUG
if(element == null) throw new System.InvalidOperationException("Could not find an element named FoundationSlot - did you forget to load a Gum project?");
#endif
            element.SetGraphicalUiElement(visual, RenderingLibrary.SystemManagers.Default);
            if(createForms) visual.FormsControlAsObject = new FoundationSlot(visual);
            return visual;
        });
        global::Gum.Forms.Controls.FrameworkElement.DefaultFormsTemplates[typeof(FoundationSlot)] = template;
        ElementSaveExtensions.RegisterGueInstantiation("FoundationSlot", () => 
        {
            var gue = template.CreateContent(null, true) as InteractiveGue;
            return gue;
        });
    }
    public RoundedRectangleRuntime Background { get; protected set; }
    public RoundedRectangleRuntime Background1 { get; protected set; }
    public SpriteRuntime SpriteInstance { get; protected set; }

    public FoundationSlot(InteractiveGue visual) : base(visual)
    {
    }
    public FoundationSlot()
    {



    }
    protected override void ReactToVisualChanged()
    {
        base.ReactToVisualChanged();
        Background = this.Visual?.GetGraphicalUiElementByName("Background") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        Background1 = this.Visual?.GetGraphicalUiElementByName("Background1") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        SpriteInstance = this.Visual?.GetGraphicalUiElementByName("SpriteInstance") as global::MonoGameGum.GueDeriving.SpriteRuntime;
        CustomInitialize();
    }
    //Not assigning variables because Object Instantiation Type is set to By Name rather than Fully In Code
    partial void CustomInitialize();
}
