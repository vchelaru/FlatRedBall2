namespace FlatRedBall2.Glue.Model;

/// <summary>
/// What a <see cref="NamedObjectSave"/> is built from.
/// </summary>
/// <remarks>
/// Values are pinned to FRB1's <c>SourceType</c> (declared with implicit ordinals in
/// <c>NamedObjectSave.cs</c>). Glue serializes enums as bare ints with no string converter, so these
/// numbers are the on-disk format: inserting or reordering a member silently misreads every project.
/// Append only.
/// </remarks>
public enum SourceType
{
    /// <summary>Built from a <see cref="ReferencedFileSave"/> — an asset on disk.</summary>
    File = 0,

    /// <summary>An instance of another Glue entity.</summary>
    Entity = 1,

    /// <summary>An engine-native type such as a Sprite or a shape.</summary>
    FlatRedBallType = 2,

    /// <summary>A Gum runtime type.</summary>
    Gum = 3,
}
