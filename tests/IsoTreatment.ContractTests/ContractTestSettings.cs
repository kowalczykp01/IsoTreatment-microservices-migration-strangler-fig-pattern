namespace IsoTreatment.ContractTests;

public sealed class ContractTestSettings
{
    public string GatewayBaseAddress { get; }
    public string MonolithBaseAddress { get; }
    public string TreatmentBaseAddress { get; }
    public string ConnectionString { get; }
    public string SigningKey { get; }
    public string Issuer { get; }
    public string Audience { get; }

    private ContractTestSettings(
        string gatewayBaseAddress,
        string monolithBaseAddress,
        string treatmentBaseAddress,
        string connectionString,
        string signingKey,
        string issuer,
        string audience)
    {
        GatewayBaseAddress = gatewayBaseAddress;
        MonolithBaseAddress = monolithBaseAddress;
        TreatmentBaseAddress = treatmentBaseAddress;
        ConnectionString = connectionString;
        SigningKey = signingKey;
        Issuer = issuer;
        Audience = audience;
    }

    public static ContractTestSettings FromEnvironment()
    {
        var signingKey = Read("AUTHENTICATION_SIGNING_KEY");
        var databasePassword = Read("MSSQL_SA_PASSWORD");

        var databaseHost = Environment.GetEnvironmentVariable("CONTRACT_TESTS_DB_HOST") ?? "localhost,14330";

        return new ContractTestSettings(
            Environment.GetEnvironmentVariable("CONTRACT_TESTS_GATEWAY_URL") ?? "http://localhost:8080",
            Environment.GetEnvironmentVariable("CONTRACT_TESTS_MONOLITH_URL") ?? "http://localhost:8081",
            Environment.GetEnvironmentVariable("CONTRACT_TESTS_TREATMENT_URL") ?? "http://localhost:8082",
            $"Server={databaseHost};Database=IsoTreatmentProcessSupport;User Id=sa;"
            + $"Password={databasePassword};Encrypt=true;TrustServerCertificate=true;",
            signingKey,
            Environment.GetEnvironmentVariable("AUTHENTICATION_ISSUER") ?? "isotreatment-users-issuer",
            Environment.GetEnvironmentVariable("AUTHENTICATION_AUDIENCE") ?? "isotreatment-users-audience");
    }

    private static string Read(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException(
            $"Environment variable '{name}' is not set. These tests run against the live stack; "
            + "start it and export the secrets first:\n"
            + "    docker compose up -d\n"
            + "    set -a; . ./.env; set +a\n"
            + "    dotnet test tests/IsoTreatment.ContractTests");
}
