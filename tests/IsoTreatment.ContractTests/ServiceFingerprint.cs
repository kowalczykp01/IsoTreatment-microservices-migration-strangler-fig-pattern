using FluentAssertions;

namespace IsoTreatment.ContractTests;

public static class MonolithOnlyPath
{
    public const string UserInfo = "/api/user/info";
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
}
