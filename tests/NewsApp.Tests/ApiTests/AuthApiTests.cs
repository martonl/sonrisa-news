using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NewsApp.Modules.Identity;

namespace NewsApp.Tests.ApiTests;

public class AuthApiTests(CustomWebApplicationFactory<Program> factory) : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_WithValidCredentials_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            Email = "register_valid@test.com",
            Password = "Password123!"
        });

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth?.Token);
        Assert.NotEmpty(auth.Token);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        var request = new { Email = "duplicate@test.com", Password = "Password123!" };
        await _client.PostAsJsonAsync("/api/auth/register", request);

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        const string email = "login_valid@test.com";
        const string password = "Password123!";
        await _client.PostAsJsonAsync("/api/auth/register", new { Email = email, Password = password });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = email,
            Password = password
        });

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth?.Token);
        Assert.NotEmpty(auth.Token);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "nobody@test.com",
            Password = "WrongPass123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsUserInfo()
    {
        const string email = "me_valid@test.com";
        const string password = "Password123!";

        // Register and capture token
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = email, Password = password });
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth?.Token);

        // Call /me with per-request Authorization header
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(email, user?.Email);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        // Create a fresh client through the factory so it routes through the test server
        using var anonClient = factory.CreateClient();
        var response = await anonClient.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
