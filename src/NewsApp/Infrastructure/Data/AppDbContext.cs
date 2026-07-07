using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NewsApp.Modules.Identity;
using NewsApp.Modules.Subscriptions;

namespace NewsApp.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
}
