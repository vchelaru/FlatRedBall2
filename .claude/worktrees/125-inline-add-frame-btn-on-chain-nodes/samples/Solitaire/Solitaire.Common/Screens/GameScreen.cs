using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlatRedBall2;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;
using System.Numerics;
using Solitaire.Cards;
using Solitaire.Entities;
using MonoGameGum;
using MonoGameGum.Input;

namespace Solitaire.Screens;

public class GameScreen : Screen
{
    private const float FaceDownOffset = 12f;
    private const float FaceUpOffset = 30f;

    // The waste pile fans the cards from the most recent stock draw to the
    // right; older waste cards collapse under the leftmost slot. Tracking the
    // visible batch (rather than always fanning the top N) means moving a
    // card off the waste shrinks the fan instead of pulling a hidden older
    // card up into view.
    private const int WasteFanWindow = 3;
    private const float WasteFanXStep = 22f;
    private int _visibleWasteCount;

    // Z layering — keeps later-stacked cards on top of earlier ones.
    private const float StackZStep = 0.01f;
    private const float DragLiftZ = 100f;

    // Card-slide tween duration. Short enough not to feel sluggish during a fast play
    // session, long enough to read where a card came from.
    private static readonly TimeSpan CardMoveDuration = TimeSpan.FromMilliseconds(160);
    // Per-card delay between successive stock→waste tweens — staggers the deal so the
    // three cards arrive one-by-one rather than as a single rigid block.
    private static readonly TimeSpan StockDrawStagger = TimeSpan.FromMilliseconds(70);

    private GameScreenGum _gum = null!;
    private GameState _state = null!;
    private Factory<CardEntity> _cardFactory = null!;
    private readonly Dictionary<Card, CardEntity> _entityFor = new();
    private FrameworkElement[] _foundationSlots = null!;
    private GraphicalUiElement[] _tableauSlots = null!;

    // Drag state
    private List<CardEntity> _dragRun = new();
    private Pile? _dragSourcePile;
    private float _dragOffsetX;
    private float _dragOffsetY;

    // Double-click tracking — second press on the same card within the window
    // sends the card to the first legal foundation.
    private const float DoubleClickWindow = 0.4f;
    private double _lastClickTime = double.NegativeInfinity;
    private CardEntity? _lastClickedEntity;

    // Win overlay — the authored WinOverlayGum component. DimBackground has HasEvents=true
    // so Gum input is absorbed by the overlay; CustomActivity also early-returns while it's
    // visible so the entity-attached card layer (not in Gum's input tree) is gated too.
    private Layer _topUiLayer = null!;
    private Solitaire.Components.WinOverlayGum _winOverlay = null!;

    public override void CustomInitialize()
    {
        _gum = new GameScreenGum();
        Add(_gum);

        // Tag each foundation slot with the suit it accepts so the placeholder
        // shows the expected target. Order matches GameState.Foundations:
        // Spades, Hearts, Clubs, Diamonds.
        _gum.Foundation0.SuitState = Solitaire.Components.FoundationSlot.Suit.Spades;
        _gum.Foundation1.SuitState = Solitaire.Components.FoundationSlot.Suit.Hearts;
        _gum.Foundation2.SuitState = Solitaire.Components.FoundationSlot.Suit.Clubs;
        _gum.Foundation3.SuitState = Solitaire.Components.FoundationSlot.Suit.Diamonds;

        _foundationSlots = new FrameworkElement[]
        {
            _gum.Foundation0, _gum.Foundation1, _gum.Foundation2, _gum.Foundation3,
        };
        _tableauSlots = new GraphicalUiElement[]
        {
            _gum.Tableau0, _gum.Tableau1, _gum.Tableau2, _gum.Tableau3,
            _gum.Tableau4, _gum.Tableau5, _gum.Tableau6,
        };

        _gum.RestartGameButton.Click += (_, _) => StartNewGame();

        _cardFactory = new Factory<CardEntity>(this);
        _state = new GameState();
        _state.Deal(new Random());

        SpawnAllCardEntities();
        RebuildVisuals();

        BuildWinOverlay();

        // Iteration aid: any change under Content/ restarts the screen so updated
        // textures, animations, and tilemaps pick up immediately. Same-dimension PNG
        // edits patch the live Texture2D in-place via Engine.Content.TryReload and skip
        // the restart automatically.
        WatchContentDirectory("Content", _ => RestartScreen(RestartMode.HotReload));

        // Gum project edits (.gucx/.gusx/.gumx) flow through Gum's own hot-reload
        // pipeline, which DOES reach entity-attached card visuals via Screen.EntityVisualsRoot —
        // the visuals get patched in place. The remaining problem: Gum's patcher reassigns
        // every property on each live instance from the file's defaults, including properties
        // the game has mutated at runtime. That clobbers card positions (inner instance
        // layout) and the win overlay's runtime IsVisible = false. Restart-on-reload sidesteps
        // it by rebuilding from a clean slate.
        //
        // The proper fix is per-property dirty tracking in Gum's runtime — when game code
        // writes to a property, mark it user-set; the patcher skips dirty properties. That's
        // a meaningful chunk of Gum work, deferred for now.
        Engine.GumHotReloadCompleted += HandleGumHotReloadCompleted;
    }

