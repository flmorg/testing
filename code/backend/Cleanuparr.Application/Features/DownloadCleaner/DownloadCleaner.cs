using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Events;
using Cleanuparr.Infrastructure.Features.Arr;
using Cleanuparr.Infrastructure.Features.Arr.Interfaces;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DownloadClient;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Data.Models.Arr.Queue;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using LogContext = Serilog.Context.LogContext;

namespace Cleanuparr.Application.Features.DownloadCleaner;

public sealed class DownloadCleaner : GenericHandler
{
    private readonly HashSet<string> _excludedHashes = [];
    
    public DownloadCleaner(
        ILogger<DownloadCleaner> logger,
        DataContext dataContext,
        IMemoryCache cache,
        IBus messageBus,
        ArrClientFactory arrClientFactory,
        ArrQueueIterator arrArrQueueIterator,
        DownloadServiceFactory downloadServiceFactory,
        EventPublisher eventPublisher
    ) : base(
        logger, dataContext, cache, messageBus,
        arrClientFactory, arrArrQueueIterator, downloadServiceFactory, eventPublisher
    )
    {
    }
    
    protected override async Task ExecuteInternalAsync()
    {
        var downloadServices = await GetInitializedDownloadServicesAsync();

        if (downloadServices.Count is 0)
        {
            _logger.LogWarning("Processing skipped because no download clients are configured");
            return;
        }

        var config = ContextProvider.Get<DownloadCleanerConfig>();
        
        bool isUnlinkedEnabled = config.UnlinkedEnabled && !string.IsNullOrEmpty(config.UnlinkedTargetCategory) && config.UnlinkedCategories.Count > 0;
        bool isCleaningEnabled = config.Categories.Count > 0;
        
        if (!isUnlinkedEnabled && !isCleaningEnabled)
        {
            _logger.LogWarning("{name} is not configured properly", nameof(DownloadCleaner));
            return;
        }
        
        IReadOnlyList<string> ignoredDownloads = ContextProvider.Get<GeneralConfig>(nameof(GeneralConfig)).IgnoredDownloads;
        
        // Process each client separately
        var allDownloads = new List<object>();
        foreach (var downloadService in downloadServices)
        {
            try
            {
                await downloadService.LoginAsync();
                var clientDownloads = await downloadService.GetSeedingDownloads();
                if (clientDownloads?.Count > 0)
                {
                    allDownloads.AddRange(clientDownloads);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get seeding downloads from download client");
            }
        }

        if (allDownloads.Count == 0)
        {
            _logger.LogDebug("no seeding downloads found");
            return;
        }
        
        _logger.LogTrace("found {count} seeding downloads", allDownloads.Count);
        
        // List<object>? downloadsToChangeCategory = null;
        List<Tuple<IDownloadService, List<object>>> downloadServiceWithDownloads = [];

        if (isUnlinkedEnabled)
        {
            // Create category for all clients
            foreach (var downloadService in downloadServices)
            {
                try
                {
                    await downloadService.CreateCategoryAsync(config.UnlinkedTargetCategory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create category for download client");
                }
            }
            
            // Get downloads to change category
            foreach (var downloadService in downloadServices)
            {
                try
                {
                    var clientDownloads = downloadService.FilterDownloadsToChangeCategoryAsync(allDownloads, config.UnlinkedCategories);
                    if (clientDownloads?.Count > 0)
                    {
                        downloadServiceWithDownloads.Add(Tuple.Create(downloadService, clientDownloads));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to filter downloads for category change");
                }
            }
        }

        // wait for the downloads to appear in the arr queue
        await Task.Delay(10 * 1000);

        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Sonarr)), InstanceType.Sonarr, true);
        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Radarr)), InstanceType.Radarr, true);
        await ProcessArrConfigAsync(ContextProvider.Get<ArrConfig>(nameof(InstanceType.Lidarr)), InstanceType.Lidarr, true);

        if (isUnlinkedEnabled && downloadServiceWithDownloads.Count > 0)
        {
            _logger.LogInformation("Found {count} potential downloads to change category", downloadServiceWithDownloads.Sum(x => x.Item2.Count));
            
            // Process each client with its own filtered downloads
            foreach (var (downloadService, downloadsToChangeCategory) in downloadServiceWithDownloads)
            {
                try
                {
                    await downloadService.ChangeCategoryForNoHardLinksAsync(downloadsToChangeCategory, _excludedHashes, ignoredDownloads);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to change category for download client {clientName}", downloadService.ClientConfig.Name);
                }
            }
            
            _logger.LogInformation("Finished changing category");
        }
        
        if (config.Categories.Count is 0)
        {
            return;
        }
        
        // Get downloads to clean
        downloadServiceWithDownloads = [];
        foreach (var downloadService in downloadServices)
        {
            try
            {
                var clientDownloads = downloadService.FilterDownloadsToBeCleanedAsync(allDownloads, config.Categories);
                if (clientDownloads?.Count > 0)
                {
                    downloadServiceWithDownloads.Add(Tuple.Create(downloadService, clientDownloads));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to filter downloads for cleaning for download client {clientName}", downloadService.ClientConfig.Name);
            }
        }
        
        // release unused objects
        allDownloads = null;

        _logger.LogInformation("found {count} potential downloads to clean", downloadServiceWithDownloads.Sum(x => x.Item2.Count));
        
        // Process cleaning for each client
        foreach (var (downloadService, downloadsToClean) in downloadServiceWithDownloads)
        {
            try
            {
                await downloadService.CleanDownloadsAsync(downloadsToClean, config.Categories, _excludedHashes, ignoredDownloads);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean downloads for download client {clientName}", downloadService.ClientConfig.Name);
            }
        }
        
        _logger.LogInformation("finished cleaning downloads");

        foreach (var downloadService in downloadServices)
        {
            downloadService.Dispose();
        }
    }

    protected override async Task ProcessInstanceAsync(ArrInstance instance, InstanceType instanceType)
    {
        using var _ = LogContext.PushProperty(LogProperties.Category, instanceType.ToString());
        
        IArrClient arrClient = _arrClientFactory.GetClient(instanceType);
        
        await _arrArrQueueIterator.Iterate(arrClient, instance, async items =>
        {
            var groups = items
                .Where(x => !string.IsNullOrEmpty(x.DownloadId))
                .GroupBy(x => x.DownloadId)
                .ToList();

            foreach (QueueRecord record in groups.Select(group => group.First()))
            {
                _excludedHashes.Add(record.DownloadId.ToLowerInvariant());
            }
        });
    }
}