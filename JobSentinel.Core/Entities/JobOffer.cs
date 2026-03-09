namespace JobSentinel.Core.Entities;

public class JobOffer
{
    public int Id { get; set; }
    public string ExternalId { get; set; } = string.Empty; 
    public string Title { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Salary { get; set; }
    public string? Location { get; set; }
    public string Url { get; set; } = string.Empty;
    public string SourceSite { get; set; } = string.Empty;
    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;
}