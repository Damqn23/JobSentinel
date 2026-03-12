using JobSentinel.Core.Entities;
using JobSentinel.Core.Interfaces;
using JobSentinel.Core.Models;
using JobSentinel.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JobSentinel.Infrastructure.Services;

public class JobService: IJobService
{
    private readonly IEnumerable<IJobScraper> _scrapers;
    private readonly ApplicationDbContext _dbContext;
    
    public JobService(IEnumerable<IJobScraper> scrapers, ApplicationDbContext dbContext)
    {
        _scrapers = scrapers;
        _dbContext = dbContext;
    }


    public async Task<string> RunAllScrapersAsync(JobSearchFilter filter)
    {
        int newJobsAdded = 0;
        int oldJobsRemoved = 0;

        // Is this a full sweep of the market?
        bool isMasterScrape = string.IsNullOrWhiteSpace(filter.Category) && string.IsNullOrWhiteSpace(filter.Keyword);
        
        foreach (var scraper in _scrapers)
        {
            Console.WriteLine($"[MANAGER] Commanding {scraper.ScraperName} to execute scrape...");
            
            // The Manager doesn't care HOW the scraper gets the jobs, it just waits for the list!
            var scrapedJobs = await scraper.ScrapeJobsAsync(filter);

            if (!scrapedJobs.Any()) continue;

            var existingUrls = await _dbContext.JobOffers
                .Where(j => j.SourceSite == scraper.ScraperName)
                .Select(j => j.Url)
                .ToListAsync();

            var newJobs = scrapedJobs.Where(j => !existingUrls.Contains(j.Url)).ToList();
            if (newJobs.Any())
            {
                _dbContext.JobOffers.AddRange(newJobs);
                newJobsAdded += newJobs.Count;
            }

            // Delta Sync: Only delete if the robot did a Master Scrape
            if (isMasterScrape)
            {
                var scrapedUrls = scrapedJobs.Select(j => j.Url).ToList();
                var expiredJobs = await _dbContext.JobOffers
                    .Where(j => j.SourceSite == scraper.ScraperName && !scrapedUrls.Contains(j.Url))
                    .ToListAsync();

                if (expiredJobs.Any())
                {
                    _dbContext.JobOffers.RemoveRange(expiredJobs);
                    oldJobsRemoved += expiredJobs.Count;
                }
            }
        }

        if (newJobsAdded > 0 || oldJobsRemoved > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return $"Sync Complete! Added {newJobsAdded} new jobs. Removed {oldJobsRemoved} expired jobs.";
    }
}