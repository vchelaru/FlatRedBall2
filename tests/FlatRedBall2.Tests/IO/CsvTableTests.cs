using FlatRedBall2.IO;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.IO;

// A type belonging to this assembly, standing in for a game's own compiled type that a CSV header
// names by fully-qualified name (e.g. "WeaponUpgradeType (MyGame.Types.WeaponUpgradeType)").
internal enum TestUpgradeType
{
    FireRate,
    BulletSpeed,
}

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

    [Fact]
    public void ResolveSchema_TypedRequiredAndBuiltinHeaders_ReturnsHeaderAndResolvedTypeForEach()
    {
        // This is the schema step ToDictionary builds on, and the seam a future per-CSV codegen
        // step (generating a real class instead of a runtime CsvRow) would reuse instead of
        // re-parsing headers itself.
        var table = CsvTable.Parse("\"Name (string, required)\",Speed (float)\nFast,100\n");

        var columns = table.ResolveSchema();

        columns[0].Header.Name.ShouldBe("Name");
        columns[0].Header.IsRequired.ShouldBeTrue();
        columns[0].Type.ShouldBe(typeof(string));
        columns[1].Header.Name.ShouldBe("Speed");
        columns[1].Type.ShouldBe(typeof(float));
    }

    [Fact]
    public void ToDictionary_BuiltinTypedColumns_ConvertsEachCellToItsDeclaredType()
    {
        string csv = "\"Name (string, required)\",IsUnlock (bool),Bonus (float?)\n" +
                     "UnlockFireball,TRUE,\n" +
                     "FireRateBoost,FALSE,0.04\n";

        var dictionary = CsvTable.Parse(csv).ToDictionary();

        dictionary["UnlockFireball"].Get<bool>("IsUnlock").ShouldBeTrue();
        dictionary["UnlockFireball"].Get<float?>("Bonus").ShouldBeNull();
        dictionary["FireRateBoost"].Get<float?>("Bonus").ShouldBe(0.04f);
    }

    [Fact]
    public void ToDictionary_ColumnTypeCannotBeResolved_KeepsRawTextAndReportsTheColumn()
    {
        string csv = "\"Name (string, required)\",Upgrade (Nonexistent.Made.Up.Type)\n" +
                     "Row1,SomeValue\n";
        CsvHeader? unresolved = null;

        var dictionary = CsvTable.Parse(csv).ToDictionary(header => unresolved = header);

        unresolved!.Value.Name.ShouldBe("Upgrade");
        dictionary["Row1"].Get<string>("Upgrade").ShouldBe("SomeValue");
    }

    [Fact]
    public void ToDictionary_ColumnTypeIsAnEnumInAnotherAssembly_ResolvesItByReflection()
    {
        // Stands in for a header naming a type in the game's own compiled assembly, which this
        // library can never reference directly.
        string csv = $"\"Name (string, required)\",Upgrade ({typeof(TestUpgradeType).FullName})\n" +
                     "Row1,FireRate\n";

        var dictionary = CsvTable.Parse(csv).ToDictionary();

        dictionary["Row1"].Get<TestUpgradeType>("Upgrade").ShouldBe(TestUpgradeType.FireRate);
    }

    [Fact]
    public void ToDictionary_CommentRow_IsNotIncluded()
    {
        string csv = "\"Name (string, required)\",Value (int)\n" +
                     "// a comment row,1\n" +
                     "Real,2\n";

        var dictionary = CsvTable.Parse(csv).ToDictionary();

        dictionary.Count.ShouldBe(1);
        dictionary["Real"].Get<int>("Value").ShouldBe(2);
    }

    [Fact]
    public void ToDictionary_KeyedByRequiredColumn_UsesItsValueAsTheDictionaryKey()
    {
        var dictionary = CsvTable.Parse("Other (int),\"Name (string, required)\"\n1,Foo\n").ToDictionary();

        dictionary.ShouldContainKey("Foo");
        dictionary["Foo"].Get<int>("Other").ShouldBe(1);
    }
}
