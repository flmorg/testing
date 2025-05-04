using Microsoft.Extensions.Options;

namespace Executable;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptionsMonitor<Config> _config;


    public Worker(ILogger<Worker> logger, IServiceScopeFactory serviceScopeFactory, IOptionsMonitor<Config> config)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _config = config;
        
        _config.OnChange(OnConfigChanged);
    }

    private void OnConfigChanged(Config arg1, string? arg2)
    {
        _logger.LogInformation("!! Value changed !!: {Variable}", arg1.Variable);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<Config>>();
            
            _logger.LogInformation("Snapshot variable: {Variable}", config.Value.Variable);
            _logger.LogInformation("Monitor variable: {Variable}", _config.CurrentValue.Variable);
            
            await Task.Delay(5000, stoppingToken);
        }
    }
}
