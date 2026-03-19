using JobSentinel.Core.Entities;
using JobSentinel.Core.Interfaces;
using JobSentinel.Core.Models;
using Microsoft.Playwright;

namespace JobSentinel.Infrastructure.Scrapers;

public class DevBgScraper: BaseJobScraper
{
    public override string ScraperName => "Dev.bg";
    
    protected override async Task<IEnumerable<JobOffer>> ExtractJobsAsync(IPage page, JobSearchFilter filter)
    {
        var allScrapedJobs = new List<JobOffer>();
        
        bool isMasterScrape = string.IsNullOrWhiteSpace(filter.Category) && string.IsNullOrWhiteSpace(filter.Keyword);
        var categoriesToScrape = new List<string>();

        if (isMasterScrape)
        {
            Console.WriteLine("[DEV.BG] Master Scrape detected. Loading all 15 parent categories...");
            categoriesToScrape.AddRange(new[] { 
                "back-end-development", "operations", "mobile-development", 
                "hardware-and-engineering", "front-end-development", "quality-assurance", 
                "data-science", "customer-support", "full-stack-development", 
                "pm-ba-and-more", "erp-crm-development", "ui-ux-and-arts", 
                "technical-support", "junior-intern", "it-management" 
            });
        }
        else
        {
            categoriesToScrape.Add(filter.Category ?? "");
        }
        
        try 
        { 
            await page.GotoAsync("https://dev.bg/", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded }); 
        }
        catch (PlaywrightException) { /* ignore initial navigation issues */ }
        
        try { await page.Locator("button[data-cky-tag='accept-button']").ClickAsync(new() { Timeout = 3000 }); } catch { }
        
        foreach (var cat in categoriesToScrape)
        {
            var baseUrl = "https://dev.bg/company/jobs/";
            if (!string.IsNullOrWhiteSpace(cat))
            {
                baseUrl = $"https://dev.bg/company/jobs/{Uri.EscapeDataString(cat.ToLower())}/";
            }
            
            var queryParams = new List<string>(); 
            if (!string.IsNullOrWhiteSpace(filter.Keyword)) queryParams.Add($"s={Uri.EscapeDataString(filter.Keyword)}");
            if (filter.IsRemote == true) queryParams.Add("_job_location=remote");
            else if (!string.IsNullOrWhiteSpace(filter.Location)) queryParams.Add($"_job_location={Uri.EscapeDataString(filter.Location.ToLower())}");
            if (!string.IsNullOrWhiteSpace(filter.Seniority)) queryParams.Add($"_seniority={Uri.EscapeDataString(filter.Seniority.ToLower())}");
            if (filter.HasDeclaredSalary == true) queryParams.Add("_salary=1");
            
            if (!string.IsNullOrWhiteSpace(filter.PaidLeave))
            {
                var leaveMapping = new Dictionary<string, string>
                {
                    { "20", "77" }, { "20-24", "78" }, { "25+", "79" }
                };
                if (leaveMapping.TryGetValue(filter.PaidLeave.ToLower(), out string leaveId)) queryParams.Add($"_paid_leave={leaveId}");
            }
            
            if (!string.IsNullOrWhiteSpace(filter.TeamSize))
            {
                var teamMapping = new Dictionary<string, string>
                {
                    { "1-9", "35" }, { "10-30", "36" }, { "30-70", "37" }, { "70+", "38"} 
                };
                if (teamMapping.TryGetValue(filter.TeamSize.ToLower(), out string teamId)) queryParams.Add($"_it_employees_count={teamId}");
            }
            
            string searchUrl = queryParams.Any() ? $"{baseUrl}?{string.Join("&", queryParams)}" : baseUrl;
            
            Console.WriteLine($"[DEV.BG] Scraping: {searchUrl}");
            
            try
            {
                await page.GotoAsync(searchUrl, new PageGotoOptions 
                { 
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 15000 
                });
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("ERR_ABORTED"))
            {
                Console.WriteLine($"[DEV.BG] Navigation warning (continuing): {ex.Message}");
                await Task.Delay(1000);
            }
            
            int maxPagesToScrape = 300; 
            int currentPage = 1;

            while (currentPage <= maxPagesToScrape)
            {
                try { await page.WaitForSelectorAsync("h6.job-title", new() { Timeout = 8000 }); }
                catch (TimeoutException) { break; } 
                
                var jobCards = await page.Locator(".jobs-loop .job-list-item, .search-results-items .job-list-item").AllAsync();
                
                foreach (var card in jobCards)
                {
                    var titleText = await card.Locator("h6.job-title").First.InnerTextAsync();
                    var jobUrl = await card.Locator("a").First.GetAttributeAsync("href");
                    
                    var companyLocator = card.Locator(".company-name");
                    string? companyNameText = await companyLocator.CountAsync() > 0 ? await companyLocator.First.TextContentAsync() : null;

                    string? locationText = null;
                    var locationSelectors = new[] { ".card-info .badge", ".job-location", ".hide-for-small-only .badge", ".tags-wrap .badge" };
                    foreach (var selector in locationSelectors)
                    {
                        var locationLocator = card.Locator(selector);
                        if (await locationLocator.CountAsync() > 0)
                        {
                            var rawLocation = await locationLocator.First.InnerTextAsync();
                            var cleaned = System.Text.RegularExpressions.Regex.Replace(rawLocation.Replace("\n", " ").Replace("\r", "").Trim(), @"\s+", " ");
                            if (!string.IsNullOrWhiteSpace(cleaned) && !cleaned.Contains("лв") && !cleaned.Contains("EUR") && !cleaned.Contains("BGN"))
                            {
                                locationText = cleaned;
                                break;
                            }
                        }
                    }

                    var salaryLocator = card.Locator(".tags-wrap .badge:has-text('лв'), .tags-wrap .badge:has-text('EUR')");
                    string? salaryText = await salaryLocator.CountAsync() > 0 ? await salaryLocator.First.TextContentAsync() : null;

                    if (!string.IsNullOrWhiteSpace(titleText))
                    {
                        allScrapedJobs.Add(new JobOffer
                        {
                            Title = titleText.Trim(),
                            CompanyName = companyNameText?.Trim(), 
                            Location = locationText?.Trim(),
                            Salary = salaryText?.Trim(),
                            Url = jobUrl,                         
                            SourceSite = ScraperName,
                            ScrapedAt = DateTime.UtcNow
                        });
                    }
                }
                
                try
                {
                    var nextButton = page.Locator("a.facetwp-page.next");
                    if (await nextButton.CountAsync() == 0 || !await nextButton.IsVisibleAsync()) 
                    {
                        break; 
                    }
                    
                    await nextButton.ScrollIntoViewIfNeededAsync(new() { Timeout = 5000 });
                    await Task.Delay(300);
                    await nextButton.ClickAsync(new() { Timeout = 5000 });
                    await Task.Delay(800);
                    
                    currentPage++;
                }
                catch (Exception ex) 
                {
                    Console.WriteLine($"[DEV.BG] Pagination finished/stopped on page {currentPage}: {ex.Message}");
                    break;
                }
            }
        }
        
        return allScrapedJobs.GroupBy(j => j.Url).Select(g => g.First()).ToList();
    }
}