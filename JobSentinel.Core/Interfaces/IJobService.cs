using JobSentinel.Core.Models;

namespace JobSentinel.Core.Interfaces;

public interface IJobService
{
    Task<string> RunAllScrapersAsync(JobSearchFilter filter);
}