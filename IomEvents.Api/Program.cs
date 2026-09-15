using IomEvents.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 3. Register HttpClient and Scraper Service
builder.Services.AddHttpClient<IEventScraper, SampleEventScraper>();

// 4 API & Swagger Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// 5. Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine("\n==================================================");
Console.WriteLine(" View docs here: https://localhost:7019/swagger");
Console.WriteLine("==================================================\n");

app.Run();