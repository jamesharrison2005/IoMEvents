using System;
using System.Collections.Generic;
using System.Text;
using IomEvents.Domain;

namespace IomEvents.Infrastructure
{
    public interface IEventScraper
    {
        Task<List<Event>> ScrapeEventsAsync();
    }
}
