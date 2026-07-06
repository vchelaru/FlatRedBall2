using AnimationEditor.Core.Git;
using System;
using Xunit;

namespace AnimationEditor.Core.Tests.Git;

public class GitLogParserTests
{
    private const char Rs = '\x1e';   // matches GitLogParser's record separator (git %x1e)
    private const char Fs = '\x1f';   // matches GitLogParser's field separator (git %x1f)

    // Builds one commit record in the shape `git log --name-status` emits with GitLogParser.Format.
    private static string Record(string hash, string author, string date, string subject, string nameStatusLine) =>
        $"{Rs}{hash}{Fs}{author}{Fs}{date}{Fs}{subject}\n{nameStatusLine}\n";

    [Fact]
    public void ParseLog_Empty_ReturnsEmpty()
    {
        Assert.Empty(GitLogParser.ParseLog(""));
    }

    [Fact]
    public void ParseLog_RenameCommit_UsesNewPath()
    {
        // A rename commit is "R100\told\tnew"; the file's path at this commit is the new name.
        string raw = Record("abc1234def", "Sam", "2026-07-06T14:08:55-05:00", "Rename sheet",
            "R100\tart/old.png\tart/player.png");

        var revisions = GitLogParser.ParseLog(raw);

        Assert.Single(revisions);
        Assert.Equal("art/player.png", revisions[0].PathAtCommit);
    }

    [Fact]
    public void ParseLog_ShortHash_FirstSevenChars()
    {
        string raw = Record("abcdef1234567890", "Sam", "2026-07-06T14:08:55-05:00", "Edit",
            "M\tart/player.png");

        Assert.Equal("abcdef1", GitLogParser.ParseLog(raw)[0].ShortHash);
    }

    [Fact]
    public void ParseLog_TwoCommits_ReturnsBothWithFields()
    {
        string raw =
            Record("aaaa111", "Alice", "2026-07-06T14:08:55-05:00", "Fix run frame", "M\tart/player.png") +
            Record("bbbb222", "Bob", "2026-07-01T09:00:00-05:00", "Add sheet", "A\tart/player.png");

        var revisions = GitLogParser.ParseLog(raw);

        Assert.Equal(2, revisions.Count);
        Assert.Equal("aaaa111", revisions[0].Hash);
        Assert.Equal("Alice", revisions[0].Author);
        Assert.Equal("Fix run frame", revisions[0].Subject);
        Assert.Equal("art/player.png", revisions[0].PathAtCommit);
        Assert.Equal(new DateTimeOffset(2026, 7, 6, 14, 8, 55, TimeSpan.FromHours(-5)), revisions[0].Date);
        Assert.Equal("bbbb222", revisions[1].Hash);
        Assert.Equal("Add sheet", revisions[1].Subject);
    }
}
