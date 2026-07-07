namespace NewsApp.Modules.NewsEvaluator;

public class AgentRunState
{
    public int Id { get; set; } = 1; // Single-row table — always Id = 1
    public DateTime? LastRunAt { get; set; }
}
