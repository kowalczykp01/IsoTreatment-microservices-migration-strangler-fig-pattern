using System.Text.RegularExpressions;
using FluentAssertions;

namespace IsoTreatment.ContractTests;

[Collection(ContractTestCollection.Name)]
public sealed class ReminderResponseParityTests
{
    private const int MissingReminderId = 999999;

    private readonly ContractTestFixture _fixture;

    public ReminderResponseParityTests(ContractTestFixture fixture) => _fixture = fixture;

    private static RecordedResponse WithoutIds(RecordedResponse response) =>
        response with { Body = Regex.Replace(response.Body, "\"id\":[0-9]+", "\"id\":N") };

    private async Task<(RecordedResponse Direct, RecordedResponse ThroughGateway)> BothWaysAsync(
        Func<ReminderApiClient, Task<RecordedResponse>> request)
    {
        var directUserId = await _fixture.SeedUserAsync();
        var gatewayUserId = await _fixture.SeedUserAsync();

        using var direct = _fixture.ClientFor(ServiceUnderTest.Treatment, directUserId);
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, gatewayUserId);

        return (await request(direct), await request(gateway));
    }

    [Fact]
    public async Task EmptyList_IsIdenticalThroughTheGateway()
    {
        var (direct, gateway) = await BothWaysAsync(client => client.GetAllAsync());

        gateway.Should().BeEquivalentTo(direct);
    }

    [Fact]
    public async Task CreatedReminder_IsIdenticalApartFromTheGeneratedId()
    {
        var (direct, gateway) = await BothWaysAsync(client => client.AddAsync("08:00"));

        WithoutIds(gateway).Should().BeEquivalentTo(WithoutIds(direct));
    }

    [Fact]
    public async Task MissingReminder_IsIdenticalThroughTheGateway()
    {
        var (direct, gateway) = await BothWaysAsync(client => client.GetAsync(MissingReminderId));

        gateway.Should().BeEquivalentTo(direct);
    }

    [Fact]
    public async Task DeletingMissingReminder_IsIdenticalThroughTheGateway()
    {
        var (direct, gateway) = await BothWaysAsync(client => client.DeleteAsync(MissingReminderId));

        gateway.Should().BeEquivalentTo(direct);
    }

    [Fact]
    public async Task UpdatingMissingReminder_IsIdenticalThroughTheGateway()
    {
        var (direct, gateway) = await BothWaysAsync(client => client.UpdateAsync(MissingReminderId, "21:15"));

        gateway.Should().BeEquivalentTo(direct);
    }

    [Fact]
    public async Task RemindersWrittenThroughTheGatewayAreVisibleDirectly()
    {
        var userId = await _fixture.SeedUserAsync();

        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);
        using var treatment = _fixture.ClientFor(ServiceUnderTest.Treatment, userId);

        await gateway.AddAsync("07:45");

        var listedDirectly = await treatment.GetAllAsync();

        listedDirectly.Body.Should().Contain("07:45");
    }
}
