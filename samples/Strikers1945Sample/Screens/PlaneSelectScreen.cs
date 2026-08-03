using FlatRedBall2;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Strikers1945Sample.Entities;

namespace Strikers1945Sample.Screens;

public class PlaneSelectScreen : Screen
{
    private int _selectedIndex;
    private readonly Sprite[] _planeSprites = new Sprite[4];
    private Label _nameLabel = null!;
    private Label _descLabel = null!;
    private Label _selectLabel = null!;

    // Selected plane data passed to gameplay
    public static PlaneData SelectedPlane { get; private set; } = PlaneData.AllPlanes[0];

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(12, 14, 30);

        var title = new Label();
        title.Text = "SELECT YOUR PLANE";
        title.Anchor(Anchor.TopLeft);
        title.X = 90;
        title.Y = 60;
        Add(title);

        // Display all 4 planes in a row
        float startX = -140f;
        float spacing = 90f;
        for (int i = 0; i < 4; i++)
        {
            var plane = PlaneData.AllPlanes[i];
            var tex = Engine.ContentManager.Load<Texture2D>(plane.SpriteName);
            var sprite = new Sprite
            {
                Texture = tex,
                TextureScale = 3f,
                X = startX + i * spacing,
                Y = 80f,
            };
            Add(sprite);
            _planeSprites[i] = sprite;
        }

        _nameLabel = new Label();
        _nameLabel.Anchor(Anchor.TopLeft);
        _nameLabel.X = 120;
        _nameLabel.Y = 350;
        Add(_nameLabel);

        _descLabel = new Label();
        _descLabel.Anchor(Anchor.TopLeft);
        _descLabel.X = 40;
        _descLabel.Y = 400;
        Add(_descLabel);

        _selectLabel = new Label();
        _selectLabel.Text = "Z: Confirm  |  Arrows: Choose";
        _selectLabel.Anchor(Anchor.TopLeft);
        _selectLabel.X = 50;
        _selectLabel.Y = 500;
        Add(_selectLabel);

        UpdateSelection();
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Left) || kb.WasKeyPressed(Keys.Up))
        {
            _selectedIndex = (_selectedIndex - 1 + 4) % 4;
            UpdateSelection();
        }
        if (kb.WasKeyPressed(Keys.Right) || kb.WasKeyPressed(Keys.Down))
        {
            _selectedIndex = (_selectedIndex + 1) % 4;
            UpdateSelection();
        }
        if (kb.WasKeyPressed(Keys.Z) || kb.WasKeyPressed(Keys.Space) || kb.WasKeyPressed(Keys.Enter))
        {
            SelectedPlane = PlaneData.AllPlanes[_selectedIndex];
            MoveToScreen<GameplayScreen>();
        }
    }

    private void UpdateSelection()
    {
        var plane = PlaneData.AllPlanes[_selectedIndex];
        _nameLabel.Text = plane.Name;
        _descLabel.Text = plane.Description;

        // Highlight selected, dim others
        for (int i = 0; i < 4; i++)
        {
            _planeSprites[i].Alpha = i == _selectedIndex ? 1f : 0.3f;
            _planeSprites[i].TextureScale = i == _selectedIndex ? 4f : 3f;
        }
    }

    public override void CustomDestroy() { }
}
