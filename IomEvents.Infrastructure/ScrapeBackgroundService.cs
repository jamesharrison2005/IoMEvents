using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IomEvents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IomEvents.Infrastructure;

public class ScrapeBackgroundService : BackgroundService
{
    private readonly IEventScraper _scraper;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScrapeBackgroundService> _logger;

    // Default interval - configurable later if needed
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public ScrapeBackgroundService(IEventScraper scraper, IServiceScopeFactory scopeFactory, ILogger<ScrapeBackgroundService> logger)
    {
        _scraper = scraper ?? throw new ArgumentNullException(nameof(scraper));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScrapeBackgroundService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var scraped = await _scraper.ScrapeEventsAsync();

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                if (scraped != null && scraped.Count > 0)
                {
                    // Prepare lookup for existing events by SourceUrl when available
                    var sourceUrls = scraped.Select(s => s.sourceUrl)
                                            .Where(u => !string.IsNullOrEmpty(u))
                                            .Distinct(StringComparer.OrdinalIgnoreCase)
                                            .ToList();

                    var existing = new List<Event>();
                    if (sourceUrls.Count > 0)
                    {
                        existing = await db.Events
                            .Where(e => sourceUrls.Contains(e.sourceUrl))
                            .ToListAsync(stoppingToken);
                    }

                    var toAdd = new List<Event>();

                    foreach (var item in scraped)
                    {
                        Event? match = null;

                        if (!string.IsNullOrEmpty(item.sourceUrl))
                        {
                            match = existing.FirstOrDefault(e =>
                                string.Equals(e.sourceUrl, item.sourceUrl, StringComparison.OrdinalIgnoreCase));
                        }

                        
                        if (match == null)
                        {
                            match = await db.Events.FirstOrDefaultAsync(e => e.title == item.title && e.startDate == item.startDate, stoppingToken);
                        }

                        if (match != null)
                        {
                            // Update fields
                            match.title = item.title;
                            match.description = item.description;
                            match.startDate = item.startDate;
                            match.location = item.location;
                            match.category = item.category;
                            match.sourceUrl = item.sourceUrl;
                        }
                        else
                        {
                            // New event - ensure id is set
                            if (item.id == Guid.Empty)
                                item.id = Guid.NewGuid();

                            toAdd.Add(item);
                        }
                    }

                    if (toAdd.Count > 0)
                    {
                        db.Events.AddRange(toAdd);
                    }

                    await db.SaveChangesAsync(stoppingToken);

                    _logger.LogInformation("Scrape iteration complete. Scraped={ScrapedCount} Added={AddedCount} Updated={UpdatedCount}", scraped.Count, toAdd.Count, scraped.Count - toAdd.Count);
                }
                else
                {
                    _logger.LogInformation("Scrape iteration found no events");
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scraping iteration");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
        }

        _logger.LogInformation("ScrapeBackgroundService stopping");
    }
}
