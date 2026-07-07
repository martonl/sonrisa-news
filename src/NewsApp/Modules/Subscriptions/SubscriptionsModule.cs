using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NewsApp.Infrastructure.Data;
using NewsApp.Modules.Identity;

namespace NewsApp.Modules.Subscriptions;

public static class SubscriptionsModule
{
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder app)
    {
        MapUserSubscriptionEndpoints(app);
        MapAdminEndpoints(app);
        return app;
    }

    private static void MapUserSubscriptionEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/subscriptions").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = principal.FindFirstValue("sub");
            if (userId is null) return Results.Unauthorized();

            var subs = await db.Subscriptions
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.CreatedAt)
                .Select(s => ToResponse(s))
                .ToListAsync();

            return Results.Ok(subs);
        });

        group.MapPost("/", async (CreateSubscriptionRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = principal.FindFirstValue("sub");
            if (userId is null) return Results.Unauthorized();

            var sub = new Subscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = request.Type,
                Target = request.Target,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Subscriptions.Add(sub);
            await db.SaveChangesAsync();

            return Results.Created($"/api/subscriptions/{sub.Id}", ToResponse(sub));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateSubscriptionRequest request, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = principal.FindFirstValue("sub");
            if (userId is null) return Results.Unauthorized();

            var sub = await db.Subscriptions.FindAsync(id);
            if (sub is null) return Results.NotFound();
            if (sub.UserId != userId) return Results.Forbid();

            if (request.Target is not null) sub.Target = request.Target;
            if (request.IsActive.HasValue) sub.IsActive = request.IsActive.Value;

            await db.SaveChangesAsync();
            return Results.Ok(ToResponse(sub));
        });

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = principal.FindFirstValue("sub");
            if (userId is null) return Results.Unauthorized();

            var sub = await db.Subscriptions.FindAsync(id);
            if (sub is null) return Results.NotFound();
            if (sub.UserId != userId) return Results.Forbid();

            db.Subscriptions.Remove(sub);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static void MapAdminEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        group.MapGet("/users", async (UserManager<ApplicationUser> userManager, int page = 1, int pageSize = 20) =>
        {
            var total = await userManager.Users.CountAsync();
            var items = await userManager.Users
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new AdminUserResponse(u.Id, u.Email!))
                .ToListAsync();

            return Results.Ok(new PagedResult<AdminUserResponse>(items, total, page, pageSize));
        });

        group.MapGet("/users/{userId}/subscriptions", async (string userId, AppDbContext db) =>
        {
            var subs = await db.Subscriptions
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.CreatedAt)
                .Select(s => ToResponse(s))
                .ToListAsync();

            return Results.Ok(subs);
        });

        group.MapPost("/users/{userId}/subscriptions", async (
            string userId,
            CreateSubscriptionRequest request,
            AppDbContext db,
            UserManager<ApplicationUser> userManager) =>
        {
            if (await userManager.FindByIdAsync(userId) is null)
                return Results.NotFound();

            var sub = new Subscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = request.Type,
                Target = request.Target,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Subscriptions.Add(sub);
            await db.SaveChangesAsync();

            return Results.Created($"/api/admin/users/{userId}/subscriptions/{sub.Id}", ToResponse(sub));
        });

        group.MapPut("/users/{userId}/subscriptions/{subId:guid}", async (
            string userId,
            Guid subId,
            UpdateSubscriptionRequest request,
            AppDbContext db) =>
        {
            var sub = await db.Subscriptions.FindAsync(subId);
            if (sub is null || sub.UserId != userId) return Results.NotFound();

            if (request.Target is not null) sub.Target = request.Target;
            if (request.IsActive.HasValue) sub.IsActive = request.IsActive.Value;

            await db.SaveChangesAsync();
            return Results.Ok(ToResponse(sub));
        });

        group.MapDelete("/users/{userId}/subscriptions/{subId:guid}", async (
            string userId,
            Guid subId,
            AppDbContext db) =>
        {
            var sub = await db.Subscriptions.FindAsync(subId);
            if (sub is null || sub.UserId != userId) return Results.NotFound();

            db.Subscriptions.Remove(sub);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    private static SubscriptionResponse ToResponse(Subscription s) =>
        new(s.Id, s.UserId, s.Type, s.Target, s.IsActive, s.CreatedAt);
}
