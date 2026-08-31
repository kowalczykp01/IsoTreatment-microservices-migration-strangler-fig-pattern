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

    private async Task<(RecordedResponse Monolith, RecordedResponse Treatment)> AgainstBothAsync(
        Func<ReminderApiClient, Task<RecordedResponse>> request)
    {
        var monolithUserId = await _fixture.SeedUserAsync();
        var treatmentUserId = await _fixture.SeedUserAsync();

        using var monolith = _fixture.ClientFor(ServiceUnderTest.Monolith, monolithUserId);
        using var treatment = _fixture.ClientFor(ServiceUnderTest.Treatment, treatmentUserId);

        return (await request(monolith), await request(treatment));
    }

    [Fact]
    public async Task EmptyList_IsIdentical()
    {
        var (monolith, treatment) = await AgainstBothAsync(client => client.GetAllAsync());

        treatment.Should().BeEquivalentTo(monolith);
    }

    [Fact]
    public async Task CreatedReminder_IsIdenticalApartFromTheGeneratedId()
    {
        var (monolith, treatment) = await AgainstBothAsync(client => client.AddAsync("08:00"));

        WithoutIds(treatment).Should().BeEquivalentTo(WithoutIds(monolith));
    }

    [Fact]
    public async Task MissingReminder_IsIdentical()
    {
        var (monolith, treatment) = await AgainstBothAsync(client => client.GetAsync(MissingReminderId));

        treatment.Should().BeEquivalentTo(monolith);
    }

    [Fact]
    public async Task DeletingMissingReminder_IsIdentical()
    {
        var (monolith, treatment) = await AgainstBothAsync(client => client.DeleteAsync(MissingReminderId));

        treatment.Should().BeEquivalentTo(monolith);
    }

    [Fact]
    public async Task UpdatingMissingReminder_IsIdentical()
    {
        var (monolith, treatment) = await AgainstBothAsync(client => client.UpdateAsync(MissingReminderId, "21:15"));

        treatment.Should().BeEquivalentTo(monolith);
    }

    [Fact]
    public async Task BothServicesSeeReeachOthersWrites_BecauseTheyShareOneDatabase()
    {
        var userId = await _fixture.SeedUserAsync();

        using var monolith = _fixture.ClientFor(ServiceUnderTest.Monolith, userId);
        using var treatment = _fixture.ClientFor(ServiceUnderTest.Treatment, userId);

        await monolith.AddAsync("07:45");
        await treatment.AddAsync("22:10");

        var listedByMonolith = await monolith.GetAllAsync();
        var listedByTreatment = await treatment.GetAllAsync();

        listedByTreatment.Body.Should().Be(listedByMonolith.Body);
        listedByMonolith.Body.Should().Contain("07:45").And.Contain("22:10");
    }
}
