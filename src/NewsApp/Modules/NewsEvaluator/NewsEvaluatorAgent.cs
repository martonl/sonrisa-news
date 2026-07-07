using System.Text;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NewsApp.Modules.Notifications;
using OpenAI;
using OpenAI.Responses;

namespace NewsApp.Modules.NewsEvaluator;

public sealed class NewsEvaluatorAgent(
    IConfiguration configuration,
    ILogger<NewsEvaluatorAgent> logger) : INewsEvaluatorAgent
{
    private const string SystemPrompt =
        """
        Evaluate this news feed. Return maximum 5 most important hard-news stories.
        Prioritize breaking international news, wars, major geopolitical developments,
        natural disasters, significant economic/business news, public health,
        science and technology, and government policy with broad international impact.
        Exclude sports, entertainment, celebrity, royal/family news, opinion pieces,
        analysis, explainers, feature stories, human-interest stories, local crime,
        court hearings, individual criminal cases, and lifestyle content.
        Return ONLY a valid JSON array (no markdown, no code blocks) where each element has:
        Title, Summary, Url, and PublishedAt (ISO 8601 UTC).
        Example: [{"Title":"...","Summary":"...","Url":"...","PublishedAt":"2026-01-01T00:00:00Z"}]
        """;

    public async Task<IReadOnlyList<NewsNotification>> EvaluateAsync(
        IReadOnlyList<RssItem> items,
        CancellationToken ct = default)
    {
        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured.");
        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

#pragma warning disable OPENAI001
        var responsesClient = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey))
            .GetResponsesClient();
#pragma warning restore OPENAI001

        var agent = responsesClient.AsAIAgent(model: model, instructions: SystemPrompt);

        var messages = new[]
        {
            new ChatMessage(ChatRole.User, BuildPrompt(items))
        };

        logger.LogInformation("Sending {Count} RSS items to AI for evaluation.", items.Count);

        var response = await agent.RunAsync(messages, null, null, ct);

        return ParseResponse(response.ToString(), items);
    }

    private static string BuildPrompt(IReadOnlyList<RssItem> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Here are the latest news items to evaluate:");
        sb.AppendLine();
        foreach (var item in items)
        {
            sb.AppendLine($"Title: {item.Title}");
            sb.AppendLine($"Summary: {item.Summary}");
            sb.AppendLine($"Url: {item.Url}");
            sb.AppendLine($"PublishedAt: {item.PublishedAt:O}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private IReadOnlyList<NewsNotification> ParseResponse(string response, IReadOnlyList<RssItem> sourceItems)
    {
        try
        {
            // Strip markdown code fences if present
            var json = response.Trim();
            if (json.StartsWith("```"))
            {
                var start = json.IndexOf('[');
                var end = json.LastIndexOf(']');
                if (start >= 0 && end > start)
                    json = json[start..(end + 1)];
            }

            var dtos = JsonSerializer.Deserialize<List<NewsNotificationDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (dtos is null || dtos.Count == 0)
                return [];

            return dtos
                .Where(d => !string.IsNullOrWhiteSpace(d.Title) && !string.IsNullOrWhiteSpace(d.Url))
                .Select(d => new NewsNotification(
                    d.Title!,
                    d.Summary ?? string.Empty,
                    d.Url!,
                    d.PublishedAt ?? DateTime.UtcNow))
                .ToList();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse AI response as JSON. Response: {Response}", response);
            return [];
        }
    }

    private record NewsNotificationDto(
        string? Title,
        string? Summary,
        string? Url,
        DateTime? PublishedAt);
}
