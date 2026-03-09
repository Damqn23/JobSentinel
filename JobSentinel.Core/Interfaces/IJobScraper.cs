using JobSentinel.Core.Entities;
using JobSentinel.Core.Models;

namespace JobSentinel.Core.Interfaces;

public interface IJobScraper
{
    string ScraperName { get; }
    
    Task<IEnumerable<JobOffer>> ScrapeJobsAsync(JobSearchFilter filter);
}