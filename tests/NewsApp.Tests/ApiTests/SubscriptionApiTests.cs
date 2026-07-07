using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NewsApp.Modules.Identity;
using NewsApp.Modules.Subscriptions;

namespace NewsApp.Tests.ApiTests;

public class SubscriptionApiTests(CustomWebApplicationFactory<Program> factory)
    : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        const string password = "Password123!";
        await _client.PostAsJsonAsync("/api/auth/register", new { Email = email, Password = password });
        var loginResp = await _client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        var auth = await loginResp.Content.ReadFromJsonAsync<AuthResponse>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    [Fact]
    public async Task GetSubscriptions_WhenUnauthorized_Returns401()
    {
        var response = await _client.GetAsync("/api/subscriptions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscriptions_WhenAuthenticated_ReturnsEmptyList()
    {
        var client = await CreateAuthenticatedClientAsync("sub_empty@test.com");
        var response = await client.GetAsync("/api/subscriptions");
        response.EnsureSuccessStatusCode();
        var subs = await response.Content.ReadFromJsonAsync<List<SubscriptionResponse>>();
        Assert.NotNull(subs);
        Assert.Empty(subs);
    }

    [Fact]
    public async Task CreateSubscription_WithValidData_Returns201()
    {
        var client = await CreateAuthenticatedClientAsync("sub_create@test.com");
        var response = await client.PostAsJsonAsync("/api/subscriptions",
            new CreateSubscriptionRequest(SubscriptionType.Email, "notify@example.com"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sub = await response.Content.ReadFromJsonAsync<SubscriptionResponse>();
        Assert.NotNull(sub);
        Assert.Equal("notify@example.com", sub.Target);
        Assert.Equal(SubscriptionType.Email, sub.Type);
        Assert.True(sub.IsActive);
    }

    [Fact]
    public async Task GetSubscriptions_AfterCreate_ReturnsList()
    {
        var client = await CreateAuthenticatedClientAsync("sub_list@test.com");
        await client.PostAsJsonAsync("/api/subscriptions",
            new CreateSubscriptionRequest(SubscriptionType.Slack, "#alerts"));

        var response = await client.GetAsync("/api/subscriptions");
        response.EnsureSuccessStatusCode();
        var subs = await response.Content.ReadFromJsonAsync<List<SubscriptionResponse>>();
        Assert.NotNull(subs);
        Assert.Single(subs);
        Assert.Equal("#alerts", subs[0].Target);
    }

    [Fact]
    public async Task UpdateSubscription_OwnSubscription_Returns200WithUpdatedValues()
    {
        var client = await CreateAuthenticatedClientAsync("sub_update@test.com");
        var createResp = await client.PostAsJsonAsync("/api/subscriptions",
            new CreateSubscriptionRequest(SubscriptionType.Email, "old@example.com"));
        var created = await createResp.Content.ReadFromJsonAsync<SubscriptionResponse>();

        var updateResp = await client.PutAsJsonAsync($"/api/subscriptions/{created!.Id}",
            new UpdateSubscriptionRequest("new@example.com", false));
        updateResp.EnsureSuccessStatusCode();
        var updated = await updateResp.Content.ReadFromJsonAsync<SubscriptionResponse>();
        Assert.Equal("new@example.com", updated!.Target);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task UpdateSubscription_OtherUsersSubscription_Returns403()
    {
        var ownerClient = await CreateAuthenticatedClientAsync("sub_owner@test.com");
        var createResp = await ownerClient.PostAsJsonAsync("/api/subscriptions",
            new CreateSubscriptionRequest(SubscriptionType.Email, "owner@example.com"));
        var created = await createResp.Content.ReadFromJsonAsync<SubscriptionResponse>();

        var otherClient = await CreateAuthenticatedClientAsync("sub_other@test.com");
        var updateResp = await otherClient.PutAsJsonAsync($"/api/subscriptions/{created!.Id}",
            new UpdateSubscriptionRequest("hacked@example.com", null));
        Assert.Equal(HttpStatusCode.Forbidden, updateResp.StatusCode);
    }

    [Fact]
    public async Task DeleteSubscription_OwnSubscription_Returns204()
    {
        var client = await CreateAuthenticatedClientAsync("sub_delete@test.com");
        var createResp = await client.PostAsJsonAsync("/api/subscriptions",
            new CreateSubscriptionRequest(SubscriptionType.Email, "del@example.com"));
        var created = await createResp.Content.ReadFromJsonAsync<SubscriptionResponse>();

        var deleteResp = await client.DeleteAsync($"/api/subscriptions/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task DeleteSubscription_OtherUsersSubscription_Returns403()
    {
        var ownerClient = await CreateAuthenticatedClientAsync("sub_del_owner@test.com");
        var createResp = await ownerClient.PostAsJsonAsync("/api/subscriptions",
            new CreateSubscriptionRequest(SubscriptionType.Email, "del_owner@example.com"));
        var created = await createResp.Content.ReadFromJsonAsync<SubscriptionResponse>();

        var otherClient = await CreateAuthenticatedClientAsync("sub_del_other@test.com");
        var deleteResp = await otherClient.DeleteAsync($"/api/subscriptions/{created!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResp.StatusCode);
    }
}
