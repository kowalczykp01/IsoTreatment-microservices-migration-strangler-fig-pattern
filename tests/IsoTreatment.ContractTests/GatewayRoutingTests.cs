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
    public async Task PathsOutsideRemindersAreAlwaysServedByTheMonolith(string path)
    {
        var userId = await _fixture.SeedUserAsync();
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);

        var withoutCanary = await gateway.GetWithBearerHeaderAsync(path);
        var withCanary = await gateway.GetWithBearerHeaderAsync(path, CanaryHeader.TreatmentValue);

        ServiceFingerprint.FromBearerHeaderResponse(withoutCanary).Should().Be(RespondingService.Monolith);
        ServiceFingerprint.FromBearerHeaderResponse(withCanary).Should().Be(RespondingService.Monolith);
    }

    [Theory]
    [InlineData("/api/reminder")]
    [InlineData("/api/reminder/999999")]
    public async Task RemindersWithoutTheCanaryHeaderAreServedByTheMonolith(string path)
    {
        var userId = await _fixture.SeedUserAsync();
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);

        var response = await gateway.GetWithBearerHeaderAsync(path);

        ServiceFingerprint.FromBearerHeaderResponse(response).Should().Be(RespondingService.Monolith);
    }

    [Fact]
    public async Task RemindersWithTheCanaryHeaderAreServedByTheTreatmentService()
    {
        var userId = await _fixture.SeedUserAsync();
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);

        var response = await gateway.GetWithBearerHeaderAsync("/api/reminder", CanaryHeader.TreatmentValue);

        ServiceFingerprint.FromBearerHeaderResponse(response).Should().Be(RespondingService.Treatment);
    }

    [Fact]
    public async Task TheCanaryRouteMatchesTheCollectionPathWithNoTrailingSegment()
    {
        var userId = await _fixture.SeedUserAsync();
        using var gateway = _fixture.ClientFor(ServiceUnderTest.Gateway, userId);
        using var treatment = _fixture.ClientFor(ServiceUnderTest.Treatment, userId);

        await treatment.AddAsync("08:00");

        var throughGateway = await gateway.GetAllWithCanaryAsync();
        var direct = await treatment.GetAllAsync();

        throughGateway.Should().BeEquivalentTo(direct);
    }
}
