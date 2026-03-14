using JobSentinel.Core.Entities;
using JobSentinel.Core.Interfaces;
using JobSentinel.Core.Models;
using Microsoft.Playwright;

namespace JobSentinel.Infrastructure.Scrapers;

public abstract class BaseJobScraper: IJobScraper
{
    public abstract string ScraperName { get; }

    public async Task<IEnumerable<JobOffer>> ScrapeJobsAsync(JobSearchFilter filter)
    {
        Console.WriteLine($"[{ScraperName.ToUpper()}] Starting browser setup...");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 }});

        await context.RouteAsync("**/*", async route =>
        {
            if (route.Request.ResourceType == "image" || route.Request.ResourceType == "media" || route.Request.ResourceType == "font")
                await route.AbortAsync();
            else
                await route.ContinueAsync();
        });

        var page = await context.NewPageAsync();
        
        var scrapedJobs = await ExtractJobsAsync(page, filter);

        Console.WriteLine($"[{ScraperName.ToUpper()}] Scraping complete. Cleaning duplicates...");

        return scrapedJobs.GroupBy(j => j.Url).Select(g => g.First()).ToList();
    }
    
    protected abstract Task<IEnumerable<JobOffer>> ExtractJobsAsync(IPage page, JobSearchFilter filter);
}