using JobSentinel.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobSentinel.Infrastructure.Data;

public class ApplicationDbContext: DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
    
    public DbSet<JobOffer> JobOffers { get; set; }
}