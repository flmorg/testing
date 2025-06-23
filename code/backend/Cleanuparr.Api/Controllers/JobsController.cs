using Cleanuparr.Api.Models;
using Cleanuparr.Infrastructure.Models;
using Infrastructure.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cleanuparr.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IJobManagementService _jobManagementService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(IJobManagementService jobManagementService, ILogger<JobsController> logger)
    {
        _jobManagementService = jobManagementService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllJobs()
    {
        try
        {
            var result = await _jobManagementService.GetAllJobs();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all jobs");
            return StatusCode(500, "An error occurred while retrieving jobs");
        }
    }

    [HttpGet("{jobType}")]
    public async Task<IActionResult> GetJob(JobType jobType)
    {
        try
        {
            var jobInfo = await _jobManagementService.GetJob(jobType);
            
            if (jobInfo.Status == "Not Found")
            {
                return NotFound($"Job '{jobType}' not found");
            }
            return Ok(jobInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job {jobType}", jobType);
            return StatusCode(500, $"An error occurred while retrieving job '{jobType}'");
        }
    }

    [HttpPost("{jobType}/start")]
    public async Task<IActionResult> StartJob(JobType jobType, [FromBody] ScheduleRequest scheduleRequest = null)
    {
        try
        {
            // Get the schedule from the request body if provided
            JobSchedule jobSchedule = scheduleRequest.Schedule;
            
            var result = await _jobManagementService.StartJob(jobType, jobSchedule);
            
            if (!result)
            {
                return BadRequest($"Failed to start job '{jobType}'");
            }
            return Ok(new { Message = $"Job '{jobType}' started successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting job {jobType}", jobType);
            return StatusCode(500, $"An error occurred while starting job '{jobType}'");
        }
    }

    [HttpPost("{jobType}/stop")]
    public async Task<IActionResult> StopJob(JobType jobType)
    {
        try
        {
            var result = await _jobManagementService.StopJob(jobType);
            
            if (!result)
            {
                return BadRequest($"Failed to stop job '{jobType}'");
            }
            return Ok(new { Message = $"Job '{jobType}' stopped successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping job {jobType}", jobType);
            return StatusCode(500, $"An error occurred while stopping job '{jobType}'");
        }
    }

    [HttpPost("{jobType}/pause")]
    public async Task<IActionResult> PauseJob(JobType jobType)
    {
        try
        {
            var result = await _jobManagementService.PauseJob(jobType);
            
            if (!result)
            {
                return BadRequest($"Failed to pause job '{jobType}'");
            }
            return Ok(new { Message = $"Job '{jobType}' paused successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing job {jobType}", jobType);
            return StatusCode(500, $"An error occurred while pausing job '{jobType}'");
        }
    }

    [HttpPost("{jobType}/resume")]
    public async Task<IActionResult> ResumeJob(JobType jobType)
    {
        try
        {
            var result = await _jobManagementService.ResumeJob(jobType);
            
            if (!result)
            {
                return BadRequest($"Failed to resume job '{jobType}'");
            }
            return Ok(new { Message = $"Job '{jobType}' resumed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming job {jobType}", jobType);
            return StatusCode(500, $"An error occurred while resuming job '{jobType}'");
        }
    }

    [HttpPut("{jobType}/schedule")]
    public async Task<IActionResult> UpdateJobSchedule(JobType jobType, [FromBody] ScheduleRequest scheduleRequest)
    {
        if (scheduleRequest?.Schedule == null)
        {
            return BadRequest("Schedule is required");
        }

        try
        {
            var result = await _jobManagementService.UpdateJobSchedule(jobType, scheduleRequest.Schedule);
            
            if (!result)
            {
                return BadRequest($"Failed to update schedule for job '{jobType}'");
            }
            return Ok(new { Message = $"Job '{jobType}' schedule updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job {jobType} schedule", jobType);
            return StatusCode(500, $"An error occurred while updating schedule for job '{jobType}'");
        }
    }
}
