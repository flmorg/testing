﻿using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Entities.Deluge.Response;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.ContentBlocker;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Deluge;

public partial class DelugeService
{
    /// <inheritdoc/>
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        hash = hash.ToLowerInvariant();

        DownloadStatus? download = await _client.GetTorrentStatus(hash);
        BlockFilesResult result = new();
        
        if (download?.Hash is null)
        {
            _logger.LogDebug("failed to find torrent {hash} in the download client", hash);
            return result;
        }
        
        result.Found = true;
        
        if (ignoredDownloads.Count > 0 && download.ShouldIgnore(ignoredDownloads))
        {
            _logger.LogInformation("skip | download is ignored | {name}", download.Name);
            return result;
        }

        result.IsPrivate = download.Private;
        
        var contentBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();
        
        if (contentBlockerConfig.IgnorePrivate && download.Private)
        {
            // ignore private trackers
            _logger.LogDebug("skip files check | download is private | {name}", download.Name);
            return result;
        }
        
        DelugeContents? contents = null;

        try
        {
            contents = await _client.GetTorrentFiles(hash);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "failed to find torrent {hash} in the download client", hash);
        }

        if (contents is null)
        {
            return result;
        }
        
        Dictionary<int, int> priorities = [];
        bool hasPriorityUpdates = false;
        long totalFiles = 0;
        long totalUnwantedFiles = 0;
        
        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);

        ProcessFiles(contents.Contents, (name, file) =>
        {
            totalFiles++;
            int priority = file.Priority;

            if (file.Priority is 0)
            {
                totalUnwantedFiles++;
            }

            if (file.Priority is not 0 && !_filenameEvaluator.IsValid(name, blocklistType, patterns, regexes))
            {
                totalUnwantedFiles++;
                priority = 0;
                hasPriorityUpdates = true;
                _logger.LogInformation("unwanted file found | {file}", file.Path);
            }
            
            priorities.Add(file.Index, priority);
        });

        if (!hasPriorityUpdates)
        {
            return result;
        }
        
        _logger.LogDebug("changing priorities | torrent {hash}", hash);

        List<int> sortedPriorities = priorities
            .OrderBy(x => x.Key)
            .Select(x => x.Value)
            .ToList();

        if (totalUnwantedFiles == totalFiles)
        {
            result.ShouldRemove = true;
        }

        await _dryRunInterceptor.InterceptAsync(ChangeFilesPriority, hash, sortedPriorities);

        return result;
    }
    
    protected virtual async Task ChangeFilesPriority(string hash, List<int> sortedPriorities)
    {
        await _client.ChangeFilesPriority(hash, sortedPriorities);
    }
}