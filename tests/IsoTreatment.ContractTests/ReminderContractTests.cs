using System.Text.RegularExpressions;
using FluentAssertions;

namespace IsoTreatment.ContractTests;

[Collection(ContractTestCollection.Name)]
public sealed class ReminderContractTests
{
    private const int MissingReminderId = 999999;
    private const int MissingUserId = 999999;

    private readonly ContractTestFixture _fixture;

    public ReminderContractTests(ContractTestFixture fixture) => _fixture = fixture;

    private static string WithoutIds(string body) => Regex.Replace(body, "\"id\":[0-9]+", "\"id\":N");

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task GetAll_ReturnsEmptyArray_ForUserWithoutReminders(string service)
    {
        var userId = await _fixture.SeedUserAsync();
        using var client = _fixture.ClientFor(service, userId);

        var response = await client.GetAllAsync();

        response.StatusCode.Should().Be(200);
        response.Body.Should().Be("[]");
        response.ContentType.Should().Be("application/json; charset=utf-8");
    }

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task GetAll_ReturnsNotFound_WhenUserRowIsMissing(string service)
    {
        using var client = _fixture.ClientFor(service, MissingUserId);

        var response = await client.GetAllAsync();

        response.StatusCode.Should().Be(404);
        response.Body.Should().Be("User not found");
        response.ContentType.Should().BeNull();
    }

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task Add_ReturnsOkWithoutLocation_AndTimeFormattedAsHoursAndMinutes(string service)
    {
        var userId = await _fixture.SeedUserAsync();
        using var client = _fixture.ClientFor(service, userId);

        var response = await client.AddAsync("08:00");

        response.StatusCode.Should().Be(200);
        response.HasLocationHeader.Should().BeFalse();
        response.ContentType.Should().Be("application/json; charset=utf-8");
        WithoutIds(response.Body).Should().Be("""{"id":N,"time":"08:00"}""");
    }

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task FullCycle_CreateReadUpdateDelete_BehavesIdentically(string service)
    {
        var userId = await _fixture.SeedUserAsync();
        using var client = _fixture.ClientFor(service, userId);

        var created = await client.AddAsync("08:00");
        var id = int.Parse(Regex.Match(created.Body, "\"id\":([0-9]+)").Groups[1].Value);

        var read = await client.GetAsync(id);
        read.StatusCode.Should().Be(200);
        read.Body.Should().Be($$"""{"id":{{id}},"time":"08:00"}""");

        var listed = await client.GetAllAsync();
        listed.Body.Should().Be($$"""[{"id":{{id}},"time":"08:00"}]""");

        var updated = await client.UpdateAsync(id, "21:15");
        updated.StatusCode.Should().Be(200);
        updated.Body.Should().Be($$"""{"id":{{id}},"time":"21:15"}""");

        var deleted = await client.DeleteAsync(id);
        deleted.StatusCode.Should().Be(204);
        deleted.Body.Should().BeEmpty();

        var afterDelete = await client.GetAllAsync();
        afterDelete.Body.Should().Be("[]");
    }

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task Get_ReturnsNotFound_WhenReminderDoesNotExist(string service)
    {
        var userId = await _fixture.SeedUserAsync();
        using var client = _fixture.ClientFor(service, userId);

        var response = await client.GetAsync(MissingReminderId);

        response.StatusCode.Should().Be(404);
        response.Body.Should().Be("Reminder not found");
        response.ContentType.Should().BeNull();
    }

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task Update_ReturnsNotFound_WhenReminderDoesNotExist(string service)
    {
        var userId = await _fixture.SeedUserAsync();
        using var client = _fixture.ClientFor(service, userId);

        var response = await client.UpdateAsync(MissingReminderId, "21:15");

        response.StatusCode.Should().Be(404);
        response.Body.Should().Be("Reminder not found");
        response.ContentType.Should().BeNull();
    }

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task Delete_ReturnsNotFound_WhenReminderDoesNotExist(string service)
    {
        var userId = await _fixture.SeedUserAsync();
        using var client = _fixture.ClientFor(service, userId);

        var response = await client.DeleteAsync(MissingReminderId);

        response.StatusCode.Should().Be(404);
        response.Body.Should().Be("Reminder not found");
        response.ContentType.Should().BeNull();
    }

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task Reminders_OwnedByAnotherUser_AreInvisible(string service)
    {
        var ownerId = await _fixture.SeedUserAsync();
        var otherId = await _fixture.SeedUserAsync();

        using var owner = _fixture.ClientFor(service, ownerId);
        using var other = _fixture.ClientFor(service, otherId);

        var created = await owner.AddAsync("08:00");
        var id = int.Parse(Regex.Match(created.Body, "\"id\":([0-9]+)").Groups[1].Value);

        var listed = await other.GetAllAsync();
        listed.Body.Should().Be("[]");

        var read = await other.GetAsync(id);
        read.StatusCode.Should().Be(404);
        read.Body.Should().Be("Reminder not found");

        var update = await other.UpdateAsync(id, "21:15");
        update.StatusCode.Should().Be(404);

        var stillOwned = await owner.GetAsync(id);
        stillOwned.Body.Should().Be($$"""{"id":{{id}},"time":"08:00"}""");
    }

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task Requests_WithoutToken_AreUnauthorized(string service)
    {
        using var client = _fixture.AnonymousClientFor(service);

        var response = await client.GetAllAsync();

        response.StatusCode.Should().Be(401);
    }

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task Requests_WithMalformedToken_AreUnauthorized(string service)
    {
        using var client = _fixture.ClientWithRawTokenFor(service, "not.a.jwt");

        var response = await client.GetAllAsync();

        response.StatusCode.Should().Be(401);
    }

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task Requests_WithTokenSignedByAnotherKey_AreUnauthorized(string service)
    {
        var userId = await _fixture.SeedUserAsync();
        var foreignToken = _fixture.CreateToken(userId, signingKey: "ACompletelyDifferentHmacKey-0123456789-0123456789==");

        using var client = _fixture.ClientWithRawTokenFor(service, foreignToken);

        var response = await client.GetAllAsync();

        response.StatusCode.Should().Be(401);
    }

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task Requests_WithTokenFromAnotherIssuer_AreUnauthorized(string service)
    {
        var userId = await _fixture.SeedUserAsync();
        var foreignToken = _fixture.CreateToken(userId, issuer: "different-issuer");

        using var client = _fixture.ClientWithRawTokenFor(service, foreignToken);

        var response = await client.GetAllAsync();

        response.StatusCode.Should().Be(401);
    }
}
