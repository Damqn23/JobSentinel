using JobSentinel.Core.Entities;
using JobSentinel.Core.Interfaces;
using JobSentinel.Core.Models;
using Microsoft.Playwright;

namespace JobSentinel.Infrastructure.Scrapers;

public class DevBgScraper: IJobScraper
{
    public string ScraperName => "Dev.bg";
    
    public async Task<IEnumerable<JobOffer>> ScrapeJobsAsync(JobSearchFilter filter)
    {
        var scrapedJobs = new List<JobOffer>();
        
        using var playwright = await Playwright.CreateAsync();
        
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false, 
            SlowMo = 50
        });
        
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 } 
        });
        
        var page = await context.NewPageAsync();
        
        var baseUrl = "https://dev.bg/company/jobs/";
        
        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            baseUrl = $"https://dev.bg/company/jobs/{Uri.EscapeDataString(filter.Category.ToLower())}/";
        }
        
        var queryParams = new List<string>(); 
        
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            queryParams.Add($"s={Uri.EscapeDataString(filter.Keyword)}");
        }
        
        if(true == filter.IsRemote)
        {
            queryParams.Add("_job_location=remote");
        }
        else if (!string.IsNullOrWhiteSpace(filter.Location))
        {
            queryParams.Add($"_job_location={Uri.EscapeDataString(filter.Location.ToLower())}");
        }
        
        if (!string.IsNullOrWhiteSpace(filter.Seniority))
        {
            queryParams.Add($"_seniority={Uri.EscapeDataString(filter.Seniority.ToLower())}");
        }
        
        if (true == filter.HasDeclaredSalary)
        {
            queryParams.Add("_salary=1");
        }
        
        if (!string.IsNullOrWhiteSpace(filter.PaidLeave))
        {
            var leaveMapping = new Dictionary<string, string>
            {
                { "20", "77" },
                { "20-24", "78" },
                { "25+", "79" },   
            };

            if (leaveMapping.TryGetValue(filter.PaidLeave.ToLower(), out string leaveId))
            {
                queryParams.Add($"_paid_leave={leaveId}");
            }
        }
        
        if (!string.IsNullOrWhiteSpace(filter.TeamSize))
        {
            var teamMapping = new Dictionary<string, string>
            {
                { "1-9", "35" },
                { "10-30", "36" }, 
                { "30-70", "37" },  
                { "70+", "38"} 
            };

            if (teamMapping.TryGetValue(filter.TeamSize.ToLower(), out string teamId))
            {
                queryParams.Add($"_it_employees_count={teamId}");
            }
        }
        
        string searchUrl = baseUrl;
        if (queryParams.Any())
        {
            searchUrl += "?" + string.Join("&", queryParams);
        }
        await page.GotoAsync(searchUrl);
        try
        {
            var cookieButton = page.Locator("button[data-cky-tag='accept-button']");
            await cookieButton.WaitForAsync(new() { Timeout = 3000 }); 
            await cookieButton.ClickAsync();
        }
        catch (TimeoutException)
        {
            // If the cookie banner doesn't show up (or we already accepted it), just move on!
        }
        int maxPagesToScrape = 20; 
        int currentPage = 1;

        while (currentPage <= maxPagesToScrape)
        {
            try
            {
                await page.WaitForSelectorAsync("h6.job-title", new() { Timeout = 10000 });
            }
            catch (TimeoutException)
            {
                return scrapedJobs;
            } 
            
            var jobCards = await page.Locator(".jobs-loop .job-list-item, .search-results-items .job-list-item").AllAsync();       
            foreach (var card in jobCards)
            {
                var titleText = await card.Locator("h6.job-title").First.InnerTextAsync();
                var companyNameText = await card.Locator(".company-name").First.InnerTextAsync();
                var jobUrl = await card.Locator("a").First.GetAttributeAsync("href");
            
                if (!string.IsNullOrWhiteSpace(titleText))
                {
                    scrapedJobs.Add(new JobOffer
                    {
                        Title = titleText.Trim(),
                        CompanyName = companyNameText.Trim(), 
                        Url = jobUrl,                         
                        SourceSite = ScraperName,
                        ScrapedAt = DateTime.UtcNow
                    });
                }
            }
            
            var nextButton = page.Locator("a.facetwp-page.next");
            
            if (await nextButton.CountAsync() == 0)
            {
                break; 
            }

            await nextButton.ClickAsync(new LocatorClickOptions { Force = true });
            
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            await Task.Delay(1500);
            
            currentPage++;
        }
        
        var uniqueJobs = scrapedJobs
            .GroupBy(j => j.Url)
            .Select(g => g.First())
            .ToList();
        
        return uniqueJobs;
    }
}