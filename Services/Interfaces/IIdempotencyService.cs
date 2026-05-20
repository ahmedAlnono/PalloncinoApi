namespace Palloncino.Services.Interfaces;
using Palloncino.Services.Implementations;

public interface IIdempotencyService
{
    Task<StoredResponse?> GetExistingResponseAsync(string key, string requestType);
    Task SaveResponseAsync(string key, string requestType, int statusCode, string responseBody);
    Task CleanupOldRecordsAsync(int daysToKeep = 30);
}