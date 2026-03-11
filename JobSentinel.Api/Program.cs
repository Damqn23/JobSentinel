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

app.MapGet("/scrape", async ([AsParameters] JobSearchFilter jobSearchFilter, IJobService jobService) =>
{
    var resultMessage = await jobService.RunAllScrapersAsync(jobSearchFilter);

    return Results.Ok(resultMessage);
});

app.MapGet("/jobs", async (ApplicationDbContext dbContext, [AsParameters] JobSearchFilter filter) =>
{
    var query = dbContext.JobOffers.AsQueryable();

    if (!string.IsNullOrWhiteSpace(filter.Category))
    {
        query = query.Where(j => j.Url.Contains(filter.Category.ToLower()));
    }

    if (!string.IsNullOrWhiteSpace(filter.Location))
    {
        query = query.Where(j => j.Location != null && j.Location.ToLower().Contains(filter.Location.ToLower()));
    }

    if (!string.IsNullOrWhiteSpace(filter.Keyword))
    {
        query = query.Where(j => j.Title.ToLower().Contains(filter.Keyword.ToLower()));
    }

    if (!string.IsNullOrWhiteSpace(filter.CompanyName))
    {
        query = query.Where(j => j.CompanyName != null && j.CompanyName.ToLower().Contains(filter.CompanyName.ToLower()));
    }

    if (!string.IsNullOrWhiteSpace(filter.Seniority))
    {
        query = query.Where(j => j.Title.ToLower().Contains(filter.Seniority.ToLower()));
    }

    if (filter.HasDeclaredSalary == true)
    {
        query = query.Where(j => !string.IsNullOrWhiteSpace(j.Salary));
    }

    if (filter.IsRemote == true)
    {
        query = query.Where(j => j.Location != null && j.Location.ToLower().Contains("remote"));
    }

    var jobs = await query
        .OrderByDescending(j => j.ScrapedAt)
        .ToListAsync();

    return Results.Ok(jobs);
});

app.Run();