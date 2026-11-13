using Antlr4.Runtime;
using Spreadsheet.Core.Grammar;

namespace Spreadsheet.Core.Evaluation;

/// <summary>
/// Розбирає вираз за допомогою ANTLR і обчислює його значення.
/// Значення клітинок, на які посилається вираз, запитуються через делегат.
/// </summary>
public static class ExpressionEvaluator
{
    /// <summary>Синтаксична перевірка виразу. Кидає <see cref="SyntaxException"/> при помилці.</summary>
    public static SpreadsheetParser.CompileUnitContext Parse(string expression)
    {
        var lexer = new SpreadsheetLexer(new AntlrInputStream(expression));
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(new ThrowingLexerErrorListener());

        var parser = new SpreadsheetParser(new CommonTokenStream(lexer));
        parser.RemoveErrorListeners();
        parser.AddErrorListener(new ThrowingParserErrorListener());

        return parser.compileUnit();
    }

    /// <summary>Повертає імена клітинок, на які посилається вираз (у верхньому регістрі).</summary>
    public static IReadOnlySet<string> GetReferences(string expression)
    {
        var tree = Parse(expression);
        var collector = new ReferenceCollector();
        collector.Visit(tree);
        return collector.References;
    }

    public static double Evaluate(string expression, Func<string, double> cellValueProvider)
    {
        var tree = Parse(expression);
        var visitor = new EvaluationVisitor(cellValueProvider);
        return visitor.Visit(tree);
    }

    private sealed class ReferenceCollector : SpreadsheetBaseVisitor<object?>
    {
        public HashSet<string> References { get; } = new(StringComparer.OrdinalIgnoreCase);

        public override object? VisitIdentifierExpr(SpreadsheetParser.IdentifierExprContext context)
        {
            References.Add(context.GetText().ToUpperInvariant());
            return null;
        }
    }

    private sealed class ThrowingLexerErrorListener : IAntlrErrorListener<int>
    {
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
            => throw new SyntaxException($"Невідомий символ у позиції {charPositionInLine + 1}");
    }

    private sealed class ThrowingParserErrorListener : BaseErrorListener
    {
        public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
            int line, int charPositionInLine, string msg, RecognitionException e)
            => throw new SyntaxException($"Синтаксична помилка у позиції {charPositionInLine + 1}: '{offendingSymbol.Text}'");
    }
}
