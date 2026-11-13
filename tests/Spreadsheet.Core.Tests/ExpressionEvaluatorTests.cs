using Spreadsheet.Core.Evaluation;
using Xunit;

namespace Spreadsheet.Core.Tests;

public class ExpressionEvaluatorTests
{
    private static double Eval(string expression) => ExpressionEvaluator.Evaluate(expression, _ => 0);

    [Theory]
    [InlineData("2+3*4", 14)]
    [InlineData("(2+3)*4", 20)]
    [InlineData("10-4-3", 3)]
    [InlineData("7/2", 3.5)]
    public void BinaryOperations_RespectPrecedenceAndAssociativity(string expression, double expected)
        => Assert.Equal(expected, Eval(expression), 10);

    [Theory]
    [InlineData("17 mod 5", 2)]
    [InlineData("17 div 5", 3)]
    [InlineData("17 MOD 5 + 17 DIV 5", 5)]
    public void ModAndDiv_WorkAsIntegerOperations(string expression, double expected)
        => Assert.Equal(expected, Eval(expression));

    [Fact]
    public void Exponent_IsRightAssociative()
        => Assert.Equal(512, Eval("2^3^2")); // 2^(3^2) = 2^9

    [Theory]
    [InlineData("max(3, 8)", 8)]
    [InlineData("min(3, 8)", 3)]
    [InlineData("max(min(1,2), 2^2) * 2", 8)]
    public void MaxMin_ReturnExpectedValue(string expression, double expected)
        => Assert.Equal(expected, Eval(expression));

    [Fact]
    public void CellReference_UsesProvidedValue()
    {
        var result = ExpressionEvaluator.Evaluate("A1*2 + b2", name => name switch
        {
            "A1" => 10,
            "B2" => 1,
            _ => throw new InvalidOperationException()
        });
        Assert.Equal(21, result);
    }

    [Theory]
    [InlineData("5/0")]
    [InlineData("5 div 0")]
    [InlineData("5 mod 0")]
    public void DivisionByZero_ThrowsEvaluationException(string expression)
        => Assert.Throws<EvaluationException>(() => Eval(expression));

    [Theory]
    [InlineData("2+")]
    [InlineData("(1+2")]
    [InlineData("max(1)")]
    [InlineData("3 # 4")]
    [InlineData("-5")] // унарного мінуса у варіанті 6 немає
    public void InvalidSyntax_ThrowsSyntaxException(string expression)
        => Assert.Throws<SyntaxException>(() => Eval(expression));

    [Fact]
    public void GetReferences_ReturnsAllCellNames()
    {
        var refs = ExpressionEvaluator.GetReferences("A1 + max(b2, C10) - a1");
        Assert.Equal(new[] { "A1", "B2", "C10" }, refs.OrderBy(x => x));
    }
}
