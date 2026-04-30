//Code for CardGum (Container)
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
partial class CardGum : global::Gum.Forms.Controls.FrameworkElement
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void RegisterRuntimeType()
    {
        var template = new global::Gum.Forms.VisualTemplate((vm, createForms) =>
        {
            var visual = new global::MonoGameGum.GueDeriving.ContainerRuntime();
            var element = ObjectFinder.Self.GetElementSave("CardGum");
#if DEBUG
if(element == null) throw new System.InvalidOperationException("Could not find an element named CardGum - did you forget to load a Gum project?");
#endif
            element.SetGraphicalUiElement(visual, RenderingLibrary.SystemManagers.Default);
            if(createForms) visual.FormsControlAsObject = new CardGum(visual);
            return visual;
        });
        global::Gum.Forms.Controls.FrameworkElement.DefaultFormsTemplates[typeof(CardGum)] = template;
        ElementSaveExtensions.RegisterGueInstantiation("CardGum", () => 
        {
            var gue = template.CreateContent(null, true) as InteractiveGue;
            return gue;
        });
    }
    public enum Suit
    {
        Spades,
        Hearts,
        Clubs,
        Diamonds,
    }
    public enum Rank
    {
        Ace,
        _2,
        _3,
        _4,
        _5,
        _6,
        _7,
        _8,
        _9,
        _10,
        Jack,
        Queen,
        King,
    }
    public enum Facing
    {
        Up,
        Down,
    }

    Suit? _suitState;
    public Suit? SuitState
    {
        get => _suitState;
        set
        {
            _suitState = value;
            if(value != null)
            {
                if(Visual.Categories.ContainsKey("Suit"))
                {
                    var category = Visual.Categories["Suit"];
                    var state = category.States.Find(item => item.Name == value.ToString());
                    this.Visual.ApplyState(state);
                }
                else
                {
                    var category = ((global::Gum.DataTypes.ElementSave)this.Visual.Tag).Categories.FirstOrDefault(item => item.Name == "Suit");
                    var state = category.States.Find(item => item.Name == value.ToString());
                    this.Visual.ApplyState(state);
                }
            }
        }
    }

    Rank? _rankState;
    public Rank? RankState
    {
        get => _rankState;
        set
        {
            _rankState = value;
            if(value != null)
            {
                if(Visual.Categories.ContainsKey("Rank"))
                {
                    var category = Visual.Categories["Rank"];
                    var state = category.States.Find(item => item.Name == value.ToString());
                    this.Visual.ApplyState(state);
                }
                else
                {
                    var category = ((global::Gum.DataTypes.ElementSave)this.Visual.Tag).Categories.FirstOrDefault(item => item.Name == "Rank");
                    var state = category.States.Find(item => item.Name == value.ToString());
                    this.Visual.ApplyState(state);
                }
            }
        }
    }

    Facing? _facingState;
    public Facing? FacingState
    {
        get => _facingState;
        set
        {
            _facingState = value;
            if(value != null)
            {
                if(Visual.Categories.ContainsKey("Facing"))
                {
                    var category = Visual.Categories["Facing"];
                    var state = category.States.Find(item => item.Name == value.ToString());
                    this.Visual.ApplyState(state);
                }
                else
                {
                    var category = ((global::Gum.DataTypes.ElementSave)this.Visual.Tag).Categories.FirstOrDefault(item => item.Name == "Facing");
                    var state = category.States.Find(item => item.Name == value.ToString());
                    this.Visual.ApplyState(state);
                }
            }
        }
    }
    public RoundedRectangleRuntime Background { get; protected set; }
    public TextRuntime RankText1 { get; protected set; }
    public TextRuntime RankText2 { get; protected set; }
    public SpriteRuntime SuitIcon { get; protected set; }
    public RoundedRectangleRuntime Back { get; protected set; }

    public CardGum(InteractiveGue visual) : base(visual)
    {
    }
    public CardGum()
    {



    }
    protected override void ReactToVisualChanged()
    {
        base.ReactToVisualChanged();
        Background = this.Visual?.GetGraphicalUiElementByName("Background") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        RankText1 = this.Visual?.GetGraphicalUiElementByName("RankText1") as global::MonoGameGum.GueDeriving.TextRuntime;
        RankText2 = this.Visual?.GetGraphicalUiElementByName("RankText2") as global::MonoGameGum.GueDeriving.TextRuntime;
        SuitIcon = this.Visual?.GetGraphicalUiElementByName("SuitIcon") as global::MonoGameGum.GueDeriving.SpriteRuntime;
        Back = this.Visual?.GetGraphicalUiElementByName("Back") as global::MonoGameGum.GueDeriving.RoundedRectangleRuntime;
        CustomInitialize();
    }
    //Not assigning variables because Object Instantiation Type is set to By Name rather than Fully In Code
    partial void CustomInitialize();
}
