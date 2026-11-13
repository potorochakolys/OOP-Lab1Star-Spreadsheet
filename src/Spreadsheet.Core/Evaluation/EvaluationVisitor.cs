using Spreadsheet.Core.Grammar;

namespace Spreadsheet.Core.Evaluation;

/// <summary>Обходить дерево розбору і обчислює арифметичний вираз.</summary>
internal sealed class EvaluationVisitor : SpreadsheetBaseVisitor<double>
{
    private readonly Func<string, double> _cellValueProvider;

    public EvaluationVisitor(Func<string, double> cellValueProvider)
    {
        _cellValueProvider = cellValueProvider;
    }

    public override double VisitCompileUnit(SpreadsheetParser.CompileUnitContext context)
        => Visit(context.expression());

    public override double VisitNumberExpr(SpreadsheetParser.NumberExprContext context)
        => double.Parse(context.GetText(), System.Globalization.CultureInfo.InvariantCulture);

    public override double VisitIdentifierExpr(SpreadsheetParser.IdentifierExprContext context)
        => _cellValueProvider(context.GetText().ToUpperInvariant());

    public override double VisitParenthesizedExpr(SpreadsheetParser.ParenthesizedExprContext context)
        => Visit(context.expression());

    public override double VisitExponentialExpr(SpreadsheetParser.ExponentialExprContext context)
    {
        var left = Visit(context.expression(0));
        var right = Visit(context.expression(1));
        return Check(Math.Pow(left, right));
    }

    public override double VisitMultiplicativeExpr(SpreadsheetParser.MultiplicativeExprContext context)
    {
        var left = Visit(context.expression(0));
        var right = Visit(context.expression(1));

        return context.operatorToken.Type switch
        {
            SpreadsheetLexer.MULTIPLY => Check(left * right),
            SpreadsheetLexer.DIVIDE => right == 0
                ? throw new EvaluationException("Ділення на нуль")
                : Check(left / right),
            SpreadsheetLexer.DIV => IntegerDivision(left, right),
            SpreadsheetLexer.MOD => Modulo(left, right),
            _ => throw new EvaluationException("Невідома операція")
        };
    }

    public override double VisitAdditiveExpr(SpreadsheetParser.AdditiveExprContext context)
    {
        var left = Visit(context.expression(0));
        var right = Visit(context.expression(1));

        return context.operatorToken.Type == SpreadsheetLexer.ADD
            ? Check(left + right)
            : Check(left - right);
    }

    public override double VisitMaxMinExpr(SpreadsheetParser.MaxMinExprContext context)
    {
        var first = Visit(context.expression(0));
        var second = Visit(context.expression(1));

        return context.func.Type == SpreadsheetLexer.MAX
            ? Math.Max(first, second)
            : Math.Min(first, second);
    }

    /// <summary>Цілочисельне ділення (div): дробові частини операндів відкидаються.</summary>
    private static double IntegerDivision(double left, double right)
    {
        var a = Math.Truncate(left);
        var b = Math.Truncate(right);
        if (b == 0)
            throw new EvaluationException("Цілочисельне ділення на нуль");
        return Math.Truncate(a / b);
    }

    /// <summary>Остача від ділення (mod) цілих частин операндів.</summary>
    private static double Modulo(double left, double right)
    {
        var a = Math.Truncate(left);
        var b = Math.Truncate(right);
        if (b == 0)
            throw new EvaluationException("Остача від ділення на нуль");
        return a % b;
    }

    private static double Check(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new EvaluationException("Результат не є скінченним числом");
        return value;
    }
}
