using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ArcMapConditions.App.Services;

/// <summary>Downloads the raw HTML of the map-conditions page.</summary>
public sealed class MapConditionsService : IDisposable
{
    public const string PageUrl = "https://arcraiders.com/map-conditions";

    private readonly HttpClient _http;

    public MapConditionsService()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        // A normal browser UA avoids naive bot filters.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ArcMapConditions/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html");
    }

    /// <summary>Returns the page HTML, or null on any network/HTTP failure.</summary>
    public async Task<string?> FetchHtmlAsync(CancellationToken ct = default)
    {
        try
        {
            using HttpResponseMessage response = await _http
                .GetAsync(PageUrl, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Properly handle cancellation
            return null;
        }
        catch (Exception ex)
        {
            // Log the exception for debugging purposes
            System.Diagnostics.Debug.WriteLine($"Error fetching HTML: {ex.Message}");
            // Swallow: the caller keeps showing the last good data.
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
