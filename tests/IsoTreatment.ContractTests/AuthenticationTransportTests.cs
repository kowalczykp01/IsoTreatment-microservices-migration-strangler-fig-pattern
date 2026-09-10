using FluentAssertions;

namespace IsoTreatment.ContractTests;

[Collection(ContractTestCollection.Name)]
public sealed class AuthenticationTransportTests
{
    private readonly ContractTestFixture _fixture;

    public AuthenticationTransportTests(ContractTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(ServiceUnderTest.Treatment)]
    [InlineData(ServiceUnderTest.Gateway)]
    public async Task TokenInCookie_IsAccepted(string service)
    {
        var userId = await _fixture.SeedUserAsync();
        using var client = _fixture.ClientFor(service, userId);

        var response = await client.GetAllAsync();

        response.StatusCode.Should().Be(200);
    }

    [Theory]
    [InlineData(ServiceUnderTest.Treatment)]
    [InlineData(ServiceUnderTest.Gateway)]
    public async Task TokenInAuthorizationHeader_IsAccepted(string service)
    {
        var userId = await _fixture.SeedUserAsync();
        using var client = _fixture.ClientFor(service, userId);

        var response = await client.GetAllWithBearerHeaderAsync();

        response.StatusCode.Should().Be(200);
        response.Body.Should().Be("[]");
    }

    [Fact]
    public async Task TokenInAuthorizationHeader_StillBreaksTheEndpointsLeftInTheMonolith()
    {
        var userId = await _fixture.SeedUserAsync();
        using var monolith = _fixture.ClientFor(ServiceUnderTest.Monolith, userId);

        var response = await monolith.GetWithBearerHeaderAsync(MonolithOnlyPath.UserInfo);

        response.StatusCode.Should().Be(500);
        response.Body.Should().Be("Something went wrong");
    }
}
