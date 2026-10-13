using System.Collections.ObjectModel;
using System.Linq;

namespace FlatRedBall2.Glue;

/// <summary>
/// A live list of <see cref="GlueEntity"/> that a loaded screen or entity owns: the counterpart of
/// FRB1's <c>PositionedObjectList</c> field.
/// </summary>
/// <remarks>
/// Game code adds and removes members directly. A member that is destroyed leaves every list it is
/// in, however it got there, so a collision relationship bound to the list never collects a corpse.
/// Get one from <see cref="GlueScreen.GetList"/> or <see cref="GlueEntity.GetList"/>; to have spawns
/// join it automatically, register it with <see cref="GlueProject.AddList"/>.
/// </remarks>
public sealed class GlueEntityList : Collection<GlueEntity>
{
    /// <inheritdoc />
    protected override void InsertItem(int index, GlueEntity item)
    {
        base.InsertItem(index, item);
        item.NoteJoined(this);
    }

    /// <inheritdoc />
    protected override void SetItem(int index, GlueEntity item)
    {
        var previous = this[index];
        base.SetItem(index, item);
        previous.NoteLeft(this);
        item.NoteJoined(this);
    }

    /// <inheritdoc />
    protected override void RemoveItem(int index)
    {
        var previous = this[index];
        base.RemoveItem(index);
        previous.NoteLeft(this);
    }

    /// <inheritdoc />
    protected override void ClearItems()
    {
        var previous = this.ToList();
        base.ClearItems();

        foreach (var member in previous)
            member.NoteLeft(this);
    }
}
