using JobSentinel.Core.Entities;
using JobSentinel.Core.Interfaces;
using JobSentinel.Core.Models;
using JobSentinel.Infrastructure.Data;

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
        var allScrapedJobs = new List<JobOffer>();
        
        int totalJobsSaved = 0;
        
        foreach (var scraper in _scrapers)
        {
            var scrapedJobs = await scraper.ScrapeJobsAsync(filter);

            if (scrapedJobs.Any())
            {
                _dbContext.JobOffers.AddRange(scrapedJobs);
                totalJobsSaved+=scrapedJobs.Count();
            }
        }
        
        if (totalJobsSaved > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return $"Success! The Manager ran { _scrapers.Count() } scraper(s) and saved {totalJobsSaved} jobs to the database.";
    }
}