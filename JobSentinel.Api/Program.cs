using JobSentinel.Core.Interfaces;
using JobSentinel.Core.Models;
using JobSentinel.Infrastructure.Data;
using JobSentinel.Infrastructure.Scrapers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IJobScraper, DevBgScraper>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/scrape", async (IJobScraper scraper, ApplicationDbContext dbContext) =>
{
    var filter = new JobSearchFilter 
    { 
        Category = "net",
    };
    var scrapedJobs = await scraper.ScrapeJobsAsync(filter);

    if (scrapedJobs.Any())
    {
        dbContext.JobOffers.AddRange(scrapedJobs);
        await dbContext.SaveChangesAsync();
    }

    return Results.Ok($"Success! The robot scraped and saved {scrapedJobs.Count()} jobs to your database.");
});

app.Run();