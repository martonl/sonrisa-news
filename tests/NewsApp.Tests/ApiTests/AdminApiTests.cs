using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NewsApp.Modules.Identity;
using NewsApp.Modules.Subscriptions;

namespace NewsApp.Tests.ApiTests;

public class AdminApiTests(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login",
            new { Email = "admin@test.com", Password = "Admin123!" });
        loginResp.EnsureSuccessStatusCode();
        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private async Task<HttpClient> CreateRegularClientAsync(string email)
    {
        const string password = "Password123!";
        await _client.PostAsJsonAsync("/api/auth/register", new { Email = email, Password = password });
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    private async Task<AdminUserResponse> GetRegisteredUserAsync(HttpClient adminClient, string email)
    {
        // Fetch the first page; test DB will never exceed pageSize in unit tests
        var usersResp = await adminClient.GetAsync("/api/admin/users?pageSize=100");
        var result = await usersResp.Content.ReadFromJsonAsync<PagedResult<AdminUserResponse>>();
        return result!.Items.First(u => u.Email == email);
    }

    [Fact]
    public async Task GetUsers_WithoutAdminRole_Returns403()
    {
        var client = await CreateRegularClientAsync("admin_nonadmin@test.com");
        var response = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_AsAdmin_ReturnsPaginatedResultContainingAdmin()
    {
        var adminClient = await CreateAdminClientAsync();
        var response = await adminClient.GetAsync("/api/admin/users");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<AdminUserResponse>>();
        Assert.NotNull(result);
        Assert.True(result.TotalCount >= 1);
        Assert.Contains(result.Items, u => u.Email == "admin@test.com");
    }

    [Fact]
    public async Task CreateSubscription_ForExistingUser_Returns201()
    {
        var adminClient = await CreateAdminClientAsync();
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "admin_sub_create@test.com", Password = "Password123!" });
        var target = await GetRegisteredUserAsync(adminClient, "admin_sub_create@test.com");

        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target.Id}/subscriptions",
            new CreateSubscriptionRequest(SubscriptionType.Email, "notify@example.com"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sub = await response.Content.ReadFromJsonAsync<SubscriptionResponse>();
        Assert.NotNull(sub);
        Assert.Equal(target.Id, sub.UserId);
        Assert.Equal("notify@example.com", sub.Target);
    }

    [Fact]
    public async Task GetSubscriptions_ForUser_AsAdmin_ReturnsList()
    {
        var adminClient = await CreateAdminClientAsync();
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "admin_sub_list@test.com", Password = "Password123!" });
        var target = await GetRegisteredUserAsync(adminClient, "admin_sub_list@test.com");

        await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target.Id}/subscriptions",
            new CreateSubscriptionRequest(SubscriptionType.Slack, "#news"));

        var listResp = await adminClient.GetAsync($"/api/admin/users/{target.Id}/subscriptions");
        listResp.EnsureSuccessStatusCode();
        var subs = await listResp.Content.ReadFromJsonAsync<List<SubscriptionResponse>>();
        Assert.NotNull(subs);
        Assert.Single(subs);
        Assert.Equal("#news", subs[0].Target);
    }

    [Fact]
    public async Task UpdateSubscription_AsAdmin_Returns200WithUpdatedValues()
    {
        var adminClient = await CreateAdminClientAsync();
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "admin_sub_update@test.com", Password = "Password123!" });
        var target = await GetRegisteredUserAsync(adminClient, "admin_sub_update@test.com");

        var createResp = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target.Id}/subscriptions",
            new CreateSubscriptionRequest(SubscriptionType.Email, "old@example.com"));
        var created = await createResp.Content.ReadFromJsonAsync<SubscriptionResponse>();

        var updateResp = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{target.Id}/subscriptions/{created!.Id}",
            new UpdateSubscriptionRequest("updated@example.com", false));
        updateResp.EnsureSuccessStatusCode();
        var updated = await updateResp.Content.ReadFromJsonAsync<SubscriptionResponse>();
        Assert.Equal("updated@example.com", updated!.Target);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task DeleteSubscription_AsAdmin_Returns204()
    {
        var adminClient = await CreateAdminClientAsync();
        await _client.PostAsJsonAsync("/api/auth/register",
            new { Email = "admin_sub_delete@test.com", Password = "Password123!" });
        var target = await GetRegisteredUserAsync(adminClient, "admin_sub_delete@test.com");

        var createResp = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target.Id}/subscriptions",
            new CreateSubscriptionRequest(SubscriptionType.Slack, "#delete-me"));
        var created = await createResp.Content.ReadFromJsonAsync<SubscriptionResponse>();

        var deleteResp = await adminClient.DeleteAsync(
            $"/api/admin/users/{target.Id}/subscriptions/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task CreateSubscription_ForNonExistentUser_Returns404()
    {
        var adminClient = await CreateAdminClientAsync();
        var fakeUserId = Guid.NewGuid().ToString();
        var response = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{fakeUserId}/subscriptions",
            new CreateSubscriptionRequest(SubscriptionType.Email, "nobody@example.com"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
