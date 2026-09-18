using IomEvents.Infrastructure;
using Microsoft.EntityFrameworkCore;

int PORT = 7019;
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));


// 3. Register HttpClient and Scraper Service
builder.Services.AddHttpClient<IEventScraper, SampleEventScraper>();

// 4. Register background scraping worker (periodic upsert worker)
builder.Services.AddHostedService<ScrapeBackgroundService>();

// 5 API & Swagger Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("\n==================================================");
Console.WriteLine(" View docs here: https://localhost:" + PORT + "/swagger");
Console.WriteLine("==================================================\n");

app.Run();