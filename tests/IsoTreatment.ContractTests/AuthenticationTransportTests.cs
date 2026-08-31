using FluentAssertions;

namespace IsoTreatment.ContractTests;

[Collection(ContractTestCollection.Name)]
public sealed class AuthenticationTransportTests
{
    private readonly ContractTestFixture _fixture;

    public AuthenticationTransportTests(ContractTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(ServiceUnderTest.Monolith)]
    [InlineData(ServiceUnderTest.Treatment)]
    public async Task TokenInCookie_IsAccepted(string service)
    {
        var userId = await _fixture.SeedUserAsync();
        using var client = _fixture.ClientFor(service, userId);

        var response = await client.GetAllAsync();

        response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task TokenInAuthorizationHeader_IsAcceptedByTreatment()
    {
        var userId = await _fixture.SeedUserAsync();
        using var client = _fixture.ClientFor(ServiceUnderTest.Treatment, userId);

        var response = await client.GetAllWithBearerHeaderAsync();

        response.StatusCode.Should().Be(200);
        response.Body.Should().Be("[]");
    }

    [Fact]
    public async Task TokenInAuthorizationHeader_CrashesTheMonolith_KnownAndAcceptedDivergence()
    {
        var userId = await _fixture.SeedUserAsync();
        using var client = _fixture.ClientFor(ServiceUnderTest.Monolith, userId);

        var response = await client.GetAllWithBearerHeaderAsync();

        response.StatusCode.Should().Be(500);
        response.Body.Should().Be("Something went wrong");
    }
}
