using Cleanuparr.Api.Models;
using Cleanuparr.Application.Features.Arr.Dtos;
using Cleanuparr.Application.Features.DownloadClient.Dtos;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Helpers;
using Cleanuparr.Infrastructure.Http.DynamicHttpClientSystem;
using Cleanuparr.Infrastructure.Logging;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Utilities;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration;
using Cleanuparr.Persistence.Models.Configuration.Arr;
using Cleanuparr.Persistence.Models.Configuration.ContentBlocker;
using Cleanuparr.Persistence.Models.Configuration.DownloadCleaner;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.Configuration.Notification;
using Cleanuparr.Persistence.Models.Configuration.QueueCleaner;
using Infrastructure.Services.Interfaces;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Cleanuparr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : ControllerBase
{
    private readonly ILogger<ConfigurationController> _logger;
    private readonly DataContext _dataContext;
    private readonly LoggingConfigManager _loggingConfigManager;
    private readonly IJobManagementService _jobManagementService;
    private readonly MemoryCache _cache;

    public ConfigurationController(
        ILogger<ConfigurationController> logger,
        DataContext dataContext,
        LoggingConfigManager loggingConfigManager,
        IJobManagementService jobManagementService,
        MemoryCache cache
    )
    {
        _logger = logger;
        _dataContext = dataContext;
        _loggingConfigManager = loggingConfigManager;
        _jobManagementService = jobManagementService;
        _cache = cache;
    }

    [HttpGet("queue_cleaner")]
    public async Task<IActionResult> GetQueueCleanerConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.QueueCleanerConfigs
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpGet("content_blocker")]
    public async Task<IActionResult> GetContentBlockerConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ContentBlockerConfigs
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("download_cleaner")]
    public async Task<IActionResult> GetDownloadCleanerConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.DownloadCleanerConfigs
                .Include(x => x.Categories)
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("download_client")]
    public async Task<IActionResult> GetDownloadClientConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var clients = await _dataContext.DownloadClients
                .AsNoTracking()
                .ToListAsync();
            
            // Return in the expected format with clients wrapper
            var config = new { clients = clients };
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpPost("download_client")]
    public async Task<IActionResult> CreateDownloadClientConfig([FromBody] CreateDownloadClientDto newClient)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate the configuration
            newClient.Validate();
            
            // Create the full config from the DTO
            var clientConfig = new DownloadClientConfig
            {
                Enabled = newClient.Enabled,
                Name = newClient.Name,
                TypeName = newClient.TypeName,
                Type = newClient.Type,
                Host = newClient.Host,
                Username = newClient.Username,
                Password = newClient.Password,
                UrlBase = newClient.UrlBase
            };
            
            // Add the new client to the database
            _dataContext.DownloadClients.Add(clientConfig);
            await _dataContext.SaveChangesAsync();
            
            return CreatedAtAction(nameof(GetDownloadClientConfig), new { id = clientConfig.Id }, clientConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create download client");
            return StatusCode(500, "Failed to create download client configuration");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpPut("download_client/{id}")]
    public async Task<IActionResult> UpdateDownloadClientConfig(Guid id, [FromBody] DownloadClientConfig updatedClient)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Find the existing download client
            var existingClient = await _dataContext.DownloadClients
                .FirstOrDefaultAsync(c => c.Id == id);
                
            if (existingClient == null)
            {
                return NotFound($"Download client with ID {id} not found");
            }
            
            // Ensure the ID in the path matches the entity being updated
            updatedClient = updatedClient with { Id = id };
            
            // Apply updates from DTO
            updatedClient.Adapt(existingClient);
            
            // Persist the configuration
            await _dataContext.SaveChangesAsync();
            
            return Ok(existingClient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update download client with ID {Id}", id);
            return StatusCode(500, "Failed to update download client configuration");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpDelete("download_client/{id}")]
    public async Task<IActionResult> DeleteDownloadClientConfig(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Find the existing download client
            var existingClient = await _dataContext.DownloadClients
                .FirstOrDefaultAsync(c => c.Id == id);
                
            if (existingClient == null)
            {
                return NotFound($"Download client with ID {id} not found");
            }
            
            // Remove the client from the database
            _dataContext.DownloadClients.Remove(existingClient);
            await _dataContext.SaveChangesAsync();
            
            // Clean up any registered HTTP client configuration
            var dynamicHttpClientFactory = HttpContext.RequestServices
                .GetRequiredService<IDynamicHttpClientFactory>();
                
            var clientName = $"DownloadClient_{id}";
            dynamicHttpClientFactory.UnregisterConfiguration(clientName);
            
            _logger.LogInformation("Removed HTTP client configuration for deleted download client {ClientName}", clientName);
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete download client with ID {Id}", id);
            return StatusCode(500, "Failed to delete download client configuration");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("general")]
    public async Task<IActionResult> GetGeneralConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.GeneralConfigs
                .AsNoTracking()
                .FirstAsync();
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("sonarr")]
    public async Task<IActionResult> GetSonarrConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .Include(x => x.Instances)
                .AsNoTracking()
                .FirstAsync(x => x.Type == InstanceType.Sonarr);
            return Ok(config.Adapt<ArrConfigDto>());
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("radarr")]
    public async Task<IActionResult> GetRadarrConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .Include(x => x.Instances)
                .AsNoTracking()
                .FirstAsync(x => x.Type == InstanceType.Radarr);
            return Ok(config.Adapt<ArrConfigDto>());
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("lidarr")]
    public async Task<IActionResult> GetLidarrConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var config = await _dataContext.ArrConfigs
                .Include(x => x.Instances)
                .AsNoTracking()
                .FirstAsync(x => x.Type == InstanceType.Lidarr);
            return Ok(config.Adapt<ArrConfigDto>());
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotificationsConfig()
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            var notifiarrConfig = await _dataContext.NotifiarrConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync();
            
            var appriseConfig = await _dataContext.AppriseConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync();
            
            // Return in the expected format with wrapper object
            var config = new 
            { 
                notifiarr = notifiarrConfig,
                apprise = appriseConfig
            };
            return Ok(config);
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    public class UpdateNotificationConfigDto
    {
        public NotifiarrConfig? Notifiarr { get; set; }
        public AppriseConfig? Apprise { get; set; }
    }

    [HttpPut("notifications")]
    public async Task<IActionResult> UpdateNotificationsConfig([FromBody] UpdateNotificationConfigDto newConfig)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Update Notifiarr config if provided
            if (newConfig.Notifiarr != null)
            {
                var existingNotifiarr = await _dataContext.NotifiarrConfigs.FirstOrDefaultAsync();
                if (existingNotifiarr != null)
                {
                    // Apply updates from DTO, excluding the ID property to avoid EF key modification error
                    var config = new TypeAdapterConfig();
                    config.NewConfig<NotifiarrConfig, NotifiarrConfig>()
                        .Ignore(dest => dest.Id);
                    
                    newConfig.Notifiarr.Adapt(existingNotifiarr, config);
                }
                else
                {
                    _dataContext.NotifiarrConfigs.Add(newConfig.Notifiarr);
                }
            }

            // Update Apprise config if provided
            if (newConfig.Apprise != null)
            {
                var existingApprise = await _dataContext.AppriseConfigs.FirstOrDefaultAsync();
                if (existingApprise != null)
                {
                    // Apply updates from DTO, excluding the ID property to avoid EF key modification error
                    var config = new TypeAdapterConfig();
                    config.NewConfig<AppriseConfig, AppriseConfig>()
                        .Ignore(dest => dest.Id);
                    
                    newConfig.Apprise.Adapt(existingApprise, config);
                }
                else
                {
                    _dataContext.AppriseConfigs.Add(newConfig.Apprise);
                }
            }

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = "Notifications configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Notifications configuration");
            return StatusCode(500, "Failed to save Notifications configuration");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("queue_cleaner")]
    public async Task<IActionResult> UpdateQueueCleanerConfig([FromBody] QueueCleanerConfig newConfig)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate the configuration
            newConfig.Validate();

            // Validate cron expression if present
            if (!string.IsNullOrEmpty(newConfig.CronExpression))
            {
                CronValidationHelper.ValidateCronExpression(newConfig.CronExpression);
            }

            // Get existing config
            var oldConfig = await _dataContext.QueueCleanerConfigs
                .FirstAsync();

            // Apply updates from DTO, excluding the ID property to avoid EF key modification error
            var adapterConfig = new TypeAdapterConfig();
            adapterConfig.NewConfig<QueueCleanerConfig, QueueCleanerConfig>()
                .Ignore(dest => dest.Id);
            
            newConfig.Adapt(oldConfig, adapterConfig);

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            // Update the scheduler based on configuration changes
            await UpdateJobSchedule(oldConfig, JobType.QueueCleaner);

            return Ok(new { Message = "QueueCleaner configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save QueueCleaner configuration");
            return StatusCode(500, "Failed to save QueueCleaner configuration");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpPut("content_blocker")]
    public async Task<IActionResult> UpdateContentBlockerConfig([FromBody] ContentBlockerConfig newConfig)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate the configuration
            newConfig.Validate();

            // Validate cron expression if present
            if (!string.IsNullOrEmpty(newConfig.CronExpression))
            {
                CronValidationHelper.ValidateCronExpression(newConfig.CronExpression, JobType.ContentBlocker);
            }

            // Get existing config
            var oldConfig = await _dataContext.ContentBlockerConfigs
                .FirstAsync();

            // Apply updates from DTO, excluding the ID property to avoid EF key modification error
            var config = new TypeAdapterConfig();
            config.NewConfig<ContentBlockerConfig, ContentBlockerConfig>()
                .Ignore(dest => dest.Id);
            
            newConfig.Adapt(oldConfig, config);

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            // Update the scheduler based on configuration changes
            await UpdateJobSchedule(oldConfig, JobType.ContentBlocker);

            return Ok(new { Message = "ContentBlocker configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save ContentBlocker configuration");
            return StatusCode(500, "Failed to save ContentBlocker configuration");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("download_cleaner")]
    public async Task<IActionResult> UpdateDownloadCleanerConfig([FromBody] UpdateDownloadCleanerConfigDto newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate cron expression if present
            if (!string.IsNullOrEmpty(newConfigDto.CronExpression))
            {
                CronValidationHelper.ValidateCronExpression(newConfigDto.CronExpression);
            }
            
            // Validate categories
            if (newConfigDto.Enabled && newConfigDto.Categories.Any())
            {
                // Check for duplicate category names
                if (newConfigDto.Categories.GroupBy(x => x.Name).Any(x => x.Count() > 1))
                {
                    return BadRequest("Duplicate clean categories found");
                }
                
                // Validate each category
                foreach (var categoryDto in newConfigDto.Categories)
                {
                    if (string.IsNullOrEmpty(categoryDto.Name?.Trim()))
                    {
                        return BadRequest("Category name cannot be empty");
                    }
                    
                    if (categoryDto.MaxRatio < 0 && categoryDto.MaxSeedTime < 0)
                    {
                        return BadRequest("Both max ratio and max seed time cannot be disabled");
                    }
                    
                    if (categoryDto.MinSeedTime < 0)
                    {
                        return BadRequest("Min seed time cannot be negative");
                    }
                }
            }
            
            // Validate unlinked settings if enabled
            if (newConfigDto.UnlinkedEnabled)
            {
                if (string.IsNullOrEmpty(newConfigDto.UnlinkedTargetCategory))
                {
                    return BadRequest("Unlinked target category is required");
                }

                if (newConfigDto.UnlinkedCategories?.Count is null or 0)
                {
                    return BadRequest("No unlinked categories configured");
                }

                if (newConfigDto.UnlinkedCategories.Contains(newConfigDto.UnlinkedTargetCategory))
                {
                    return BadRequest("The unlinked target category should not be present in unlinked categories");
                }

                if (newConfigDto.UnlinkedCategories.Any(string.IsNullOrEmpty))
                {
                    return BadRequest("Empty unlinked category filter found");
                }

                if (!string.IsNullOrEmpty(newConfigDto.UnlinkedIgnoredRootDir) && !Directory.Exists(newConfigDto.UnlinkedIgnoredRootDir))
                {
                    return BadRequest($"{newConfigDto.UnlinkedIgnoredRootDir} root directory does not exist");
                }
            }

            // Get existing config
            var oldConfig = await _dataContext.DownloadCleanerConfigs
                .Include(x => x.Categories)
                .FirstAsync();

            // Update the main properties from DTO

            oldConfig.Enabled = newConfigDto.Enabled;
            oldConfig.CronExpression = newConfigDto.CronExpression;
            oldConfig.UseAdvancedScheduling = newConfigDto.UseAdvancedScheduling;
            oldConfig.DeletePrivate = newConfigDto.DeletePrivate;
            oldConfig.UnlinkedEnabled = newConfigDto.UnlinkedEnabled;
            oldConfig.UnlinkedTargetCategory = newConfigDto.UnlinkedTargetCategory;
            oldConfig.UnlinkedUseTag = newConfigDto.UnlinkedUseTag;
            oldConfig.UnlinkedIgnoredRootDir = newConfigDto.UnlinkedIgnoredRootDir;
            oldConfig.UnlinkedCategories = newConfigDto.UnlinkedCategories;

            // Handle Categories collection separately to avoid EF tracking issues
            // Clear existing categories
            _dataContext.CleanCategories.RemoveRange(oldConfig.Categories);
            _dataContext.DownloadCleanerConfigs.Update(oldConfig);
            
            // Add new categories
            foreach (var categoryDto in newConfigDto.Categories)
            {
                _dataContext.CleanCategories.Add(new CleanCategory
                {
                    Name = categoryDto.Name,
                    MaxRatio = categoryDto.MaxRatio,
                    MinSeedTime = categoryDto.MinSeedTime,
                    MaxSeedTime = categoryDto.MaxSeedTime,
                    DownloadCleanerConfigId = oldConfig.Id
                });
            }

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            // Update the scheduler based on configuration changes
            await UpdateJobSchedule(oldConfig, JobType.DownloadCleaner);

            return Ok(new { Message = "DownloadCleaner configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save DownloadCleaner configuration");
            return StatusCode(500, "Failed to save DownloadCleaner configuration");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    [HttpPut("general")]
    public async Task<IActionResult> UpdateGeneralConfig([FromBody] GeneralConfig newConfig)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Validate the configuration
            newConfig.Validate();

            // Get existing config
            var oldConfig = await _dataContext.GeneralConfigs
                .FirstAsync();

            // Apply updates from DTO, excluding the ID property to avoid EF key modification error
            var config = new TypeAdapterConfig();
            config.NewConfig<GeneralConfig, GeneralConfig>()
                .Ignore(dest => dest.Id);

            if (oldConfig.DryRun && !newConfig.DryRun)
            {
                foreach (string strikeType in Enum.GetNames(typeof(StrikeType)))
                {
                    var keys = _cache.Keys
                        .Where(key => key.ToString()?.StartsWith(strikeType, StringComparison.InvariantCultureIgnoreCase) is true)
                        .ToList();

                    foreach (object key in keys)
                    {
                        _cache.Remove(key);
                    }
                    
                    _logger.LogTrace("Removed all cache entries for strike type: {StrikeType}", strikeType);
                }
            }
            
            newConfig.Adapt(oldConfig, config);

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            // Update all HTTP client configurations with new general settings
            var dynamicHttpClientFactory = HttpContext.RequestServices
                .GetRequiredService<IDynamicHttpClientFactory>();
            
            dynamicHttpClientFactory.UpdateAllClientsFromGeneralConfig(oldConfig);
            
            _logger.LogInformation("Updated all HTTP client configurations with new general settings");

            // Set the logging level based on the new configuration
            _loggingConfigManager.SetLogLevel(newConfig.LogLevel);

            return Ok(new { Message = "General configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save General configuration");
            return StatusCode(500, "Failed to save General configuration");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("sonarr")]
    public async Task<IActionResult> UpdateSonarrConfig([FromBody] UpdateSonarrConfigDto newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get existing config
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Sonarr);

            config.FailedImportMaxStrikes = newConfigDto.FailedImportMaxStrikes;

            // Validate the configuration
            config.Validate();

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = "Sonarr configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Sonarr configuration");
            return StatusCode(500, "Failed to save Sonarr configuration");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("radarr")]
    public async Task<IActionResult> UpdateRadarrConfig([FromBody] UpdateRadarrConfigDto newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get existing config
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Radarr);

            config.FailedImportMaxStrikes = newConfigDto.FailedImportMaxStrikes;

            // Validate the configuration
            config.Validate();

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = "Radarr configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Radarr configuration");
            return StatusCode(500, "Failed to save Radarr configuration");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("lidarr")]
    public async Task<IActionResult> UpdateLidarrConfig([FromBody] UpdateLidarrConfigDto newConfigDto)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get existing config
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Lidarr);

            config.FailedImportMaxStrikes = newConfigDto.FailedImportMaxStrikes;

            // Validate the configuration
            config.Validate();

            // Persist the configuration
            await _dataContext.SaveChangesAsync();

            return Ok(new { Message = "Lidarr configuration updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Lidarr configuration");
            return StatusCode(500, "Failed to save Lidarr configuration");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
    
    /// <summary>
    /// Updates a job schedule based on configuration changes
    /// </summary>
    /// <param name="config">The job configuration</param>
    /// <param name="jobType">The type of job to update</param>
    private async Task UpdateJobSchedule(IJobConfig config, JobType jobType)
    {
        if (config.Enabled)
        {
            // Get the cron expression based on the specific config type
            if (!string.IsNullOrEmpty(config.CronExpression))
            {
                // If the job is enabled, update its schedule with the configured cron expression
                _logger.LogInformation("{name} is enabled, updating job schedule with cron expression: {CronExpression}",
                    jobType.ToString(), config.CronExpression);

                // Create a Quartz job schedule with the cron expression
                await _jobManagementService.StartJob(jobType, null, config.CronExpression);
            }
            else
            {
                _logger.LogWarning("{name} is enabled, but no cron expression was found in the configuration", jobType.ToString());
            }

            return;
        }

        // If the job is disabled, stop it
        _logger.LogInformation("{name} is disabled, stopping the job", jobType.ToString());
        await _jobManagementService.StopJob(jobType);
    }

    [HttpPost("sonarr/instances")]
    public async Task<IActionResult> CreateSonarrInstance([FromBody] CreateArrInstanceDto newInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Sonarr config to add the instance to
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Sonarr);

            // Create the new instance
            var instance = new ArrInstance
            {
                Enabled = newInstance.Enabled,
                Name = newInstance.Name,
                Url = new Uri(newInstance.Url),
                ApiKey = newInstance.ApiKey,
                ArrConfigId = config.Id,
            };
            
            // Add to the config's instances collection
            // config.Instances.Add(instance);
            await _dataContext.ArrInstances.AddAsync(instance);
            // Save changes
            await _dataContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetSonarrConfig), new { id = instance.Id }, instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Sonarr instance");
            return StatusCode(500, "Failed to create Sonarr instance");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("sonarr/instances/{id}")]
    public async Task<IActionResult> UpdateSonarrInstance(Guid id, [FromBody] CreateArrInstanceDto updatedInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Sonarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Sonarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Sonarr instance with ID {id} not found");
            }

            // Update the instance properties
            instance.Enabled = updatedInstance.Enabled;
            instance.Name = updatedInstance.Name;
            instance.Url = new Uri(updatedInstance.Url);
            instance.ApiKey = updatedInstance.ApiKey;

            await _dataContext.SaveChangesAsync();

            return Ok(instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Sonarr instance with ID {Id}", id);
            return StatusCode(500, "Failed to update Sonarr instance");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("sonarr/instances/{id}")]
    public async Task<IActionResult> DeleteSonarrInstance(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Sonarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Sonarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Sonarr instance with ID {id} not found");
            }

            // Remove the instance
            config.Instances.Remove(instance);
            await _dataContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Sonarr instance with ID {Id}", id);
            return StatusCode(500, "Failed to delete Sonarr instance");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("radarr/instances")]
    public async Task<IActionResult> CreateRadarrInstance([FromBody] CreateArrInstanceDto newInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Radarr config to add the instance to
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Radarr);

            // Create the new instance
            var instance = new ArrInstance
            {
                Enabled = newInstance.Enabled,
                Name = newInstance.Name,
                Url = new Uri(newInstance.Url),
                ApiKey = newInstance.ApiKey,
                ArrConfigId = config.Id,
            };
            
            // Add to the config's instances collection
            await _dataContext.ArrInstances.AddAsync(instance);
            // Save changes
            await _dataContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRadarrConfig), new { id = instance.Id }, instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Radarr instance");
            return StatusCode(500, "Failed to create Radarr instance");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("radarr/instances/{id}")]
    public async Task<IActionResult> UpdateRadarrInstance(Guid id, [FromBody] CreateArrInstanceDto updatedInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Radarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Radarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Radarr instance with ID {id} not found");
            }

            // Update the instance properties
            instance.Enabled = updatedInstance.Enabled;
            instance.Name = updatedInstance.Name;
            instance.Url = new Uri(updatedInstance.Url);
            instance.ApiKey = updatedInstance.ApiKey;

            await _dataContext.SaveChangesAsync();

            return Ok(instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Radarr instance with ID {Id}", id);
            return StatusCode(500, "Failed to update Radarr instance");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("radarr/instances/{id}")]
    public async Task<IActionResult> DeleteRadarrInstance(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Radarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Radarr);
            
            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Radarr instance with ID {id} not found");
            }
            
            // Remove the instance
            config.Instances.Remove(instance);
            await _dataContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Radarr instance with ID {Id}", id);
            return StatusCode(500, "Failed to delete Radarr instance");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPost("lidarr/instances")]
    public async Task<IActionResult> CreateLidarrInstance([FromBody] CreateArrInstanceDto newInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Lidarr config to add the instance to
            var config = await _dataContext.ArrConfigs
                .FirstAsync(x => x.Type == InstanceType.Lidarr);

            // Create the new instance
            var instance = new ArrInstance
            {
                Enabled = newInstance.Enabled,
                Name = newInstance.Name,
                Url = new Uri(newInstance.Url),
                ApiKey = newInstance.ApiKey,
                ArrConfigId = config.Id,
            };
            
            // Add to the config's instances collection
            // config.Instances.Add(instance);
            await _dataContext.ArrInstances.AddAsync(instance);
            // Save changes
            await _dataContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLidarrConfig), new { id = instance.Id }, instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Lidarr instance");
            return StatusCode(500, "Failed to create Lidarr instance");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpPut("lidarr/instances/{id}")]
    public async Task<IActionResult> UpdateLidarrInstance(Guid id, [FromBody] CreateArrInstanceDto updatedInstance)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Lidarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Lidarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Lidarr instance with ID {id} not found");
            }

            // Update the instance properties
            instance.Enabled = updatedInstance.Enabled;
            instance.Name = updatedInstance.Name;
            instance.Url = new Uri(updatedInstance.Url);
            instance.ApiKey = updatedInstance.ApiKey;

            await _dataContext.SaveChangesAsync();

            return Ok(instance.Adapt<ArrInstanceDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Lidarr instance with ID {Id}", id);
            return StatusCode(500, "Failed to update Lidarr instance");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }

    [HttpDelete("lidarr/instances/{id}")]
    public async Task<IActionResult> DeleteLidarrInstance(Guid id)
    {
        await DataContext.Lock.WaitAsync();
        try
        {
            // Get the Lidarr config and find the instance
            var config = await _dataContext.ArrConfigs
                .Include(c => c.Instances)
                .FirstAsync(x => x.Type == InstanceType.Lidarr);

            var instance = config.Instances.FirstOrDefault(i => i.Id == id);
            if (instance == null)
            {
                return NotFound($"Lidarr instance with ID {id} not found");
            }

            // Remove the instance
            config.Instances.Remove(instance);
            await _dataContext.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Lidarr instance with ID {Id}", id);
            return StatusCode(500, "Failed to delete Lidarr instance");
        }
        finally
        {
            DataContext.Lock.Release();
        }
    }
}