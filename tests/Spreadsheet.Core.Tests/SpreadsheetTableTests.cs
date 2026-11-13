using Spreadsheet.Core.Model;
using Spreadsheet.Core.Storage;
using Xunit;

namespace Spreadsheet.Core.Tests;

public class SpreadsheetTableTests
{
    [Fact]
    public void DependentCell_IsRecalculated_WhenSourceChanges()
    {
        var table = new SpreadsheetTable(3, 3);
        table.SetExpression("A1", "5");
        table.SetExpression("B1", "A1 * 2");
        Assert.Equal(10, table.FindCell("B1")!.Value);

        table.SetExpression("A1", "7");
        Assert.Equal(14, table.FindCell("B1")!.Value);
    }

    [Fact]
    public void EmptyCell_IsTreatedAsZero()
    {
        var table = new SpreadsheetTable(3, 3);
        table.SetExpression("A1", "B1 + 4");
        Assert.Equal(4, table.FindCell("A1")!.Value);
    }

    [Fact]
    public void CyclicReference_MarksAllCellsInCycleAsErrors()
    {
        var table = new SpreadsheetTable(3, 3);
        table.SetExpression("A1", "B1 + 1");
        table.SetExpression("B1", "C1 + 1");
        table.SetExpression("C1", "A1 + 1");

        foreach (var name in new[] { "A1", "B1", "C1" })
        {
            var cell = table.FindCell(name)!;
            Assert.Null(cell.Value);
            Assert.StartsWith(SpreadsheetTable.CycleMessagePrefix, cell.Error);
        }
    }

    [Fact]
    public void ReferenceOutsideTable_IsError()
    {
        var table = new SpreadsheetTable(2, 2);
        table.SetExpression("A1", "Z99 + 1");
        Assert.NotNull(table.FindCell("A1")!.Error);
    }

    [Fact]
    public void ErrorInSource_PropagatesToDependent()
    {
        var table = new SpreadsheetTable(2, 2);
        table.SetExpression("A1", "1/0");
        table.SetExpression("A2", "A1 + 1");
        Assert.NotNull(table.FindCell("A2")!.Error);
        Assert.Equal("#ПОМИЛКА", table.FindCell("A2")!.DisplayValue);
    }

    [Fact]
    public void RemoveLastRow_ReportsDependentCells()
    {
        var table = new SpreadsheetTable(3, 2);
        table.SetExpression("A3", "5");
        table.SetExpression("B1", "A3 * 2");

        Assert.Equal(new[] { "B1" }, table.GetDependentsOfLastRow());
        var dependents = table.RemoveLastRow();

        Assert.Equal(new[] { "B1" }, dependents);
        Assert.Equal(2, table.RowCount);
        Assert.NotNull(table.FindCell("B1")!.Error);
    }

    [Fact]
    public void Document_RoundTrip_KeepsExpressions()
    {
        var table = new SpreadsheetTable(4, 3);
        table.SetExpression("A1", "2^10");
        table.SetExpression("C4", "A1 mod 7");

        var json = SpreadsheetSerializer.Serialize(table.ToDocument());
        var restored = SpreadsheetTable.FromDocument(SpreadsheetSerializer.Deserialize(json));

        Assert.Equal(4, restored.RowCount);
        Assert.Equal(3, restored.ColumnCount);
        Assert.Equal("A1 mod 7", restored.FindCell("C4")!.Expression);
        Assert.Equal(1024 % 7, restored.FindCell("C4")!.Value);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(701, "ZZ")]
    public void ColumnName_ConvertsIndexToLetters(int index, string expected)
    {
        Assert.Equal(expected, CellAddress.ColumnName(index));
        Assert.Equal(index, CellAddress.ColumnIndex(expected));
    }
}
