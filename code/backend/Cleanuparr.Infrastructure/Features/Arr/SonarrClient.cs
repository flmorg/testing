﻿using System.Text;
using Cleanuparr.Domain.Entities.Sonarr;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.ItemStriker;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Data.Models.Arr;
using Data.Models.Arr.Queue;
using Infrastructure.Interceptors;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Series = Cleanuparr.Domain.Entities.Sonarr.Series;

namespace Cleanuparr.Infrastructure.Features.Arr;

public class SonarrClient : ArrClient, ISonarrClient
{
    public SonarrClient(
        ILogger<SonarrClient> logger,
        IHttpClientFactory httpClientFactory,
        IStriker striker,
        IDryRunInterceptor dryRunInterceptor
    ) : base(logger, httpClientFactory, striker, dryRunInterceptor)
    {
    }
    
    protected override string GetQueueUrlPath()
    {
        return "/api/v3/queue";
    }

    protected override string GetQueueUrlQuery(int page)
    {
        return $"page={page}&pageSize=200&includeUnknownSeriesItems=true&includeSeries=true&includeEpisode=true";
    }

    protected override string GetQueueDeleteUrlPath(long recordId)
    {
        return $"/api/v3/queue/{recordId}";
    }

    protected override string GetQueueDeleteUrlQuery(bool removeFromClient)
    {
        string query = "blocklist=true&skipRedownload=true&changeCategory=false";
        query += removeFromClient ? "&removeFromClient=true" : "&removeFromClient=false";
        
        return query;
    }

    public override async Task SearchItemsAsync(ArrInstance arrInstance, HashSet<SearchItem>? items)
    {
        if (items?.Count is null or 0)
        {
            return;
        }

        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v3/command";
        
        foreach (SonarrCommand command in GetSearchCommands(items.Cast<SonarrSearchItem>().ToHashSet()))
        {
            using HttpRequestMessage request = new(HttpMethod.Post, uriBuilder.Uri);
            request.Content = new StringContent(
                JsonConvert.SerializeObject(command, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                Encoding.UTF8,
                "application/json"
            );
            SetApiKey(request, arrInstance.ApiKey);

            string? logContext = await ComputeCommandLogContextAsync(arrInstance, command, command.SearchType);

            try
            {
                HttpResponseMessage? response = await _dryRunInterceptor.InterceptAsync<HttpResponseMessage>(SendRequestAsync, request);
                response?.Dispose();
                
                _logger.LogInformation("{log}", GetSearchLog(command.SearchType, arrInstance.Url, command, true, logContext));
            }
            catch
            {
                _logger.LogError("{log}", GetSearchLog(command.SearchType, arrInstance.Url, command, false, logContext));
                throw;
            }
        }
    }

    public override bool IsRecordValid(QueueRecord record)
    {
        if (record.EpisodeId is 0 || record.SeriesId is 0)
        {
            _logger.LogDebug("skip | episode id and/or series id missing | {title}", record.Title);
            return false;
        }

        return base.IsRecordValid(record);
    }

    private static string GetSearchLog(
        SonarrSearchType searchType,
        Uri instanceUrl,
        SonarrCommand command,
        bool success,
        string? logContext
    )
    {
        string status = success ? "triggered" : "failed";
        
        return searchType switch
        {
            SonarrSearchType.Episode =>
                $"episodes search {status} | {instanceUrl} | {logContext ?? $"episode ids: {string.Join(',', command.EpisodeIds)}"}",
            SonarrSearchType.Season =>
                $"season search {status} | {instanceUrl} | {logContext ?? $"season: {command.SeasonNumber} series id: {command.SeriesId}"}",
            SonarrSearchType.Series => $"series search {status} | {instanceUrl} | {logContext ?? $"series id: {command.SeriesId}"}",
            _ => throw new ArgumentOutOfRangeException(nameof(searchType), searchType, null)
        };
    }

    private async Task<string?> ComputeCommandLogContextAsync(ArrInstance arrInstance, SonarrCommand command, SonarrSearchType searchType)
    {
        try
        {
            StringBuilder log = new();

            if (searchType is SonarrSearchType.Episode)
            {
                var episodes = await GetEpisodesAsync(arrInstance, command.EpisodeIds);

                if (episodes?.Count is null or 0)
                {
                    return null;
                }

                var seriesIds = episodes
                    .Select(x => x.SeriesId)
                    .Distinct()
                    .ToList();

                List<Series> series = [];

                foreach (long id in seriesIds)
                {
                    Series? show = await GetSeriesAsync(arrInstance, id);

                    if (show is null)
                    {
                        return null;
                    }

                    series.Add(show);
                }

                foreach (var group in command.EpisodeIds.GroupBy(id => episodes.First(x => x.Id == id).SeriesId))
                {
                    var show = series.First(x => x.Id == group.Key);
                    var episode = episodes
                        .Where(ep => group.Any(x => x == ep.Id))
                        .OrderBy(x => x.SeasonNumber)
                        .ThenBy(x => x.EpisodeNumber)
                        .Select(x => $"S{x.SeasonNumber.ToString().PadLeft(2, '0')}E{x.EpisodeNumber.ToString().PadLeft(2, '0')}")
                        .ToList();

                    log.Append($"[{show.Title} {string.Join(',', episode)}]");
                }
            }

            if (searchType is SonarrSearchType.Season)
            {
                Series? show = await GetSeriesAsync(arrInstance, command.SeriesId.Value);

                if (show is null)
                {
                    return null;
                }

                log.Append($"[{show.Title} season {command.SeasonNumber}]");
            }

            if (searchType is SonarrSearchType.Series)
            {
                Series? show = await GetSeriesAsync(arrInstance, command.SeriesId.Value);

                if (show is null)
                {
                    return null;
                }

                log.Append($"[{show.Title}]");
            }

            return log.ToString();
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to compute log context");
        }

        return null;
    }

    private async Task<List<Episode>?> GetEpisodesAsync(ArrInstance arrInstance, List<long> episodeIds)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v3/episode";
        uriBuilder.Query = string.Join('&', episodeIds.Select(x => $"episodeIds={x}"));
        
        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
                
        string responseBody = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<List<Episode>>(responseBody);
    }

