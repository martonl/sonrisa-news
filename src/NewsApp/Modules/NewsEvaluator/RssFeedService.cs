using System.ServiceModel.Syndication;
using System.Xml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NewsApp.Modules.NewsEvaluator;

public sealed class RssFeedService(
    HttpClient httpClient,
    IConfiguration configuration,
    ILogger<RssFeedService> logger) : IRssFeedService
{
    public async Task<IReadOnlyList<RssItem>> FetchItemsAsync(CancellationToken ct = default)
    {
        var urls = configuration.GetSection("NewsEvaluator:RssSources").Get<string[]>() ?? [];
        var items = new List<RssItem>();

        foreach (var url in urls)
        {
            try
            {
                var xml = await httpClient.GetStringAsync(url, ct);
                using var reader = XmlReader.Create(new StringReader(xml));
                var feed = SyndicationFeed.Load(reader);

                items.AddRange(feed.Items.Select(item => new RssItem(
                    item.Title?.Text ?? string.Empty,
                    item.Summary?.Text ?? string.Empty,
                    item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty,
                    item.PublishDate.UtcDateTime)));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch RSS feed from {Url}", url);
            }
        }

        return items;
    }
}
