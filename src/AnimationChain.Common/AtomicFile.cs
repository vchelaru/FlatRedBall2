using System;
using System.IO;
using System.Text;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// Writes a file crash-safely: the write runs against a temp file next to the target, which is
/// only then moved into place with <see cref="File.Move(string, string, bool)"/>. A reader (or a
/// crash) mid-write can only ever see the old complete file or the new complete file, never a
/// truncated one -- unlike <c>File.Create</c>/<c>File.WriteAllText</c> direct to the target path,
/// which truncates it before the new content is written.
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// Runs <paramref name="writeAction"/> against a temp file beside <paramref name="path"/>,
    /// then atomically moves it over <paramref name="path"/>. If <paramref name="writeAction"/>
    /// throws, the temp file is deleted and <paramref name="path"/> is left completely untouched.
    /// The temp file is created in the same directory as <paramref name="path"/> so the final
    /// move is a same-volume rename, not a cross-volume copy.
    /// </summary>
    public static void Write(string path, Action<Stream> writeAction)
    {
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            {
                writeAction(stream);
            }
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// <see cref="Write"/> for UTF-8 text. A file already at <paramref name="path"/> keeps its line ending (its first
    /// line's) and whether it ends in a newline; a missing or single-line file gets exactly what
    /// <paramref name="writeAction"/> writes, which must not end in a newline.
    /// </summary>
    internal static void WriteKeepingLineEndings(string path, Action<Stream> writeAction)
    {
        string old = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        int firstBreak = old.IndexOf('\n');
        if (firstBreak < 0)
        {
            Write(path, writeAction);
            return;
        }

        string newLine = firstBreak > 0 && old[firstBreak - 1] == '\r' ? "\r\n" : "\n";
        using var buffer = new MemoryStream();
        writeAction(buffer);
        // Not ReplaceLineEndings: it also replaces U+0085, U+2028 and form feeds, which are data here.
        string text = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length)
            .Replace("\r\n", "\n").Replace("\n", newLine);
        if (old.EndsWith('\n')) text += newLine;
        Write(path, stream => stream.Write(Encoding.UTF8.GetBytes(text)));
    }
}
