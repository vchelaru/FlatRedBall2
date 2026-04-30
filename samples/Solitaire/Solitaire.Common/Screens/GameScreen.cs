using System;
using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Solitaire.Cards;
using Solitaire.Entities;

namespace Solitaire.Screens;

public class GameScreen : Screen
{
    // Behavior — not layout — so it stays in code:
    // how far each subsequent card is offset down within a tableau column.
    private const float FaceDownOffset = 12f;
    private const float FaceUpOffset = 28f;

    private GameScreenGum _gum = null!;
    private GameState _state = null!;
    private Factory<CardEntity> _cardFactory = null!;

    public override void CustomInitialize()
    {
        _gum = new GameScreenGum();
        Add(_gum);

        _cardFactory = new Factory<CardEntity>(this);
        _state = new GameState();
        _state.Deal(new Random());

        LayOutCards();
    }

    private void LayOutCards()
    {
        for (int i = 0; i < _state.Stock.Count; i++)
        {
            SpawnCardAt(_state.Stock.Cards[i], _gum.StockSlot);
        }

        for (int i = 0; i < _state.Waste.Count; i++)
        {
            SpawnCardAt(_state.Waste.Cards[i], _gum.WasteSlot);
        }

        var foundationSlots = new FrameworkElement[]
        {
            _gum.Foundation0, _gum.Foundation1, _gum.Foundation2, _gum.Foundation3,
        };
        for (int f = 0; f < _state.Foundations.Length; f++)
        {
            var pile = _state.Foundations[f];
            for (int i = 0; i < pile.Count; i++)
            {
                SpawnCardAt(pile.Cards[i], foundationSlots[f]);
            }
        }

        var tableauSlots = new GraphicalUiElement[]
        {
            _gum.Tableau0, _gum.Tableau1, _gum.Tableau2, _gum.Tableau3,
            _gum.Tableau4, _gum.Tableau5, _gum.Tableau6,
        };
        for (int col = 0; col < _state.Tableaus.Length; col++)
        {
            var pile = _state.Tableaus[col];
            var (worldX, startY) = SlotWorldCenter(tableauSlots[col]);
            float worldY = startY;
            for (int i = 0; i < pile.Count; i++)
            {
                var card = pile.Cards[i];
                SpawnCardAt(card, worldX, worldY);
                // World Y+ is up; stacking visually downward means decreasing Y.
                worldY -= card.IsFaceUp ? FaceUpOffset : FaceDownOffset;
            }
        }
    }

    // Slot positions live in the Gum project. They may be nested under containers
    // (FoundationContainer, etc.), so AbsoluteLeft/AbsoluteTop walk the parent
    // chain — local X/Y would be relative to the immediate parent and place
    // cards at the wrong spot. Convert (canvas pixels, Y+ down, top-left origin)
    // into world coords (camera ortho, Y+ up, centered) before spawning.
    private (float worldX, float worldY) SlotWorldCenter(GraphicalUiElement slot)
    {
        float canvasX = slot.AbsoluteLeft + slot.GetAbsoluteWidth() / 2f;
        float canvasY = slot.AbsoluteTop + slot.GetAbsoluteHeight() / 2f;
        float worldX = canvasX - Camera.OrthogonalWidth / 2f;
        float worldY = Camera.OrthogonalHeight / 2f - canvasY;
        return (worldX, worldY);
    }

    private void SpawnCardAt(Card card, FrameworkElement slot)
    {
        var (x, y) = SlotWorldCenter(slot.Visual);
        SpawnCardAt(card, x, y);
    }

    private void SpawnCardAt(Card card, GraphicalUiElement slot)
    {
        var (x, y) = SlotWorldCenter(slot);
        SpawnCardAt(card, x, y);
    }

    private void SpawnCardAt(Card card, float worldX, float worldY)
    {
        var entity = _cardFactory.Create(e => e.Model = card);
        entity.X = worldX;
        entity.Y = worldY;
    }
}
