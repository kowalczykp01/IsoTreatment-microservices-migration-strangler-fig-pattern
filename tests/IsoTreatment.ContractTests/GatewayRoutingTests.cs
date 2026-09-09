using FluentAssertions;

namespace IsoTreatment.ContractTests;

[Collection(ContractTestCollection.Name)]
public sealed class GatewayRoutingTests
{
    private readonly ContractTestFixture _fixture;

    public GatewayRoutingTests(ContractTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task TheFingerprintTellsTheTwoServicesApart()
    {
        var userId = await _fixture.SeedUserAsync();

        using var monolith = _fixture.ClientFor(ServiceUnderTest.Monolith, userId);
        using var treatment = _fixture.ClientFor(ServiceUnderTest.Treatment, userId);

        ServiceFingerprint.ShouldDistinguishServices(
            await monolith.GetAllWithBearerHeaderAsync(),
            await treatment.GetAllWithBearerHeaderAsync());
    }

    [Theory]
    [InlineData("/api/user/info")]
    [InlineData("/api/entry")]
    [InlineData("/api/treatment-process")]
    public async Task PathsOutsideRemindersAreStillServedByTheMonolith(string path)
    {
        var userId = await _fixture.SeedUserAsync();
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);

        var response = await gateway.GetWithBearerHeaderAsync(path);

        ServiceFingerprint.FromBearerHeaderResponse(response).Should().Be(RespondingService.Monolith);
    }

    [Fact]
    public async Task TheReminderCollectionIsServedByTheTreatmentService()
    {
        var userId = await _fixture.SeedUserAsync();
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);

        var response = await gateway.GetWithBearerHeaderAsync("/api/reminder");

        ServiceFingerprint.FromBearerHeaderResponse(response).Should().Be(RespondingService.Treatment);
    }

    [Fact]
    public async Task ASingleReminderIsServedByTheTreatmentService()
    {
        var userId = await _fixture.SeedUserAsync();
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);
        using var treatment = _fixture.ClientFor(ServiceUnderTest.Treatment, userId);

        var created = await treatment.AddAsync("08:00");
        var id = int.Parse(System.Text.RegularExpressions.Regex.Match(created.Body, "\"id\":([0-9]+)").Groups[1].Value);

        var response = await gateway.GetWithBearerHeaderAsync($"/api/reminder/{id}");

        ServiceFingerprint.FromBearerHeaderResponse(response).Should().Be(RespondingService.Treatment);
    }

    [Fact]
    public async Task TheCanaryHeaderNoLongerChangesRouting()
    {
        var userId = await _fixture.SeedUserAsync();
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);

        var without = await gateway.GetWithBearerHeaderAsync("/api/reminder");
        var with = await gateway.GetWithBearerHeaderAsync("/api/reminder", CanaryHeader.TreatmentValue);

        ServiceFingerprint.FromBearerHeaderResponse(without).Should().Be(RespondingService.Treatment);
        ServiceFingerprint.FromBearerHeaderResponse(with).Should().Be(RespondingService.Treatment);
    }

    [Fact]
    public async Task TheReminderRouteMatchesTheCollectionPathWithNoTrailingSegment()
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
