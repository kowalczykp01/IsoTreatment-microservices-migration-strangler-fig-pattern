using FluentAssertions;

namespace IsoTreatment.ContractTests;

[Collection(ContractTestCollection.Name)]
public sealed class GatewayRoutingTests
{
    private readonly ContractTestFixture _fixture;

    public GatewayRoutingTests(ContractTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(MonolithOnlyPath.UserInfo)]
    [InlineData("/api/entry")]
    [InlineData("/api/treatment-process")]
    public async Task PathsOutsideRemindersAreServedByTheMonolith(string path)
    {
        var userId = await _fixture.SeedUserAsync();
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);

        var response = await gateway.GetWithBearerHeaderAsync(path);

        ServiceFingerprint.FromBearerHeaderResponse(response).Should().Be(RespondingService.Monolith);
    }

    [Fact]
    public async Task TheMonolithNoLongerServesReminders()
    {
        var userId = await _fixture.SeedUserAsync();
        using var monolith = _fixture.ClientFor(ServiceUnderTest.Monolith, userId);

        var response = await monolith.GetAllAsync();

        response.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task TheMonolithStillServesEverythingElse()
    {
        var userId = await _fixture.SeedUserAsync();
        using var monolith = _fixture.ClientFor(ServiceUnderTest.Monolith, userId);

        var response = await monolith.GetWithBearerHeaderAsync(MonolithOnlyPath.UserInfo);

        ServiceFingerprint.FromBearerHeaderResponse(response).Should().Be(RespondingService.Monolith);
    }

    [Fact]
    public async Task RemindersThroughTheGatewayAreServedByTheTreatmentService()
    {
        var userId = await _fixture.SeedUserAsync();
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);

        var response = await gateway.GetWithBearerHeaderAsync("/api/reminder");

        ServiceFingerprint.FromBearerHeaderResponse(response).Should().Be(RespondingService.Treatment);
    }

    [Fact]
    public async Task TheGatewayIsTransparentForReminders()
    {
        var userId = await _fixture.SeedUserAsync();
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);
        using var treatment = _fixture.ClientFor(ServiceUnderTest.Treatment, userId);

        await treatment.AddAsync("08:00");

        var throughGateway = await gateway.GetAllAsync();
        var direct = await treatment.GetAllAsync();

        throughGateway.Should().BeEquivalentTo(direct);
    }
}
