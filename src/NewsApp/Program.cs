using Microsoft.EntityFrameworkCore;
using NewsApp.Infrastructure.Data;
using NewsApp.Modules.Identity;
using NewsApp.Modules.Notifications;
using NewsApp.Modules.Subscriptions;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Modules
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddNotificationsModule();

// API
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapIdentityEndpoints();
app.MapSubscriptionEndpoints();

await app.SeedAdminAsync();

app.Run();

// Required so WebApplicationFactory<Program> can find this class in tests
public partial class Program { }
