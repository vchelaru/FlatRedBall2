using FlatRedBall2.IO;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.IO;

// Covers the CSV dialect FlatRedBall's tooling produces: typed headers, a required key column,
// comments, and rows that do not match the header width.
public class CsvTableTests
{
    [Fact]
    public void Parse_HeaderWithTypeAndRequired_ReadsBothInEitherOrder()
    {
        // Both orders occur: one generator hardcodes "(string, required)" and another splices the
        // reflected type name in, producing "(System.String, required)".
        var table = CsvTable.Parse("\"Name (string, required)\",Speed (float)\nFast,100\n");

        table.Headers[0].Name.ShouldBe("Name");
        table.Headers[0].Type.ShouldBe("string");
        table.Headers[0].IsRequired.ShouldBeTrue();
        table.KeyHeader!.Value.Name.ShouldBe("Name");

        CsvHeader.Parse("Name (required, System.String)").Type.ShouldBe("System.String");
        CsvHeader.Parse("Name (required, System.String)").IsRequired.ShouldBeTrue();
    }

    [Fact]
    public void Parse_HeaderWithSpaces_StripsThemFromTheMemberName()
    {
        CsvTable.Parse("Max HP (int)\n5\n").Headers[0].Name.ShouldBe("MaxHP");
    }

    [Fact]
    public void Parse_CommentedAndEmptyRows_AreSkipped()
    {
        string csv = "Name (string, required),Speed (float)\n" +
                     "# a whole-line comment\n" +
                     "//CommentedOut,1\n" +
                     ",\n" +
                     "Real,2\n";

        var table = CsvTable.Parse(csv);

        table.Rows.Count.ShouldBe(1);
        table.Value(table.Rows[0], "Name").ShouldBe("Real");
    }

    [Fact]
    public void Parse_RowNarrowerOrWiderThanTheHeader_IsPaddedOrTruncated()
    {
        var table = CsvTable.Parse("A (int),B (int),C (int)\n1\n1,2,3,4\n");

        table.Rows[0].Count.ShouldBe(3);
        table.Rows[0][2].ShouldBe("");
        table.Rows[1].Count.ShouldBe(3);
    }

    [Fact]
    public void Parse_QuotedFieldWithCommaAndEscapedQuote_ReadsAsOneValue()
    {
        var table = CsvTable.Parse("A (string),B (string)\n\"one, two\",\"say \"\"hi\"\"\"\n");

        table.Rows[0][0].ShouldBe("one, two");
        table.Rows[0][1].ShouldBe("say \"hi\"");
    }

    [Fact]
    public void Float_AbsentOrUnparseableValue_ReturnsTheFallback()
    {
        var table = CsvTable.Parse("A (float)\n\n");

        table.Float(new[] { "" }, "A", 16f).ShouldBe(16f);
        table.Float(new[] { "" }, "Missing", 3f).ShouldBe(3f);
    }
}