    private void BuildWinOverlay()
    {
        _topUiLayer = new Layer("TopUI");
        Layers.Add(_topUiLayer);

        _winOverlay = new Solitaire.Components.WinOverlayGum();
        _winOverlay.ButtonStandardInstance.Click += (_, _) => StartNewGame();

        Add(_winOverlay, _topUiLayer);
        _winOverlay.IsVisible = false;
    }

    private void StartNewGame()
    {
        // Tear down the existing 52 entities — Deal() builds a fresh deck of
        // new Card instances, so the entity-to-card map must be rebuilt.
        foreach (var entity in _cardFactory.Instances.ToArray())
        {
            entity.Destroy();
        }
        _entityFor.Clear();
        _dragRun.Clear();
        _dragSourcePile = null;
        _lastClickedEntity = null;
        _visibleWasteCount = 0;

        _state.Deal(new Random());

        SpawnAllCardEntities();
        RebuildVisuals();
        _winOverlay.IsVisible = false;
    }

    public override void CustomDestroy()
    {
        Engine.GumHotReloadCompleted -= HandleGumHotReloadCompleted;
    }

    private void HandleGumHotReloadCompleted()
    {
        RestartScreen(RestartMode.HotReload);
    }

    public override void CustomActivity(FrameTime time)
    {
        // The win overlay's New Game button is processed by Forms input
        // independently of CustomActivity; suspending gameplay input while
        // it's visible prevents the underlying cards from picking up clicks
        // that pass through transparent regions of the panel.
        if (_winOverlay.IsVisible)
        {
            return;
        }

        var cursor = Engine.Input.Cursor;
        var world = cursor.WorldPosition;

        if (cursor.PrimaryPressed)
        {
            HandlePrimaryPressed(world, time.SinceGameStart.TotalSeconds);
        }
        else if (cursor.PrimaryDown && _dragRun.Count > 0)
        {
            UpdateDrag(world);
        }
        else if (!cursor.PrimaryDown && _dragRun.Count > 0)
        {
            EndDrag(world);
        }

        if (_state.IsWon)
        {
            _winOverlay.IsVisible = true;
        }

#if DEBUG
        // Debug shortcut: Ctrl+W forces the win overlay so the win screen can be exercised
        // without dealing through 52 cards. Excluded from release builds.
        var keyboard = Engine.Input.Keyboard;
        if (keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.W)
            && (keyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl)
                || keyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl)))
        {
            _winOverlay.IsVisible = true;
        }
