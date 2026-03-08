//Code for StylesScreen
using Gum.Converters;
using Gum.DataTypes;
using Gum.Managers;
using Gum.Wireframe;
using GumRuntime;
using MonoGameGum;
using MonoGameGum.GueDeriving;
using PongGravity.Components.Controls;
using PongGravity.Components.Elements;
using RenderingLibrary.Graphics;
using System.Linq;
namespace PongGravity.Screens;
partial class StylesScreen : global::Gum.Forms.Controls.FrameworkElement
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    public static void RegisterRuntimeType()
    {
        var template = new global::Gum.Forms.VisualTemplate((vm, createForms) =>
        {
            var visual = new global::MonoGameGum.GueDeriving.ContainerRuntime();
            var element = ObjectFinder.Self.GetElementSave("StylesScreen");
#if DEBUG
if(element == null) throw new System.InvalidOperationException("Could not find an element named StylesScreen - did you forget to load a Gum project?");
#endif
            element.SetGraphicalUiElement(visual, RenderingLibrary.SystemManagers.Default);
            if(createForms) visual.FormsControlAsObject = new StylesScreen(visual);
            visual.Width = 0;
            visual.WidthUnits = global::Gum.DataTypes.DimensionUnitType.RelativeToParent;
            visual.Height = 0;
            visual.HeightUnits = global::Gum.DataTypes.DimensionUnitType.RelativeToParent;
            return visual;
        });
        global::Gum.Forms.Controls.FrameworkElement.DefaultFormsTemplates[typeof(StylesScreen)] = template;
        ElementSaveExtensions.RegisterGueInstantiation("StylesScreen", () => 
        {
            var gue = template.CreateContent(null, true) as InteractiveGue;
            return gue;
        });
    }
    public ContainerRuntime TextStyleContainer { get; protected set; }
    public TextRuntime TextTitle { get; protected set; }
    public TextRuntime TextH1 { get; protected set; }
    public TextRuntime TextH2 { get; protected set; }
    public TextRuntime TextH3 { get; protected set; }
    public TextRuntime TextNormal { get; protected set; }
    public TextRuntime TextStrong { get; protected set; }
    public TextRuntime TextEmphasis { get; protected set; }
    public TextRuntime TextSmall { get; protected set; }
    public TextRuntime TextTiny { get; protected set; }
    public NineSliceRuntime Solid { get; protected set; }
    public NineSliceRuntime Bordered { get; protected set; }
    public NineSliceRuntime BracketHorizontal { get; protected set; }
    public NineSliceRuntime BracketVertical { get; protected set; }
    public NineSliceRuntime Tab { get; protected set; }
    public NineSliceRuntime TabBordered { get; protected set; }
    public NineSliceRuntime Outlined { get; protected set; }
    public NineSliceRuntime OutlinedHeavy { get; protected set; }
    public NineSliceRuntime Panel { get; protected set; }
    public ContainerRuntime NineSliceStyleContainer { get; protected set; }
    public ContainerRuntime ButtonsContainer { get; protected set; }
    public ButtonStandard ButtonStandardInstance { get; protected set; }
    public ButtonStandardIcon ButtonStandardIconInstance { get; protected set; }
    public ButtonTab ButtonTabInstance { get; protected set; }
    public ButtonIcon ButtonIconInstance { get; protected set; }
    public ButtonConfirm ButtonConfirmInstance { get; protected set; }
    public ButtonDeny ButtonDenyInstance { get; protected set; }
    public ButtonClose ButtonCloseInstance { get; protected set; }
    public ContainerRuntime ElementsContainer { get; protected set; }
    public PercentBar PercentBarPrimary { get; protected set; }
    public PercentBar PercentBarLinesDecor { get; protected set; }
    public PercentBar PercentBarCautionDecor { get; protected set; }
    public PercentBarIcon PercentBarIconPrimary { get; protected set; }
    public PercentBarIcon PercentBarIconLinesDecor { get; protected set; }
    public PercentBarIcon PercentBarIconCautionDecor { get; protected set; }
    public ContainerRuntime ControlsContainer { get; protected set; }
    public Label LabelInstance { get; protected set; }
    public CheckBox CheckBoxInstance { get; protected set; }
    public RadioButton RadioButtonInstance { get; protected set; }
    public ComboBox ComboBoxInstance { get; protected set; }
    public ListBox ListBoxInstance { get; protected set; }
    public Slider SliderInstance { get; protected set; }
    public TextBox TextBoxInstance { get; protected set; }
    public PasswordBox PasswordBoxInstance { get; protected set; }
    public DividerVertical DividerVerticalInstance { get; protected set; }
    public DividerHorizontal DividerHorizontalInstance { get; protected set; }
    public Icon IconInstance { get; protected set; }
    public CautionLines CautionLinesInstance { get; protected set; }
    public VerticalLines VerticalLinesInstance { get; protected set; }
    public ContainerRuntime ColorContainer { get; protected set; }
    public TextRuntime TextBlack { get; protected set; }
    public TextRuntime TextDarkGray { get; protected set; }
    public TextRuntime TextGray { get; protected set; }
    public TextRuntime TextLightGray { get; protected set; }
    public TextRuntime TextWhite { get; protected set; }
    public TextRuntime TextPrimaryDark { get; protected set; }
    public TextRuntime TextPrimary { get; protected set; }
    public TextRuntime TextPrimaryLight { get; protected set; }
    public TextRuntime TextAccent { get; protected set; }
    public TextRuntime TextSuccess { get; protected set; }
    public TextRuntime TextWarning { get; protected set; }
    public TextRuntime TextWarning1 { get; protected set; }
    public Keyboard KeyboardInstance { get; protected set; }
    public ContainerRuntime KeyboardContainer { get; protected set; }
    public TreeView TreeViewInstance { get; protected set; }
    public DialogBox DialogBoxInstance { get; protected set; }

    public StylesScreen(InteractiveGue visual) : base(visual)
    {
    }
    public StylesScreen()
    {



    }
    protected override void ReactToVisualChanged()
    {
        base.ReactToVisualChanged();
        TextStyleContainer = this.Visual?.GetGraphicalUiElementByName("TextStyleContainer") as global::MonoGameGum.GueDeriving.ContainerRuntime;
        TextTitle = this.Visual?.GetGraphicalUiElementByName("TextTitle") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextH1 = this.Visual?.GetGraphicalUiElementByName("TextH1") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextH2 = this.Visual?.GetGraphicalUiElementByName("TextH2") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextH3 = this.Visual?.GetGraphicalUiElementByName("TextH3") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextNormal = this.Visual?.GetGraphicalUiElementByName("TextNormal") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextStrong = this.Visual?.GetGraphicalUiElementByName("TextStrong") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextEmphasis = this.Visual?.GetGraphicalUiElementByName("TextEmphasis") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextSmall = this.Visual?.GetGraphicalUiElementByName("TextSmall") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextTiny = this.Visual?.GetGraphicalUiElementByName("TextTiny") as global::MonoGameGum.GueDeriving.TextRuntime;
        Solid = this.Visual?.GetGraphicalUiElementByName("Solid") as global::MonoGameGum.GueDeriving.NineSliceRuntime;
        Bordered = this.Visual?.GetGraphicalUiElementByName("Bordered") as global::MonoGameGum.GueDeriving.NineSliceRuntime;
        BracketHorizontal = this.Visual?.GetGraphicalUiElementByName("BracketHorizontal") as global::MonoGameGum.GueDeriving.NineSliceRuntime;
        BracketVertical = this.Visual?.GetGraphicalUiElementByName("BracketVertical") as global::MonoGameGum.GueDeriving.NineSliceRuntime;
        Tab = this.Visual?.GetGraphicalUiElementByName("Tab") as global::MonoGameGum.GueDeriving.NineSliceRuntime;
        TabBordered = this.Visual?.GetGraphicalUiElementByName("TabBordered") as global::MonoGameGum.GueDeriving.NineSliceRuntime;
        Outlined = this.Visual?.GetGraphicalUiElementByName("Outlined") as global::MonoGameGum.GueDeriving.NineSliceRuntime;
        OutlinedHeavy = this.Visual?.GetGraphicalUiElementByName("OutlinedHeavy") as global::MonoGameGum.GueDeriving.NineSliceRuntime;
        Panel = this.Visual?.GetGraphicalUiElementByName("Panel") as global::MonoGameGum.GueDeriving.NineSliceRuntime;
        NineSliceStyleContainer = this.Visual?.GetGraphicalUiElementByName("NineSliceStyleContainer") as global::MonoGameGum.GueDeriving.ContainerRuntime;
        ButtonsContainer = this.Visual?.GetGraphicalUiElementByName("ButtonsContainer") as global::MonoGameGum.GueDeriving.ContainerRuntime;
        ButtonStandardInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ButtonStandard>(this.Visual,"ButtonStandardInstance");
        ButtonStandardIconInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ButtonStandardIcon>(this.Visual,"ButtonStandardIconInstance");
        ButtonTabInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ButtonTab>(this.Visual,"ButtonTabInstance");
        ButtonIconInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ButtonIcon>(this.Visual,"ButtonIconInstance");
        ButtonConfirmInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ButtonConfirm>(this.Visual,"ButtonConfirmInstance");
        ButtonDenyInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ButtonDeny>(this.Visual,"ButtonDenyInstance");
        ButtonCloseInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ButtonClose>(this.Visual,"ButtonCloseInstance");
        ElementsContainer = this.Visual?.GetGraphicalUiElementByName("ElementsContainer") as global::MonoGameGum.GueDeriving.ContainerRuntime;
        PercentBarPrimary = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<PercentBar>(this.Visual,"PercentBarPrimary");
        PercentBarLinesDecor = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<PercentBar>(this.Visual,"PercentBarLinesDecor");
        PercentBarCautionDecor = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<PercentBar>(this.Visual,"PercentBarCautionDecor");
        PercentBarIconPrimary = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<PercentBarIcon>(this.Visual,"PercentBarIconPrimary");
        PercentBarIconLinesDecor = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<PercentBarIcon>(this.Visual,"PercentBarIconLinesDecor");
        PercentBarIconCautionDecor = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<PercentBarIcon>(this.Visual,"PercentBarIconCautionDecor");
        ControlsContainer = this.Visual?.GetGraphicalUiElementByName("ControlsContainer") as global::MonoGameGum.GueDeriving.ContainerRuntime;
        LabelInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<Label>(this.Visual,"LabelInstance");
        CheckBoxInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<CheckBox>(this.Visual,"CheckBoxInstance");
        RadioButtonInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<RadioButton>(this.Visual,"RadioButtonInstance");
        ComboBoxInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ComboBox>(this.Visual,"ComboBoxInstance");
        ListBoxInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<ListBox>(this.Visual,"ListBoxInstance");
        SliderInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<Slider>(this.Visual,"SliderInstance");
        TextBoxInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<TextBox>(this.Visual,"TextBoxInstance");
        PasswordBoxInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<PasswordBox>(this.Visual,"PasswordBoxInstance");
        DividerVerticalInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<DividerVertical>(this.Visual,"DividerVerticalInstance");
        DividerHorizontalInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<DividerHorizontal>(this.Visual,"DividerHorizontalInstance");
        IconInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<Icon>(this.Visual,"IconInstance");
        CautionLinesInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<CautionLines>(this.Visual,"CautionLinesInstance");
        VerticalLinesInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<VerticalLines>(this.Visual,"VerticalLinesInstance");
        ColorContainer = this.Visual?.GetGraphicalUiElementByName("ColorContainer") as global::MonoGameGum.GueDeriving.ContainerRuntime;
        TextBlack = this.Visual?.GetGraphicalUiElementByName("TextBlack") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextDarkGray = this.Visual?.GetGraphicalUiElementByName("TextDarkGray") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextGray = this.Visual?.GetGraphicalUiElementByName("TextGray") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextLightGray = this.Visual?.GetGraphicalUiElementByName("TextLightGray") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextWhite = this.Visual?.GetGraphicalUiElementByName("TextWhite") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextPrimaryDark = this.Visual?.GetGraphicalUiElementByName("TextPrimaryDark") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextPrimary = this.Visual?.GetGraphicalUiElementByName("TextPrimary") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextPrimaryLight = this.Visual?.GetGraphicalUiElementByName("TextPrimaryLight") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextAccent = this.Visual?.GetGraphicalUiElementByName("TextAccent") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextSuccess = this.Visual?.GetGraphicalUiElementByName("TextSuccess") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextWarning = this.Visual?.GetGraphicalUiElementByName("TextWarning") as global::MonoGameGum.GueDeriving.TextRuntime;
        TextWarning1 = this.Visual?.GetGraphicalUiElementByName("TextWarning1") as global::MonoGameGum.GueDeriving.TextRuntime;
        KeyboardInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<Keyboard>(this.Visual,"KeyboardInstance");
        KeyboardContainer = this.Visual?.GetGraphicalUiElementByName("KeyboardContainer") as global::MonoGameGum.GueDeriving.ContainerRuntime;
        TreeViewInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<TreeView>(this.Visual,"TreeViewInstance");
        DialogBoxInstance = global::Gum.Forms.GraphicalUiElementFormsExtensions.TryGetFrameworkElementByName<DialogBox>(this.Visual,"DialogBoxInstance");
        CustomInitialize();
    }
    //Not assigning variables because Object Instantiation Type is set to By Name rather than Fully In Code
    partial void CustomInitialize();
}
