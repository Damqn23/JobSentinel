using JobSentinel.Core.Entities;
using JobSentinel.Core.Models;
using Microsoft.Playwright;

namespace JobSentinel.Infrastructure.Scrapers;


public class JobsBgScraper : BaseJobScraper
    {
        public override string ScraperName => "Jobs.bg";

        protected override async Task<IEnumerable<JobOffer>> ExtractJobsAsync(IPage page, JobSearchFilter filter)
        {
            var scrapedJobs = new List<JobOffer>();

            // ==========================================
            // WE WILL WRITE THE JOBS.BG LOGIC HERE
            // ==========================================
            
            return scrapedJobs; 
        }
    }