#endif
    }

    // --- Initial spawn ----------------------------------------------------------------

    private void SpawnAllCardEntities()
    {
        foreach (var pile in EnumeratePiles())
        {
            foreach (var card in pile.Cards)
            {
                var entity = _cardFactory.Create(e => e.Model = card);
                _entityFor[card] = entity;
            }
        }
    }

    private IEnumerable<Pile> EnumeratePiles()
    {
        yield return _state.Stock;
        yield return _state.Waste;
        foreach (var f in _state.Foundations) yield return f;
        foreach (var t in _state.Tableaus) yield return t;
    }

    // --- Visual layout ----------------------------------------------------------------

    private void RebuildVisuals(bool animate = false, HashSet<Card>? skipCards = null)
    {
        // Cards stacked at the same position would compound their antialiased edges into a
        // hard aliased outline. For piles that pin every card on top of the previous one
        // (stock, foundation, the collapsed portion of waste) only the topmost card draws;
        // tableau and the visible waste fan offset their cards visibly so every card draws.

        bool IsLayoutSuppressed(Card c) => IsBeingDragged(c) || (skipCards?.Contains(c) ?? false);

        // Stock — all stacked at slot center, only the topmost non-dragged card visible.
        int stockVisibleTop = TopVisibleIndex(_state.Stock);
        for (int i = 0; i < _state.Stock.Count; i++)
        {
            var card = _state.Stock.Cards[i];
            if (IsLayoutSuppressed(card)) continue;
            var (x, y) = SlotWorldCenter(_gum.StockSlot);
            PlaceCard(card, x, y, i * StackZStep, animate, isVisible: i == stockVisibleTop);
        }

        int wasteCount = _state.Waste.Count;
        // The visible batch is the cards from the most recent draw that
        // haven't been moved away yet. Cards below the batch collapse to
        // the leftmost slot.
        int visible = Math.Min(_visibleWasteCount, wasteCount);
        int collapsedCount = wasteCount - visible;
        // Collapsed-top is the topmost non-dragged card in the collapsed range. Static
        // i == collapsedCount - 1 picks the dragged card when it is itself the topmost
        // collapsed, leaving the slot empty during the drag.
        int collapsedVisibleTop = -1;
        for (int j = collapsedCount - 1; j >= 0; j--)
        {
            if (!IsLayoutSuppressed(_state.Waste.Cards[j])) { collapsedVisibleTop = j; break; }
        }
        // The leftmost fan card sits at the same X as the collapsed pile (fanIndex 0).
        // When it's present and not being dragged it fully covers the collapsed top, so
        // suppress the collapsed-top to avoid two cards alpha-stacking at the same spot.
        bool leftmostFanCovers = collapsedCount < wasteCount
            && !IsLayoutSuppressed(_state.Waste.Cards[collapsedCount]);
        for (int i = 0; i < wasteCount; i++)
        {
            var card = _state.Waste.Cards[i];
            if (IsLayoutSuppressed(card)) continue;
            var (cx, cy) = SlotWorldCenter(_gum.WasteSlot);
            int fanIndex = Math.Max(0, i - collapsedCount);
            bool isCollapsedTop = i == collapsedVisibleTop && !leftmostFanCovers;
            bool isInFan = i >= collapsedCount;
            PlaceCard(card, cx + fanIndex * WasteFanXStep, cy, i * StackZStep, animate,
                isVisible: isInFan || isCollapsedTop);
        }

        for (int f = 0; f < _state.Foundations.Length; f++)
        {
            var pile = _state.Foundations[f];
            // Topmost non-dragged: when the pile's top card is being dragged off, the card
            // beneath it should reveal during the drag — not snap into view at EndDrag.
            int visibleTop = TopVisibleIndex(pile);
            var (x, y) = SlotWorldCenter(_foundationSlots[f].Visual);
            for (int i = 0; i < pile.Count; i++)
            {
                var card = pile.Cards[i];
                if (IsLayoutSuppressed(card)) continue;
                PlaceCard(card, x, y, i * StackZStep, animate, isVisible: i == visibleTop);
            }
        }

        for (int col = 0; col < _state.Tableaus.Length; col++)
        {
            var pile = _state.Tableaus[col];
            var (x, startY) = SlotWorldCenter(_tableauSlots[col]);
            float y = startY;
            for (int i = 0; i < pile.Count; i++)
            {
                var card = pile.Cards[i];
                if (!IsLayoutSuppressed(card))
                {
                    PlaceCard(card, x, y, i * StackZStep, animate, isVisible: true);
                }
                y -= card.IsFaceUp ? FaceUpOffset : FaceDownOffset;
            }
        }
    }

    private void PlaceCard(Card card, float x, float y, float z, bool animate, bool isVisible)
    {
        var entity = _entityFor[card];
        if (isVisible)
        {
            entity.CancelPendingHide();
            entity.IsVisible = true;
        }
        else if (animate && entity.IsVisible)
        {
            // A card going visible→hidden during an animated rebuild is almost always being
            // covered by an incoming card whose tween hasn't completed yet (e.g. a card
            // sliding to the foundation; the previous foundation top should stay onscreen
            // until the new card has arrived to cover it). Defer the hide by the tween
            // duration so the slot never blanks mid-flight.
            entity.HideAfter(CardMoveDuration);
        }
        else
        {
            entity.CancelPendingHide();
            entity.IsVisible = false;
        }
        if (animate)
        {
            entity.TweenMoveTo(x, y, z, CardMoveDuration);
        }
        else
        {
            entity.X = x;
            entity.Y = y;
            entity.Z = z;
        }
        // Don't re-assign Model on every reposition — that would re-apply all
        // three Gum state categories on every frame's RebuildVisuals and may
        // not be a no-op at the runtime layer (asset reloads, etc.).
        // Only update facing if it diverged from the model.
        entity.RefreshFacing();
    }

    private bool IsBeingDragged(Card card) => _dragRun.Exists(e => e.Model == card);

    // Fires off the per-card stock→waste tweens with a small stagger so the three cards
    // arrive one-by-one. Each card sits at its stock position until its tween starts —
    // RebuildVisuals already skipped these cards via skipCards.
    private async Task StaggerStockDrawAsync(List<Card> drawnCards)
    {
        int wasteCount = _state.Waste.Count;
        int collapsedCount = wasteCount - _visibleWasteCount;
        var (cx, cy) = SlotWorldCenter(_gum.WasteSlot);

        for (int k = 0; k < drawnCards.Count; k++)
        {
            if (k > 0)
            {
                try { await Engine.Time.Delay(StockDrawStagger); }
                catch (TaskCanceledException) { return; }
            }

            var card = drawnCards[k];
            // The drawn cards occupy the top `drawnCards.Count` slots of the waste pile.
            int pileIndex = wasteCount - drawnCards.Count + k;
            int fanIndex = Math.Max(0, pileIndex - collapsedCount);
            var entity = _entityFor[card];
            entity.CancelPendingHide();
            entity.IsVisible = true;
            entity.RefreshFacing();
            entity.TweenMoveTo(cx + fanIndex * WasteFanXStep, cy, pileIndex * StackZStep, CardMoveDuration);
        }
    }

    // Highest pile index whose card isn't currently being dragged. Returns -1 if every
    // card in the pile is dragged (in which case nothing in the pile draws).
    private int TopVisibleIndex(Pile pile)
    {
        for (int i = pile.Count - 1; i >= 0; i--)
        {
            if (!IsBeingDragged(pile.Cards[i])) return i;
        }
        return -1;
    }

    private (float worldX, float worldY) SlotWorldCenter(GraphicalUiElement slot)
    {
        float canvasX = slot.AbsoluteLeft + slot.GetAbsoluteWidth() / 2f;
        float canvasY = slot.AbsoluteTop + slot.GetAbsoluteHeight() / 2f;
        float worldX = canvasX - Camera.OrthogonalWidth / 2f;
        float worldY = Camera.OrthogonalHeight / 2f - canvasY;
        return (worldX, worldY);
    }

    // --- Input handling ---------------------------------------------------------------

    private void HandlePrimaryPressed(Vector2 world, double nowSeconds)
    {
        // Stock click: draw three (or recycle when empty).
        if (CursorOver(_gum.StockSlot, world))
        {
            int wasteBefore = _state.Waste.Count;
            bool isRecycle = _state.Stock.IsEmpty;
            _state.DrawThree();
            // After a recycle, the waste is empty; after a draw, the visible
            // batch is exactly the number of cards that just moved to waste.
            _visibleWasteCount = isRecycle ? 0 : _state.Waste.Count - wasteBefore;
            _lastClickedEntity = null;

            if (isRecycle || _visibleWasteCount == 0)
            {
                RebuildVisuals(animate: true);
            }
            else
            {
                // Stagger the just-drawn cards: rebuild everything else first (skipping
                // the new arrivals so they hold their stock position), then fly each one
                // to the waste with a small per-card delay for that one-by-one feel.
                var drawnCards = new List<Card>(_visibleWasteCount);
                for (int i = wasteBefore; i < _state.Waste.Count; i++)
                {
                    drawnCards.Add(_state.Waste.Cards[i]);
                }
                var skipSet = new HashSet<Card>(drawnCards);
                RebuildVisuals(animate: true, skipCards: skipSet);
                _ = StaggerStockDrawAsync(drawnCards);
            }
            return;
        }

        var hit = TopmostCardUnder(world);
        if (hit?.Model is { IsFaceUp: true } card)
        {
            bool isDoubleClick = ReferenceEquals(hit, _lastClickedEntity)
                && nowSeconds - _lastClickTime <= DoubleClickWindow;

            _lastClickTime = nowSeconds;
            _lastClickedEntity = hit;

            if (isDoubleClick && TryAutoFoundation(card))
            {
                _lastClickedEntity = null; // prevent triple-click chain
                RebuildVisuals(animate: true);
                return;
            }

            BeginDrag(card, world);
        }
        else
        {
            _lastClickedEntity = null;
        }
    }

    /// <summary>
    /// Sends <paramref name="card"/> to the first legal foundation pile.
    /// Returns true if the move was committed. Only the top card of its
    /// source pile is eligible — auto-promotion never reaches into a stack.
    /// </summary>
    private bool TryAutoFoundation(Card card)
    {
        var source = FindPileOf(card);
        if (source is null || source.Top != card || !card.IsFaceUp) return false;

        foreach (var foundation in _state.Foundations)
        {
            if (Rules.CanPlaceOnFoundation(card, foundation))
            {
                bool fromWaste = source is WastePile;
                source.Pop();
                foundation.Push(card);
                if (fromWaste) _visibleWasteCount = Math.Max(0, _visibleWasteCount - 1);
                if (source is TableauPile t && t.Top is { IsFaceUp: false } newTop)
                    newTop.IsFaceUp = true;
                return true;
            }
        }
        return false;
    }

    private CardEntity? TopmostCardUnder(Vector2 world)
    {
        CardEntity? best = null;
        float bestZ = float.NegativeInfinity;
        foreach (var entity in _cardFactory.Instances)
        {
            if (Engine.Input.Cursor.IsOver(entity) && entity.Z > bestZ)
            {
                best = entity;
                bestZ = entity.Z;
            }
        }
        return best;
    }

    private bool CursorOver(GraphicalUiElement slot, Vector2 world)
    {
        var (cx, cy) = SlotWorldCenter(slot);
        float halfW = slot.GetAbsoluteWidth() / 2f;
        float halfH = slot.GetAbsoluteHeight() / 2f;
        return world.X >= cx - halfW && world.X <= cx + halfW
            && world.Y >= cy - halfH && world.Y <= cy + halfH;
    }

    // --- Drag/drop --------------------------------------------------------------------

    private void BeginDrag(Card root, Vector2 world)
    {
        _dragSourcePile = FindPileOf(root);
        if (_dragSourcePile is null) return;

        // Drag run = picked card + everything stacked above it (only legal for tableau).
        // Foundation/waste only drag the single top card.
        int rootIndex = -1;
        for (int i = 0; i < _dragSourcePile.Count; i++)
        {
            if (_dragSourcePile.Cards[i] == root) { rootIndex = i; break; }
        }
        if (rootIndex < 0) return;

        if (_dragSourcePile is TableauPile)
        {
            for (int i = rootIndex; i < _dragSourcePile.Count; i++)
            {
                _dragRun.Add(_entityFor[_dragSourcePile.Cards[i]]);
            }
        }
        else
        {
            // Only drag the top card from waste / foundation.
            if (rootIndex != _dragSourcePile.Count - 1) return;
            _dragRun.Add(_entityFor[root]);
        }

        var pickedEntity = _entityFor[root];
        _dragOffsetX = pickedEntity.X - world.X;
        _dragOffsetY = pickedEntity.Y - world.Y;

        // Lift Z so dragged cards render above everything else.
        for (int i = 0; i < _dragRun.Count; i++)
        {
            _dragRun[i].Z = DragLiftZ + i * StackZStep;
        }

        // Recompute visibility now that one or more cards are excluded from their source
        // pile — e.g. dragging the top of a foundation should reveal the card beneath it
        // immediately, not snap into view at EndDrag.
        RebuildVisuals(animate: false);
    }

    private void UpdateDrag(Vector2 world)
    {
        // The first card in the run anchors to the cursor (preserving grab offset);
        // subsequent cards stay at their fixed face-up offset below the anchor.
        float anchorX = world.X + _dragOffsetX;
        float anchorY = world.Y + _dragOffsetY;
        for (int i = 0; i < _dragRun.Count; i++)
        {
            _dragRun[i].X = anchorX;
            _dragRun[i].Y = anchorY - i * FaceUpOffset;
        }
    }

    private void EndDrag(Vector2 world)
    {
        var run = _dragRun;
        var source = _dragSourcePile!;
        _dragRun = new List<CardEntity>();
        _dragSourcePile = null;

        var moving = new List<Card>(run.Count);
        foreach (var e in run) moving.Add(e.Model!);

        if (TryFindLegalDrop(moving, world, out var destination))
        {
            bool fromWaste = source is WastePile;
            for (int i = 0; i < moving.Count; i++) source.Pop();
            foreach (var card in moving) destination!.Push(card);
            if (fromWaste) _visibleWasteCount = Math.Max(0, _visibleWasteCount - moving.Count);

            if (source is TableauPile tableau && tableau.Top is { IsFaceUp: false } newTop)
            {
                newTop.IsFaceUp = true;
            }
        }

        // Both legal-drop and snap-back animate: legal cards slide to destination,
        // illegal cards slide back to source.
        RebuildVisuals(animate: true);
    }

    private bool TryFindLegalDrop(List<Card> moving, Vector2 world, out Pile? destination)
    {
        destination = null;
        var root = moving[0];

        // Check foundations — only legal for a single-card move.
        if (moving.Count == 1)
        {
            for (int f = 0; f < _state.Foundations.Length; f++)
            {
                if (CursorOver(_foundationSlots[f].Visual, world)
                    && Rules.CanPlaceOnFoundation(root, _state.Foundations[f]))
                {
                    destination = _state.Foundations[f];
                    return true;
                }
            }
        }

        // Check tableaus.
        for (int col = 0; col < _state.Tableaus.Length; col++)
        {
            if (IsCursorOverTableauColumn(_tableauSlots[col], _state.Tableaus[col], world)
                && Rules.CanPlaceOnTableau(root, _state.Tableaus[col].Top))
            {
                destination = _state.Tableaus[col];
                return true;
            }
        }

        return false;
    }

    // A tableau column extends downward as cards are stacked, so the drop zone
    // is the slot rectangle plus everything below it down through the visible run.
    private bool IsCursorOverTableauColumn(GraphicalUiElement slot, TableauPile pile, Vector2 world)
    {
        var (cx, cy) = SlotWorldCenter(slot);
        float halfW = slot.GetAbsoluteWidth() / 2f;
        float halfH = slot.GetAbsoluteHeight() / 2f;

        // Compute the bottom of the visible stack.
        float bottomY = cy - halfH;
        float y = cy;
        for (int i = 0; i < pile.Count; i++)
        {
            y -= pile.Cards[i].IsFaceUp ? FaceUpOffset : FaceDownOffset;
            bottomY = y - halfH;
        }

        return world.X >= cx - halfW && world.X <= cx + halfW
            && world.Y >= bottomY && world.Y <= cy + halfH;
    }

    private Pile? FindPileOf(Card card)
    {
        if (Contains(_state.Stock, card)) return _state.Stock;
        if (Contains(_state.Waste, card)) return _state.Waste;
        foreach (var f in _state.Foundations) if (Contains(f, card)) return f;
        foreach (var t in _state.Tableaus) if (Contains(t, card)) return t;
        return null;
    }

    private static bool Contains(Pile pile, Card card)
    {
        for (int i = 0; i < pile.Count; i++)
        {
            if (pile.Cards[i] == card) return true;
        }
        return false;
    }
}
