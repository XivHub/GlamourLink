using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;

namespace GlamourLink.Eorzea;

public enum FetchStatus
{
    Ok,
    NotFound,
    Refused,
    Network,
    Timeout,
    Cancelled,
    Malformed,
}

public sealed record FetchResult(FetchStatus Status, EcGlamour? Glamour, string Message);

/// <summary>
/// Talks to Eorzea Collection's undocumented glamour JSON endpoint. Its 200
/// responses and its error pages both carry `Content-Type: text/html`, so
/// success is never read from content type; see <see cref="FetchAsync"/>.
/// </summary>
public sealed class EorzeaCollectionClient : IDisposable
{
    private const int RequestTimeoutSeconds = 15;
    private const long MaxResponseBytes = 1024 * 1024; // 1 MiB

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly Configuration _cfg;
    private readonly IPluginLog _log;

    public EorzeaCollectionClient(Configuration cfg, IPluginLog log)
    {
        _cfg = cfg;
        _log = log;

        // Only what never changes lives here. The User-Agent is configurable
        // and is set per request in FetchAsync so a settings change takes
        // effect on the next fetch without rebuilding this client.
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
    }

    public async Task<FetchResult> FetchAsync(int id, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        int? statusCode = null;

        FetchResult Result(FetchStatus status, EcGlamour? glamour, string message)
        {
            _log.Debug($"GlamourLink fetch id={id} status={(statusCode?.ToString() ?? "n/a")} elapsedMs={stopwatch.ElapsedMilliseconds}");
            return new FetchResult(status, glamour, message);
        }

        var url = $"{_cfg.ApiBaseUrl.TrimEnd('/')}/{id}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        if (string.IsNullOrEmpty(_cfg.UserAgent) || !TryParseUserAgent(request, _cfg.UserAgent))
        {
            TryParseUserAgent(request, Configuration.DefaultUserAgent);
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(RequestTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return Result(FetchStatus.Network, null, $"Could not reach Eorzea Collection: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            return ct.IsCancellationRequested
                ? Result(FetchStatus.Cancelled, null, "")
                : Result(FetchStatus.Timeout, null, "Eorzea Collection did not answer within 15 seconds.");
        }

        using (response)
        {
            statusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                return statusCode switch
                {
                    404 => Result(FetchStatus.NotFound, null, $"Eorzea Collection has no glamour #{id}."),
                    403 => Result(FetchStatus.Refused, null,
                        "Eorzea Collection refused the request (403). Its bot check may have changed; see the User-Agent setting."),
                    429 => Result(FetchStatus.Refused, null,
                        "Eorzea Collection is rate limiting (429). Wait a minute and try again."),
                    503 => Result(FetchStatus.Refused, null,
                        "Eorzea Collection is challenging or unavailable (503)."),
                    _ => Result(FetchStatus.Refused, null,
                        $"Eorzea Collection returned an unexpected status ({statusCode})."),
                };
            }

            (bool Ok, string Body) body;
            try
            {
                body = await ReadBodyAsync(response, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return ct.IsCancellationRequested
                    ? Result(FetchStatus.Cancelled, null, "")
                    : Result(FetchStatus.Timeout, null, "Eorzea Collection did not answer within 15 seconds.");
            }

            if (!body.Ok)
            {
                return Result(FetchStatus.Refused, null, "Eorzea Collection returned an unexpected page.");
            }

            var trimmed = body.Body.TrimStart('﻿').TrimStart();
            if (trimmed.Length == 0 || trimmed[0] != '{')
            {
                return Result(FetchStatus.Refused, null, "Eorzea Collection returned a web page instead of data.");
            }

            try
            {
                var glamour = JsonSerializer.Deserialize<EcGlamour>(trimmed, JsonOptions);
                if (glamour?.Gear is null)
                {
                    return Result(FetchStatus.Malformed, null, "Eorzea Collection sent data GlamourLink could not read.");
                }

                return Result(FetchStatus.Ok, glamour, "");
            }
            catch (JsonException)
            {
                return Result(FetchStatus.Malformed, null, "Eorzea Collection sent data GlamourLink could not read.");
            }
        }
    }

    /// <summary>Reads the response body up to <see cref="MaxResponseBytes"/>; Ok is false past the cap.</summary>
    private static async Task<(bool Ok, string Body)> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            long total = 0;
            int read;
            while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
            {
                total += read;
                if (total > MaxResponseBytes)
                {
                    return (false, "");
                }

                buffer.Write(chunk, 0, read);
            }

            return (true, Encoding.UTF8.GetString(buffer.ToArray()));
        }
    }

    private static bool TryParseUserAgent(HttpRequestMessage request, string userAgent)
    {
        try
        {
            request.Headers.UserAgent.ParseAdd(userAgent);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public void Dispose() => _http.Dispose();
}
