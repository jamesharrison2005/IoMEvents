using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HtmlAgilityPack;
using IomEvents.Domain;
using Microsoft.Extensions.Logging;

namespace IomEvents.Infrastructure;

public class SampleEventScraper : IEventScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SampleEventScraper>? _logger;

    public SampleEventScraper(HttpClient httpClient, ILogger<SampleEventScraper>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<Event>> ScrapeEventsAsync()
    {
        var events = new List<Event>();
        var targetUrl = "https://www.visitisleofman.com/whats-on";

        string html;
        try
        {
            html = await _httpClient.GetStringAsync(targetUrl);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to fetch listing page: {Url}", targetUrl);
            return events;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Collect candidate detail links from the listing page using several heuristics
        var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>();
        var candidates = new List<string>();

        foreach (var a in linkNodes)
        {
            var href = a.GetAttributeValue("href", string.Empty);
            if (string.IsNullOrWhiteSpace(href)) continue;

            // Normalize relative links
            string absolute;
            try
            {
                absolute = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? href
                    : new Uri(new Uri(targetUrl), href).ToString();
            }
            catch
            {
                continue;
            }

            if (absolute.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || absolute.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
                continue;

            // Heuristic filters: likely event detail pages
            if (absolute.Contains("/events/") || absolute.Contains("/whats-on/") || absolute.Contains("/event/"))
            {
                if (!candidates.Contains(absolute)) candidates.Add(absolute);
            }
        }

        // Fallback: look for article nodes used by some event themes
        if (candidates.Count == 0)
        {
            var eventNodes = doc.DocumentNode.SelectNodes("//article[contains(@class, 'type-tribe_events')]");
            if (eventNodes != null)
            {
                foreach (var node in eventNodes)
                {
                    var a = node.SelectSingleNode(".//a[@href]");
                    if (a == null) continue;
                    var href = a.GetAttributeValue("href", string.Empty);
                    if (string.IsNullOrWhiteSpace(href)) continue;
                    var absolute = href.StartsWith("http") ? href : new Uri(new Uri(targetUrl), href).ToString();
                    if (!candidates.Contains(absolute)) candidates.Add(absolute);
                }
            }
        }

        // Limit number of detail pages to follow to avoid long-running operations
        var toFetch = candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
        _logger?.LogInformation("Found {Count} candidate event links, fetching {ToFetch}", candidates.Count, toFetch.Count);

        foreach (var url in toFetch)
        {
            try
            {
                var detailHtml = await _httpClient.GetStringAsync(url);
                var d = new HtmlDocument();
                d.LoadHtml(detailHtml);

                // Title
                var title = d.DocumentNode.SelectSingleNode("//h1")?.InnerText.Trim()
                            ?? d.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", string.Empty)?.Trim()
                            ?? d.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim();

                // Description
                var description = d.DocumentNode.SelectSingleNode("//meta[@name='description']")?.GetAttributeValue("content", string.Empty)?.Trim()
                                  ?? d.DocumentNode.SelectSingleNode("//*[contains(@class,'event-description')]")?.InnerText.Trim()
                                  ?? string.Empty;

                // Date/time parsing - try time[datetime] first
                DateTime startDate = DateTime.UtcNow.AddDays(1);
                var timeNode = d.DocumentNode.SelectSingleNode("//time[@datetime]");
                if (timeNode != null)
                {
                    var dtStr = timeNode.GetAttributeValue("datetime", string.Empty).Trim();
                    if (!string.IsNullOrEmpty(dtStr) && DateTime.TryParse(dtStr, out var parsed))
                        startDate = parsed.ToUniversalTime();
                }
                else
                {
                    var dateText = d.DocumentNode.SelectSingleNode("//p[contains(@class,'date')]|//div[contains(@class,'date')]|//span[contains(@class,'date')]")?.InnerText;
                    if (!string.IsNullOrWhiteSpace(dateText) && DateTime.TryParse(dateText.Trim(), out var parsed2))
                        startDate = parsed2.ToUniversalTime();
                }

                // Location
                var location = "Isle of Man";
                var bodyText = d.DocumentNode.InnerText;
                var addressMatch = System.Text.RegularExpressions.Regex.Match(
                    bodyText, @"[A-Za-z0-9'&.\-]+(?:,\s*[A-Za-z0-9'&.\-]+){1,4},?\s*IM\d{1,2}\s?\d[A-Z]{2}");
                if (addressMatch.Success)
                {
                    var matchedTown = IomTowns.All.FirstOrDefault(town =>
                        addressMatch.Value.Contains(town, StringComparison.OrdinalIgnoreCase));
                    if (matchedTown != null)
                        location = matchedTown;
                }

                // Category
                var category = "General";
                var headingNodes = d.DocumentNode.SelectNodes("//h1|//h2|//h3|//h4") ?? Enumerable.Empty<HtmlNode>();
                var typeHeading = headingNodes.FirstOrDefault(n =>
                    System.Text.RegularExpressions.Regex.IsMatch(n.InnerText.Trim(), @"^Type:\S"));
                if (typeHeading != null)
                {
                    category = typeHeading.InnerText.Trim().Substring("Type:".Length).Trim();
                }
                events.Add(new Event
                {
                    id = Guid.NewGuid(),
                    title = HtmlEntity.DeEntitize(title ?? "Untitled"),
                    description = HtmlEntity.DeEntitize(description ?? string.Empty),
                    startDate = startDate,
                    location = HtmlEntity.DeEntitize(location ?? "Isle of Man"),
                    category = HtmlEntity.DeEntitize(category ?? "General"),
                    sourceUrl = url
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to parse event detail {Url}", url);
            }
        }

        return events;
    }
}
