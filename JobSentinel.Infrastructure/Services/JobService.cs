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
    private static readonly SemaphoreSlim _scrapeLock = new(1, 1);
    
    public JobService(IEnumerable<IJobScraper> scrapers, ApplicationDbContext dbContext)
    {
        _scrapers = scrapers;
        _dbContext = dbContext;
    }


    public async Task<string> RunAllScrapersAsync(JobSearchFilter filter)
    {
        
        if (!await _scrapeLock.WaitAsync(0))
        {
            return "[MANAGER] A scrape is already running. Please wait for it to finish.";
        }

        try
        {
            int newJobsAdded = 0;
            int oldJobsRemoved = 0;
        
            bool isMasterScrape = string.IsNullOrWhiteSpace(filter.Category) && string.IsNullOrWhiteSpace(filter.Keyword);
        
            foreach (var scraper in _scrapers)
            {
                Console.WriteLine($"[MANAGER] Commanding {scraper.ScraperName} to execute scrape...");
            
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

                if (isMasterScrape)
                {
                    var scrapedUrls = scrapedJobs.Select(j => j.Url).ToList();
                
                    oldJobsRemoved += await _dbContext.JobOffers
                        .Where(j => j.SourceSite == scraper.ScraperName && !scrapedUrls.Contains(j.Url))
                        .ExecuteDeleteAsync();
                }
            }

            if (newJobsAdded > 0)
            {
                await _dbContext.SaveChangesAsync();
            }

            return $"Sync Complete! Added {newJobsAdded} new jobs. Removed {oldJobsRemoved} expired jobs.";
        }
        finally
        {
            _scrapeLock.Release();
        }
        
    }
}