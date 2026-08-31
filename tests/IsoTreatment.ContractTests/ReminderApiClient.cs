using System.Net.Http.Headers;
using System.Text;

namespace IsoTreatment.ContractTests;

public sealed record RecordedResponse(int StatusCode, string Body, string? ContentType, bool HasLocationHeader);

public sealed class ReminderApiClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly string? _token;

    public ReminderApiClient(string baseAddress, string? token)
    {
        _client = new HttpClient { BaseAddress = new Uri(baseAddress), Timeout = TimeSpan.FromSeconds(30) };
        _token = token;
    }

    public Task<RecordedResponse> GetAllAsync() =>
        SendAsync(HttpMethod.Get, "/api/reminder");

    public Task<RecordedResponse> GetAsync(int id) =>
        SendAsync(HttpMethod.Get, $"/api/reminder/{id}");

    public Task<RecordedResponse> AddAsync(string time) =>
        SendAsync(HttpMethod.Post, "/api/reminder", time);

    public Task<RecordedResponse> UpdateAsync(int id, string time) =>
        SendAsync(HttpMethod.Put, $"/api/reminder/{id}", time);

    public Task<RecordedResponse> DeleteAsync(int id) =>
        SendAsync(HttpMethod.Delete, $"/api/reminder/{id}");

    public Task<RecordedResponse> GetAllWithBearerHeaderAsync() =>
        SendAsync(HttpMethod.Get, "/api/reminder", body: null, useAuthorizationHeader: true);

    public Task<RecordedResponse> GetAllWithCanaryAsync() =>
        SendAsync(HttpMethod.Get, "/api/reminder", canaryValue: CanaryHeader.TreatmentValue);

    public Task<RecordedResponse> GetWithBearerHeaderAsync(string path, string? canaryValue = null) =>
        SendAsync(HttpMethod.Get, path, body: null, useAuthorizationHeader: true, canaryValue: canaryValue);

    private async Task<RecordedResponse> SendAsync(
        HttpMethod method,
        string path,
        string? body = null,
        bool useAuthorizationHeader = false,
        string? canaryValue = null)
    {
        using var request = new HttpRequestMessage(method, path);

        if (canaryValue is not null)
        {
            request.Headers.Add(CanaryHeader.Name, canaryValue);
        }

        if (_token is not null)
        {
            if (useAuthorizationHeader)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            }
            else
            {
                request.Headers.Add("Cookie", $"token={_token}");
            }
        }

        if (body is not null)
        {
            request.Content = new StringContent($$"""{"time":"{{body}}"}""", Encoding.UTF8, "application/json");
        }

        using var response = await _client.SendAsync(request);

        return new RecordedResponse(
            (int)response.StatusCode,
            await response.Content.ReadAsStringAsync(),
            response.Content.Headers.ContentType?.ToString(),
            response.Headers.Location is not null);
    }

    public void Dispose() => _client.Dispose();
}
