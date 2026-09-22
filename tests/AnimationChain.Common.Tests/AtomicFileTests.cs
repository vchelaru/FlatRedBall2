using System;
using System.IO;
using System.Text;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;

namespace AnimationEditorCommon.Tests;

// #1183: project-format saves (.achx/.achj/.tsx/.aeproperties/.tiledsync) wrote directly to
// their final path via File.Create/File.WriteAllText, which truncates the file to zero bytes
// before streaming new content -- a crash or a concurrent reader mid-write can observe an
// empty file. AtomicFile is the shared fix: write to a same-directory temp file, then
// File.Move(overwrite: true) it into place, so the target path only ever shows the old
// complete content or the new complete content, never a partial write.
public class AtomicFileTests
{
    [Fact]
    public void Write_NewFile_WritesExpectedContent()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            AtomicFile.Write(path, stream =>
            {
                var bytes = Encoding.UTF8.GetBytes("hello");
                stream.Write(bytes, 0, bytes.Length);
            });

            File.ReadAllText(path).ShouldBe("hello");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Write_Success_LeavesNoTempFileBehind()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            AtomicFile.Write(path, stream =>
            {
                var bytes = Encoding.UTF8.GetBytes("hello");
                stream.Write(bytes, 0, bytes.Length);
            });

            Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + "*")
                .ShouldBe(new[] { path });
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Write_OverwritesExistingFile_WithNewContent()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, "old content");
        try
        {
            AtomicFile.Write(path, stream =>
            {
                var bytes = Encoding.UTF8.GetBytes("new");
                stream.Write(bytes, 0, bytes.Length);
            });

            File.ReadAllText(path).ShouldBe("new");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Write_WriteActionThrows_OriginalFileIsUntouched()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, "original content");
        try
        {
            Should.Throw<InvalidOperationException>(() =>
                AtomicFile.Write(path, stream => throw new InvalidOperationException("boom")));

            // The defect this guards against: a direct File.Create/WriteAllText truncates the
            // target before the failure, leaving a zero-length file. Atomic write must leave the
            // original file completely intact when the write callback fails partway through.
            File.ReadAllText(path).ShouldBe("original content");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Write_WriteActionThrows_NoTempFileLeftBehind()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllText(path, "original content");
        try
        {
            Should.Throw<InvalidOperationException>(() =>
                AtomicFile.Write(path, stream => throw new InvalidOperationException("boom")));

            Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + "*")
                .ShouldBe(new[] { path });
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
