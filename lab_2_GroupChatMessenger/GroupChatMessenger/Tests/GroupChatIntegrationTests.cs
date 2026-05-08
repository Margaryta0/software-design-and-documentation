using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using GroupChatMessenger.Api;
using Xunit;

namespace GroupChatMessenger.Tests;

public class GroupChatIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public GroupChatIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DatabasePath", "Data Source=:memory:");
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task FullGroupChatFlow_MessageDeliveredAndRead()
    {
        // Step 1: Create users
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var charlie = await CreateUserAsync("Charlie");

        Assert.NotNull(alice.Id);
        Assert.Equal("Alice", alice.Name);

        // Step 2: Create group
        var group = await CreateGroupAsync("Dev Team", new[] { alice.Id, bob.Id, charlie.Id });

        Assert.NotNull(group.Id);
        Assert.Equal("Dev Team", group.Name);
        Assert.Equal(3, group.MemberIds.Count);

        // Step 3: Send a message from Alice
        var message = await SendMessageAsync(group.Id, alice.Id, "Hello everyone!");

        Assert.NotNull(message.Id);
        Assert.Equal("Hello everyone!", message.Text);
        Assert.Equal("DeliveryInProgress", message.Status);
        Assert.Equal(2, message.DeliveryStatuses.Count);
        Assert.All(message.DeliveryStatuses, ds => Assert.Equal("Enqueued", ds.State));

        // Step 4: Process deliveries (simulates background worker)
        var deliveryResult = await _client.PostAsync("/delivery/process", null);
        deliveryResult.EnsureSuccessStatusCode();
        var processed = await deliveryResult.Content.ReadFromJsonAsync<ProcessDeliveriesResponse>();
        Assert.Contains(message.Id, processed!.MessageIds);

        // Step 5: Retrieve message history — message must exist
        var history = await _client.GetFromJsonAsync<List<MessageResponse>>($"/groups/{group.Id}/messages");
        Assert.NotNull(history);
        Assert.Single(history);
        Assert.Equal(message.Id, history[0].Id);
        Assert.Equal("FullyDelivered", history[0].Status);

        // Step 6: Bob marks the message as read
        var readResp = await _client.PostAsJsonAsync(
            $"/messages/{message.Id}/read",
            new MarkReadRequest(bob.Id));
        Assert.Equal(HttpStatusCode.OK, readResp.StatusCode);

        // Step 7: Verify delivery status updated
        var updated = await _client.GetFromJsonAsync<MessageResponse>($"/messages/{message.Id}");
        Assert.NotNull(updated);
        var bobStatus = updated.DeliveryStatuses.First(ds => ds.RecipientId == bob.Id);
        Assert.Equal("Read", bobStatus.State);
        Assert.NotNull(bobStatus.ReadAt);
        // Charlie hasn't read yet → PartiallyRead
        Assert.Equal("PartiallyRead", updated.Status);
    }

    [Fact]
    public async Task SendMessage_EmptyText_ReturnsBadRequest()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var group = await CreateGroupAsync("Test Group", new[] { alice.Id, bob.Id });

        var resp = await _client.PostAsJsonAsync(
            $"/groups/{group.Id}/messages",
            new SendMessageRequest(alice.Id, "   ")); // whitespace only

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task SendMessage_NonMember_ReturnsBadRequest()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var outsider = await CreateUserAsync("Outsider");
        var group = await CreateGroupAsync("Test Group", new[] { alice.Id, bob.Id });

        var resp = await _client.PostAsJsonAsync(
            $"/groups/{group.Id}/messages",
            new SendMessageRequest(outsider.Id, "Can I join?"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetMessages_UnknownGroup_ReturnsNotFound()
    {
        var resp = await _client.GetAsync("/groups/nonexistent-id/messages");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task SendMessage_FanOut_CreatesPerRecipientDeliveryStatus()
    {
        var alice = await CreateUserAsync("Alice");
        var bob = await CreateUserAsync("Bob");
        var charlie = await CreateUserAsync("Charlie");
        var diana = await CreateUserAsync("Diana");

        var group = await CreateGroupAsync("Big Group", new[] { alice.Id, bob.Id, charlie.Id, diana.Id });
        var message = await SendMessageAsync(group.Id, alice.Id, "Hi all!");

        // 3 recipients (not Alice) → 3 delivery entries
        Assert.Equal(3, message.DeliveryStatuses.Count);
        Assert.DoesNotContain(message.DeliveryStatuses, ds => ds.RecipientId == alice.Id);
    }


    private async Task<UserResponse> CreateUserAsync(string name)
    {
        var resp = await _client.PostAsJsonAsync("/users", new CreateUserRequest(name));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    private async Task<GroupResponse> CreateGroupAsync(string name, IEnumerable<string> memberIds)
    {
        var resp = await _client.PostAsJsonAsync("/groups", new CreateGroupRequest(name, memberIds.ToList()));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<GroupResponse>())!;
    }

    private async Task<MessageResponse> SendMessageAsync(string groupId, string senderId, string text)
    {
        var resp = await _client.PostAsJsonAsync(
            $"/groups/{groupId}/messages",
            new SendMessageRequest(senderId, text));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<MessageResponse>())!;
    }

    public void Dispose() => _client.Dispose();
}
