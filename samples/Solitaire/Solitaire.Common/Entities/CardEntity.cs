using FlatRedBall2;
using RenderingLibrary.Graphics;
using Solitaire.Cards;
using Solitaire.Components;

namespace Solitaire.Entities;

public class CardEntity : Entity
{
    private CardGum _gum = null!;
    private Card? _model;

    public Card? Model
    {
        get => _model;
        set
        {
            _model = value;
            ApplyModel();
        }
    }

    public override void CustomInitialize()
    {
        _gum = new CardGum();
        // The entity is positioned by its center, but Gum's default origin is
        // top-left. Re-anchor the visual to its center on this instance so the
        // entity's X/Y matches the visual's geometric center. Authored on the
        // instance (not the component) — centering is a consumer concern, not
        // a property of the card itself.
        _gum.Visual.XOrigin = HorizontalAlignment.Center;
        _gum.Visual.YOrigin = VerticalAlignment.Center;
        Add(_gum);
        ApplyModel();
    }

    private void ApplyModel()
    {
        if (_gum is null || _model is null) return;

        _gum.SuitState = _model.Suit switch
        {
            Suit.Spades => CardGum.Suit.Spades,
            Suit.Hearts => CardGum.Suit.Hearts,
            Suit.Clubs => CardGum.Suit.Clubs,
            Suit.Diamonds => CardGum.Suit.Diamonds,
            _ => CardGum.Suit.Spades,
        };

        _gum.RankState = (int)_model.Rank switch
        {
            1 => CardGum.Rank.Ace,
            2 => CardGum.Rank._2,
            3 => CardGum.Rank._3,
            4 => CardGum.Rank._4,
            5 => CardGum.Rank._5,
            6 => CardGum.Rank._6,
            7 => CardGum.Rank._7,
            8 => CardGum.Rank._8,
            9 => CardGum.Rank._9,
            10 => CardGum.Rank._10,
            11 => CardGum.Rank.Jack,
            12 => CardGum.Rank.Queen,
            13 => CardGum.Rank.King,
            _ => CardGum.Rank.Ace,
        };

        _gum.FacingState = _model.IsFaceUp ? CardGum.Facing.Up : CardGum.Facing.Down;
    }
}
