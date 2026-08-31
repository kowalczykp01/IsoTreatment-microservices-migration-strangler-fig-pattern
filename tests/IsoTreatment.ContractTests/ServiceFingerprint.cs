using FluentAssertions;

namespace IsoTreatment.ContractTests;

public static class CanaryHeader
{
    public const string Name = "X-Canary";
    public const string TreatmentValue = "treatment";
}

public enum RespondingService
{
    Unknown,
    Monolith,
    Treatment
}

public static class ServiceFingerprint
{
    public static RespondingService FromBearerHeaderResponse(RecordedResponse response) =>
        response.StatusCode switch
        {
            500 => RespondingService.Monolith,
            200 => RespondingService.Treatment,
            _ => RespondingService.Unknown
        };

    public static void ShouldDistinguishServices(RecordedResponse monolith, RecordedResponse treatment)
    {
        FromBearerHeaderResponse(monolith).Should().Be(RespondingService.Monolith);
        FromBearerHeaderResponse(treatment).Should().Be(RespondingService.Treatment);
    }
}
