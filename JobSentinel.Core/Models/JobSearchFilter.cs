namespace JobSentinel.Core.Models;

public class JobSearchFilter
{
    public string? Keyword { get; set; }
    public string? CompanyName { get; set; }
    public string? Location { get; set; }    
    public string? Seniority { get; set; }   
    public bool? IsRemote { get; set; }
    public bool? HasDeclaredSalary { get; set; }
    public string? PaidLeave { get; set; }     
    public string? TeamSize { get; set; }
    public string? Category { get; set; }
}