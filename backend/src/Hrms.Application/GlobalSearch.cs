namespace Hrms.Application;

public sealed record GlobalSearchHit(string Kind, string Title, string Subtitle, string Icon, string Route,
    IReadOnlyDictionary<string, string>? QueryParams = null);

public sealed record GlobalSearchResponse(IReadOnlyList<GlobalSearchHit> Items);

public interface IGlobalSearchService
{
    Task<GlobalSearchResponse> SearchAsync(string query, CancellationToken cancellationToken);
}
