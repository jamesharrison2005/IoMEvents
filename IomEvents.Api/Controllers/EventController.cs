using IomEvents.Domain;
using IomEvents.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IomEvents.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _context;

    public EventsController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/events
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Event>>> GetEvents()
    {
        return await _context.Events.ToListAsync();
    }

    // GET: api/events/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Event>> GetEvent(Guid id)
    {
        var @event = await _context.Events.FindAsync(id);

        if (@event == null)
        {
            return NotFound();
        }

        return @event;
    }

    // POST: api/events/scrape
    [HttpPost("scrape")]
    public async Task<IActionResult> ScrapeAndSave([FromServices] IEventScraper scraper)
    {
        var events = await scraper.ScrapeEventsAsync();

        return Ok(new { Count = events.Count, Events = events });
    }
}