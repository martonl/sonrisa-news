using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using NewsApp.Infrastructure.Data;

namespace NewsApp.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        var secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = key,
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
            });

        services.AddScoped<JwtTokenService>();

        return services;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("/api/auth");

        authGroup.MapPost("/register", async (
            RegisterRequest request,
            UserManager<ApplicationUser> userManager,
            JwtTokenService jwtTokenService) =>
        {
            var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
            var result = await userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
                return Results.BadRequest(result.Errors);

            var roles = await userManager.GetRolesAsync(user);
            return Results.Ok(new AuthResponse(jwtTokenService.GenerateToken(user, roles)));
        });

        authGroup.MapPost("/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            JwtTokenService jwtTokenService) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email);
            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
                return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(user);
            return Results.Ok(new AuthResponse(jwtTokenService.GenerateToken(user, roles)));
        });

        authGroup.MapGet("/me", async (
            ClaimsPrincipal user,
            UserManager<ApplicationUser> userManager) =>
        {
            var userId = user.FindFirstValue("sub");
            if (userId is null)
                return Results.Unauthorized();

            var applicationUser = await userManager.FindByIdAsync(userId);
            if (applicationUser is null)
                return Results.NotFound();

            var roles = await userManager.GetRolesAsync(applicationUser);
            return Results.Ok(new UserResponse(applicationUser.Id, applicationUser.Email!, roles));
        })
        .RequireAuthorization();

        return app;
    }

    public static async Task SeedAdminAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        // Ensure the schema exists. Phase 3 will replace this with database.Migrate()
        // once the InitialCreate migration has been generated.
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationUser>>();

        const string adminRole = "Admin";
        if (!await roleManager.RoleExistsAsync(adminRole))
            await roleManager.CreateAsync(new IdentityRole(adminRole));

        var email = config["AdminSeed:Email"];
        var password = config["AdminSeed:Password"];

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning("AdminSeed:Email or AdminSeed:Password not configured — skipping admin seed.");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, adminRole);
                logger.LogInformation("Admin user seeded: {Email}", email);
            }
            else
            {
                logger.LogError("Failed to seed admin user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