    private async Task<Series?> GetSeriesAsync(ArrInstance arrInstance, long seriesId)
    {
        UriBuilder uriBuilder = new(arrInstance.Url);
        uriBuilder.Path = $"{uriBuilder.Path.TrimEnd('/')}/api/v3/series/{seriesId}";
        
        using HttpRequestMessage request = new(HttpMethod.Get, uriBuilder.Uri);
        SetApiKey(request, arrInstance.ApiKey);

        using HttpResponseMessage response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
                
        string responseBody = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<Series>(responseBody);
    }

    private List<SonarrCommand> GetSearchCommands(HashSet<SonarrSearchItem> items)
    {
        const string episodeSearch = "EpisodeSearch";
        const string seasonSearch = "SeasonSearch";
        const string seriesSearch = "SeriesSearch";
        
        List<SonarrCommand> commands = new();

        foreach (SonarrSearchItem item in items)
        {
            SonarrCommand command = item.SearchType is SonarrSearchType.Episode
                ? commands.FirstOrDefault() ?? new() { Name = episodeSearch, EpisodeIds = new() }
                : new();
            
            switch (item.SearchType)
            {
                case SonarrSearchType.Episode when command.EpisodeIds is null:
                    command.EpisodeIds = [item.Id];
                    break;
                
                case SonarrSearchType.Episode when command.EpisodeIds is not null:
                    command.EpisodeIds.Add(item.Id);
                    break;
                
                case SonarrSearchType.Season:
                    command.Name = seasonSearch;
                    command.SeasonNumber = item.Id;
                    command.SeriesId = ((SonarrSearchItem)item).SeriesId;
                    break;
                
                case SonarrSearchType.Series:
                    command.Name = seriesSearch;
                    command.SeriesId = item.Id;
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(item.SearchType), item.SearchType, null);
            }

            if (item.SearchType is SonarrSearchType.Episode && commands.Count > 0)
            {
                // only one command will be generated for episodes search
                continue;
            }
            
            command.SearchType = item.SearchType;
            commands.Add(command);
        }
        
        return commands;
    }
}