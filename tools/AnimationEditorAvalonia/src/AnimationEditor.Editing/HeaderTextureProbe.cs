namespace AnimationEditor.Editing;

/// <summary>
/// Decides whether a file is an image by its magic bytes (PNG, JPEG, GIF, BMP), for callers with no image
/// decoder. It cannot catch a truncated body, only a file that is not an image at all.
/// </summary>
public sealed class HeaderTextureProbe : ITextureProbe
{
    private static readonly byte[][] Signatures =
    [
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], // PNG
        [0xFF, 0xD8, 0xFF],                                // JPEG
        [0x47, 0x49, 0x46, 0x38],                          // GIF8
        [0x42, 0x4D],                                      // BM
    ];

    public bool CanDecode(string absolutePath)
    {
        try
        {
            var head = new byte[8];
            using var stream = File.OpenRead(absolutePath);
            int read = stream.Read(head, 0, head.Length);
            return Signatures.Any(s => read >= s.Length && head.AsSpan(0, s.Length).SequenceEqual(s));
        }
        catch (IOException)
        {
            return false;
        }
    }
}
