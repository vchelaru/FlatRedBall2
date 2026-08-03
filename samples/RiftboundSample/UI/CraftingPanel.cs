using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Models;
using RiftboundSample.Systems;

namespace RiftboundSample.UI;

/// <summary>
/// Full-screen crafting overlay. Left panel shows a filterable recipe list;
/// right panel shows selected recipe details and a craft button.
/// </summary>
public class CraftingPanel
{
    private Screen _screen = null!;
    private Panel _root = null!;

    // Left side
    private StackPanel _filterRow = null!;
    private StackPanel _recipeList = null!;
    private readonly List<Label> _recipeLabels = [];
    private readonly List<Label> _filterLabels = [];

    // Right side
    private Label _detailName = null!;
    private Label _detailDescription = null!;
    private StackPanel _materialsList = null!;
    private Label _outputLabel = null!;
    private Label _craftButton = null!;
    private Label _batchLabel = null!;
    private Label _pinLabel = null!;

    // State
    private List<RecipeData> _allRecipes = [];
    private List<RecipeData> _filteredRecipes = [];
    private PartyState _party = null!;
    private CraftingSystem _craftingSystem = null!;
    private int _selectedRecipeIndex;
    private int _selectedFilterIndex;
    private bool _onFilterRow = true;
    private int _batchIndex;
    private readonly int[] _batchAmounts = [1, 5, 10, -1]; // -1 = max

    private static readonly string[] Filters = ["All", "Weapon", "Armor", "Accessory", "Consumable"];

    /// <summary>Item ID of the pinned recipe's next needed material, if any.</summary>
    public string? PinnedRecipeId { get; private set; }

    public bool IsVisible => _root?.Visual.Visible ?? false;
    public event Action? Closed;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _root = new Panel();
        _root.Dock(Dock.Fill);
        _root.Visual.Visible = false;

        // Background
        var bg = new ColoredRectangleRuntime
        {
            Width = 0, Height = 0,
            WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
            HeightUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent,
            Red = 20, Green = 20, Blue = 30, Alpha = 230,
        };
        _root.Visual.Children.Add(bg);

        var title = new Label { Text = "-- Crafting --" };
        title.Anchor(Anchor.Top);
        title.Y = 8;
        _root.AddChild(title);

        // Main horizontal layout
        var mainRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        mainRow.Anchor(Anchor.TopLeft);
        mainRow.X = 16;
        mainRow.Y = 32;

        // --- Left panel ---
        var leftPanel = new StackPanel { Spacing = 4 };

