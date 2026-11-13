namespace Spreadsheet.Core.Evaluation;

/// <summary>Помилка синтаксичного аналізу виразу.</summary>
public class SyntaxException : Exception
{
    public SyntaxException(string message) : base(message) { }
}

/// <summary>Помилка під час обчислення виразу (ділення на нуль, цикл, невідома клітинка тощо).</summary>
public class EvaluationException : Exception
{
    public EvaluationException(string message) : base(message) { }
}
