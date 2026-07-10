using FlatRedBall2;
using Gum.Forms.Controls;

namespace GumFormsPopupTest;

// Focused sample for issues #657 / #659: FlatRedBall2 now attaches Gum's SystemManagers to the
// Gum roots it owns, so EffectiveManagers resolves for controls added through FRB2. Each control
// below exercises a behavior Gum gates on EffectiveManagers != null — all silently broken before
// the fix:
//
//   ComboBox  -> dropdown must CLOSE on an outside click (ComboBox.HideListBox gate)
//   Menu      -> submenu must CLOSE on an outside click (MenuItem.HidePopupRecursively gate)
//   TextBox   -> must show its hover-highlight visual state on rollover (TextBoxBase gate)
//   Label     -> "Loaded fired: YES" proves FrameworkElement.Loaded fired (HandleParentChanged gate)
//
// Controls added via screen.Add live under the primary Camera.UiRoot; the ComboBox/Menu popups
// route to that camera's popup root and draw at its zoom, lining up under their opener.
public class TestScreen : Screen
{
    public override void CustomInitialize()
    {
        var instructions = new Label
        {
            X = 20,
            Y = 20,
            Width = 560,
            Text = "Open the dropdown/menu, then click empty space -> they must close. " +
                   "Hover the text box -> it should highlight."
        };
        Add(instructions);

        var loadedStatus = new Label { X = 20, Y = 70, Width = 560, Text = "ComboBox.Loaded fired: NO" };
        Add(loadedStatus);

        var combo = new ComboBox { X = 20, Y = 120, Width = 220, Height = 34 };
        combo.Items.Add("Apple");
        combo.Items.Add("Banana");
        combo.Items.Add("Cherry");
        // Subscribe BEFORE Add: managers are attached before CustomInitialize, so Loaded fires
        // synchronously when the control is parented here.
        combo.Loaded += (_, _) => loadedStatus.Text = "ComboBox.Loaded fired: YES";
        Add(combo);

        var textBox = new TextBox { X = 300, Y = 120, Width = 220, Height = 34, Text = "hover me" };
        Add(textBox);

        var menu = new Menu { X = 20, Y = 200 };
        var fileItem = new MenuItem { Header = "File" };
        // Items is annotated IList? but ItemsControl initializes it to a non-null collection.
        fileItem.Items!.Add(new MenuItem { Header = "Open" });
        fileItem.Items!.Add(new MenuItem { Header = "Save" });
        menu.Items!.Add(fileItem);
        Add(menu);

        // #659: element.AddToRoot() (Gum's own API -> GumService.Root) must now RENDER. Before the
        // fix a control added this way received input but never drew; GumService.Root is now unified
        // with the screen's OverlayRoot, so it draws full-window.
        var rootLabel = new Label
        {
            X = 20,
            Y = 300,
            Width = 540,
            Text = "Added via element.AddToRoot() -> unified GumService.Root. Must be visible."
        };
        rootLabel.AddToRoot();
    }
}