        _filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var filter in Filters)
        {
            var fl = new Label { Text = filter };
            _filterRow.AddChild(fl);
            _filterLabels.Add(fl);
        }
        leftPanel.AddChild(_filterRow);

        _recipeList = new StackPanel { Spacing = 2 };
        leftPanel.AddChild(_recipeList);

        mainRow.AddChild(leftPanel);

        // --- Right panel ---
        var rightPanel = new StackPanel { Spacing = 6 };

        _detailName = new Label { Text = "" };
        rightPanel.AddChild(_detailName);

        _detailDescription = new Label { Text = "" };
        rightPanel.AddChild(_detailDescription);

        var matHeader = new Label { Text = "Materials:" };
        rightPanel.AddChild(matHeader);

        _materialsList = new StackPanel { Spacing = 2 };
        rightPanel.AddChild(_materialsList);

        _outputLabel = new Label { Text = "" };
        rightPanel.AddChild(_outputLabel);

        // Batch selector
        var batchRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _batchLabel = new Label { Text = "x1" };
        batchRow.AddChild(new Label { Text = "Amount:" });
        batchRow.AddChild(_batchLabel);
        rightPanel.AddChild(batchRow);

        _craftButton = new Label { Text = "  [Craft]" };
        rightPanel.AddChild(_craftButton);

        _pinLabel = new Label { Text = "  (T to pin tracker)" };
        rightPanel.AddChild(_pinLabel);

        mainRow.AddChild(rightPanel);
        _root.AddChild(mainRow);

        // Controls hint
        var hint = new Label { Text = "Up/Down: select  Left/Right: filter  Enter: craft  T: pin  Esc: close" };
        hint.Anchor(Anchor.BottomLeft);
        hint.X = 16;
        hint.Y = -8;
        _root.AddChild(hint);

        _screen.Add(_root);
    }

    public void Show(List<RecipeData> recipes, PartyState party, CraftingSystem craftingSystem)
    {
        _allRecipes = recipes;
        _party = party;
        _craftingSystem = craftingSystem;
        _selectedFilterIndex = 0;
        _selectedRecipeIndex = 0;
        _batchIndex = 0;
        _onFilterRow = true;
        ApplyFilter();
        _root.Visual.Visible = true;
    }

    public void Hide()
    {
        _root.Visual.Visible = false;
    }

    public void Update(FlatRedBallService engine)
    {
        if (!IsVisible) return;

        var kb = engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Escape))
        {
            Hide();
            Closed?.Invoke();
            return;
        }

        if (_onFilterRow)
            UpdateFilterSelection(kb);
        else
            UpdateRecipeSelection(kb);
    }

    private void UpdateFilterSelection(FlatRedBall2.Input.IKeyboard kb)
    {
        if (kb.WasKeyPressed(Keys.Left))
        {
            _selectedFilterIndex = (_selectedFilterIndex - 1 + Filters.Length) % Filters.Length;
            ApplyFilter();
        }
        else if (kb.WasKeyPressed(Keys.Right))
        {
            _selectedFilterIndex = (_selectedFilterIndex + 1) % Filters.Length;
            ApplyFilter();
        }
        else if (kb.WasKeyPressed(Keys.Down) && _filteredRecipes.Count > 0)
        {
            _onFilterRow = false;
            _selectedRecipeIndex = 0;
            UpdateRecipeHighlight();
            UpdateDetail();
        }
    }

    private void UpdateRecipeSelection(FlatRedBall2.Input.IKeyboard kb)
    {
        if (_filteredRecipes.Count == 0)
        {
            if (kb.WasKeyPressed(Keys.Up))
                _onFilterRow = true;
            return;
        }

        if (kb.WasKeyPressed(Keys.Up))
        {
            if (_selectedRecipeIndex == 0)
            {
                _onFilterRow = true;
                UpdateFilterHighlight();
                return;
            }
            _selectedRecipeIndex--;
            UpdateRecipeHighlight();
            UpdateDetail();
        }
        else if (kb.WasKeyPressed(Keys.Down))
        {
            if (_selectedRecipeIndex < _filteredRecipes.Count - 1)
            {
                _selectedRecipeIndex++;
                UpdateRecipeHighlight();
                UpdateDetail();
            }
        }
        else if (kb.WasKeyPressed(Keys.Left))
        {
            _batchIndex = (_batchIndex - 1 + _batchAmounts.Length) % _batchAmounts.Length;
            UpdateBatchLabel();
        }
        else if (kb.WasKeyPressed(Keys.Right))
        {
            _batchIndex = (_batchIndex + 1) % _batchAmounts.Length;
            UpdateBatchLabel();
        }
        else if (kb.WasKeyPressed(Keys.Enter) || kb.WasKeyPressed(Keys.Space))
        {
            DoCraft();
        }
        else if (kb.WasKeyPressed(Keys.T))
        {
            var recipe = _filteredRecipes[_selectedRecipeIndex];
            PinnedRecipeId = PinnedRecipeId == recipe.Id ? null : recipe.Id;
            UpdateDetail();
        }
    }

    private void DoCraft()
    {
        if (_filteredRecipes.Count == 0) return;
        var recipe = _filteredRecipes[_selectedRecipeIndex];
        int amount = _batchAmounts[_batchIndex];
        if (amount == -1)
            amount = _craftingSystem.GetMaxCraftable(recipe, _party);

        _craftingSystem.Craft(recipe, _party, amount);
        UpdateRecipeHighlight();
        UpdateDetail();
    }

    private void ApplyFilter()
    {
        string filter = Filters[_selectedFilterIndex].ToLowerInvariant();
        _filteredRecipes = filter == "all"
            ? new List<RecipeData>(_allRecipes)
            : _allRecipes.Where(r => r.Category == filter).ToList();

        _selectedRecipeIndex = 0;
        RebuildRecipeList();
        UpdateFilterHighlight();
        UpdateDetail();
    }

    private void RebuildRecipeList()
    {
        foreach (var label in _recipeLabels)
            _recipeList.Visual.Children.Remove(label.Visual);
        _recipeLabels.Clear();

        for (int i = 0; i < _filteredRecipes.Count; i++)
        {
            var recipe = _filteredRecipes[i];
            bool canCraft = _craftingSystem.CanCraft(recipe, _party);
            string prefix = !_onFilterRow && i == _selectedRecipeIndex ? "> " : "  ";
            string dim = canCraft ? "" : " (need materials)";
            var label = new Label { Text = $"{prefix}{recipe.Name}{dim}" };
            _recipeList.AddChild(label);
            _recipeLabels.Add(label);
        }
    }

    private void UpdateRecipeHighlight()
    {
        for (int i = 0; i < _recipeLabels.Count && i < _filteredRecipes.Count; i++)
        {
            var recipe = _filteredRecipes[i];
            bool canCraft = _craftingSystem.CanCraft(recipe, _party);
            string prefix = !_onFilterRow && i == _selectedRecipeIndex ? "> " : "  ";
            string dim = canCraft ? "" : " (need materials)";
            _recipeLabels[i].Text = $"{prefix}{recipe.Name}{dim}";
        }
    }

    private void UpdateFilterHighlight()
    {
        for (int i = 0; i < _filterLabels.Count; i++)
        {
            string prefix = _onFilterRow && i == _selectedFilterIndex ? "[" : " ";
            string suffix = _onFilterRow && i == _selectedFilterIndex ? "]" : " ";
            _filterLabels[i].Text = $"{prefix}{Filters[i]}{suffix}";
        }
    }

    private void UpdateDetail()
    {
        if (_filteredRecipes.Count == 0)
        {
            _detailName.Text = "";
            _detailDescription.Text = "No recipes in this category.";
            ClearMaterialsList();
            _outputLabel.Text = "";
            _craftButton.Text = "";
            _pinLabel.Text = "";
            return;
        }

        var recipe = _filteredRecipes[_selectedRecipeIndex];
        _detailName.Text = recipe.Name;
        _detailDescription.Text = recipe.Description;

        ClearMaterialsList();
        foreach (var (materialId, needed) in recipe.Materials)
        {
            int have = _party.Inventory.TryGetValue(materialId, out int h) ? h : 0;
            string status = have >= needed ? "OK" : "!!";
            var label = new Label { Text = $"  {materialId}: {have}/{needed} {status}" };
            _materialsList.AddChild(label);
        }

        _outputLabel.Text = $"Output: {recipe.OutputItemId} x{recipe.OutputCount}";
        UpdateBatchLabel();

        bool canCraft = _craftingSystem.CanCraft(recipe, _party);
        _craftButton.Text = canCraft ? "> [Craft]" : "  [Cannot craft]";

        bool isPinned = PinnedRecipeId == recipe.Id;
        _pinLabel.Text = isPinned ? "  (T to unpin)" : "  (T to pin tracker)";
    }

    private void UpdateBatchLabel()
    {
        int amount = _batchAmounts[_batchIndex];
        _batchLabel.Text = amount == -1 ? "Max" : $"x{amount}";
    }

    private void ClearMaterialsList()
    {
        var children = _materialsList.Visual.Children;
        for (int i = children.Count - 1; i >= 0; i--)
            children.RemoveAt(i);
    }
}
