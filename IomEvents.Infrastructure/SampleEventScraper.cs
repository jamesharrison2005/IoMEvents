using HtmlAgilityPack;
using IomEvents.Domain;

namespace IomEvents.Infrastructure;

public class SampleEventScraper : IEventScraper
{
    private readonly HttpClient _httpClient;

    public SampleEventScraper(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<Event>> ScrapeEventsAsync()
    {
        var events = new List<Event>();

        // Example URL: Replace with your target Isle of Man event page URL
        var targetUrl = "https://www.whatsoninisleofman.com/events/";

        var html = await _httpClient.GetStringAsync(targetUrl);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Select event card nodes using XPath (Adjust selectors for your specific target site)
        var eventNodes = doc.DocumentNode.SelectNodes("//article[contains(@class, 'type-tribe_events')]");

        if (eventNodes == null) return events;

        foreach (var node in eventNodes)
        {
            var title = node.SelectSingleNode(".//h3[contains(@class, 'tribe-events-month-event-title')]")?.InnerText.Trim();
            var sourceUrl = node.SelectSingleNode(".//a")?.GetAttributeValue("href", string.Empty);

            if (!string.IsNullOrEmpty(title))
            {
                events.Add(new Event
                {
                    id = Guid.NewGuid(),
                    title = HtmlEntity.DeEntitize(title),
                    description = "Scraped local event",
                    startDate = DateTime.UtcNow.AddDays(1), // Default placeholder date until parsed
                    location = "Isle of Man",
                    category = "General",
                    sourceUrl = sourceUrl ?? targetUrl
                });
            }
        }

        return events;
    }
}