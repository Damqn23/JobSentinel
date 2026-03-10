using JobSentinel.Core.Interfaces;
using JobSentinel.Core.Models;
using JobSentinel.Infrastructure.Data;
using JobSentinel.Infrastructure.Scrapers;
using JobSentinel.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IJobScraper, DevBgScraper>();
builder.Services.AddScoped<IJobService, JobService>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/scrape", async (IJobService jobService) =>
{
    var filter = new JobSearchFilter 
    { 
        Category = "net",
    };
    
    var resultMessage = await jobService.RunAllScrapersAsync(filter);

    return Results.Ok(resultMessage);
});

app.Run();