using JobSentinel.Core.Entities;
using JobSentinel.Core.Models;
using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace JobSentinel.Infrastructure.Scrapers;

public class JobsBgScraper : BaseJobScraper
{
    public override string ScraperName => "Jobs.bg";

    protected override async Task<IEnumerable<JobOffer>> ExtractJobsAsync(IPage page, JobSearchFilter filter)
    {
        var allScrapedJobs = new List<JobOffer>();
        
        var baseUrl = "https://www.jobs.bg/en/front_job_search.php?categories%5B%5D=56";
        
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.Keyword)) 
            queryParams.Add($"keyword={Uri.EscapeDataString(filter.Keyword)}");
            
        string searchUrl = queryParams.Any() ? $"{baseUrl}&{string.Join("&", queryParams)}" : baseUrl;
        Console.WriteLine($"[JOBS.BG] Scraping: {searchUrl}");

        try
        {
            await page.GotoAsync(searchUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 20000 });
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("ERR_ABORTED")) { }
        
        try 
        { 
            var cookieBtn = page.Locator("button:has-text('Accept'), button:has-text('Съгласен съм')");
            if (await cookieBtn.CountAsync() > 0)
            {
                await cookieBtn.First.ClickAsync(new() { Timeout = 3000 }); 
            }
        } 
        catch { }
        
        Console.WriteLine("[JOBS.BG] Waiting for initial jobs to load from AJAX...");
        try 
        {
            await page.WaitForSelectorAsync("li", new() { Timeout = 15000 });
            
            await Task.Delay(1000); 
        }
        catch (TimeoutException)
        {
            Console.WriteLine("[JOBS.BG] ERROR: No jobs loaded after 15 seconds.");
            await page.ScreenshotAsync(new() { Path = "JobsBg_Error.png" });
            Console.WriteLine("[JOBS.BG] Saved screenshot to JobsBg_Error.png. Check the bin/Debug folder.");
            return new List<JobOffer>();
        }

        int previousJobCount = 0;
        int noNewJobsCounter = 0; 
        
        var cardLocator = page.Locator("li .mdc-card");

        Console.WriteLine("[JOBS.BG] Initial jobs found. Initiating infinite scroll...");

        while (true)
        {
            int currentJobCount = await cardLocator.CountAsync();

            if (currentJobCount == previousJobCount)
            {
                noNewJobsCounter++;
                await Task.Delay(2000); 

                if (noNewJobsCounter >= 3) 
                {
                    Console.WriteLine($"[JOBS.BG] Scroll finished. Found {currentJobCount} jobs.");
                    break;
                }
            }
            else
            {
                noNewJobsCounter = 0; 
                Console.WriteLine($"[JOBS.BG] Loaded {currentJobCount} jobs... scrolling further.");
            }

            previousJobCount = currentJobCount;

            await page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
            await Task.Delay(1500); 
        }
        
        Console.WriteLine("[JOBS.BG] Extracting data from DOM...");
        var finalJobCards = await cardLocator.AllAsync();

        foreach (var card in finalJobCards)
        {
            var titleLocator = card.Locator(".card-title span:not(:has(.star))").Last;
            string? titleText = await titleLocator.CountAsync() > 0 ? await titleLocator.InnerTextAsync() : null;
            
            if (string.IsNullOrWhiteSpace(titleText))
            {
                titleText = await card.Locator(".card-title").InnerTextAsync();
                titleText = Regex.Replace(titleText, @"star", "", RegexOptions.IgnoreCase).Trim();
            }

            var urlLocator = card.Locator("a.black-link-b");
            string? jobUrl = await urlLocator.CountAsync() > 0 ? await urlLocator.First.GetAttributeAsync("href") : null;
            if (jobUrl != null && !jobUrl.StartsWith("http")) jobUrl = "https://www.jobs.bg" + jobUrl;

            var companyLocator = card.Locator(".card-logo-info .secondary-text");
            string? companyNameText = await companyLocator.CountAsync() > 0 ? await companyLocator.First.InnerTextAsync() : null;

            var infoLocator = card.Locator(".card-info.card__subtitle");
            string? locationText = null;
            string? salaryText = null;
            
            if (await infoLocator.CountAsync() > 0)
            {
                var rawInfo = await infoLocator.First.InnerTextAsync();
                var infoParts = rawInfo.Split(';');
                
                if (infoParts.Length > 0) locationText = infoParts[0].Trim();
                
                var salaryPart = infoParts.FirstOrDefault(p => p.Contains("Salary"));
                if (salaryPart != null)
                {
                     salaryText = salaryPart.Replace("Salary", "").Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(titleText) && !string.IsNullOrWhiteSpace(jobUrl))
            {
                allScrapedJobs.Add(new JobOffer
                {
                    Title = titleText.Trim(),
                    CompanyName = companyNameText?.Trim(), 
                    Location = locationText,
                    Salary = salaryText,
                    Url = jobUrl,                         
                    SourceSite = ScraperName,
                    ScrapedAt = DateTime.UtcNow
                });
            }
        }
        
        return allScrapedJobs.GroupBy(j => j.Url).Select(g => g.First()).ToList();
    }
}