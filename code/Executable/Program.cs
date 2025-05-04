using Executable;

var builder = Host.CreateApplicationBuilder(args);

// builder.Configuration.Sources.Clear();
// builder.Configuration.AddJsonFile("/config/appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

var isContainer = bool.Parse(builder.Configuration["DOTNET_RUNNING_IN_CONTAINER"] ?? "false");
string settingsFilePath = isContainer is true
    ? "/config/appsettings.json"
    : "config/appsettings.json";

builder.Configuration
    .AddJsonFile(settingsFilePath, optional: false, reloadOnChange: true);

builder.Services.Configure<Config>(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
