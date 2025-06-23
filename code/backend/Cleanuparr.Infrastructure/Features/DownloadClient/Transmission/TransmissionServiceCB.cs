﻿using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Extensions;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Persistence.Models.Configuration.ContentBlocker;
using Microsoft.Extensions.Logging;
using Transmission.API.RPC.Entity;

namespace Cleanuparr.Infrastructure.Features.DownloadClient.Transmission;

public partial class TransmissionService
{
    /// <inheritdoc/>
    public override async Task<BlockFilesResult> BlockUnwantedFilesAsync(string hash, IReadOnlyList<string> ignoredDownloads)
    {
        TorrentInfo? download = await GetTorrentAsync(hash);
        BlockFilesResult result = new();

        if (download?.FileStats is null || download.FileStats.Length == 0)
        {
            _logger.LogDebug("failed to find torrent {hash} in the download client", hash);
            return result;
        }
        
        result.Found = true;

        if (download.Files is null)
        {
            _logger.LogDebug("torrent {hash} has no files", hash);
            return result;
        }
        
        if (ignoredDownloads.Count > 0 && download.ShouldIgnore(ignoredDownloads))
        {
            _logger.LogDebug("skip | download is ignored | {name}", download.Name);
            return result;
        }

        bool isPrivate = download.IsPrivate ?? false;
        result.IsPrivate = isPrivate;
        
        var contentBlockerConfig = ContextProvider.Get<ContentBlockerConfig>();
        
        if (contentBlockerConfig.IgnorePrivate && isPrivate)
        {
            // ignore private trackers
            _logger.LogDebug("skip files check | download is private | {name}", download.Name);
            return result;
        }

        List<long> unwantedFiles = [];
        long totalFiles = 0;
        long totalUnwantedFiles = 0;
        
        InstanceType instanceType = (InstanceType)ContextProvider.Get<object>(nameof(InstanceType));
        BlocklistType blocklistType = _blocklistProvider.GetBlocklistType(instanceType);
        ConcurrentBag<string> patterns = _blocklistProvider.GetPatterns(instanceType);
        ConcurrentBag<Regex> regexes = _blocklistProvider.GetRegexes(instanceType);
        
        for (int i = 0; i < download.Files.Length; i++)
        {
            if (download.FileStats?[i].Wanted == null)
            {
                continue;
            }

            totalFiles++;
            
            if (!download.FileStats[i].Wanted.Value)
            {
                totalUnwantedFiles++;
                continue;
            }

            if (_filenameEvaluator.IsValid(download.Files[i].Name, blocklistType, patterns, regexes))
            {
                continue;
            }
            
            _logger.LogInformation("unwanted file found | {file}", download.Files[i].Name);
            unwantedFiles.Add(i);
            totalUnwantedFiles++;
        }

        if (unwantedFiles.Count is 0)
        {
            return result;
        }

        if (totalUnwantedFiles == totalFiles)
        {
            result.ShouldRemove = true;
        }
        
        _logger.LogDebug("marking {count} unwanted files as skipped for {name}", totalUnwantedFiles, download.Name);

        await _dryRunInterceptor.InterceptAsync(SetUnwantedFiles, download.Id, unwantedFiles.ToArray());

        return result;
    }
}