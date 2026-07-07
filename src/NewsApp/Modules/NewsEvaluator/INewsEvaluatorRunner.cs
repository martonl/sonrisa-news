namespace NewsApp.Modules.NewsEvaluator;

public interface INewsEvaluatorRunner
{
    Task RunOnceAsync(CancellationToken ct = default);
}
