using FolderWatcherService.src;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

await new HostBuilder()
    .ConfigureAppConfiguration((hostContext, configApp) =>
    {
        configApp
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        //.AddJsonFile("C:\\Users\\shipa\\source\\repos\\FolderWatcherService\\appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureServices((hostContext, services) =>
    {
        services.AddLogging();
        services.AddHostedService<DirectoryWatcher>();
    })
    .ConfigureLogging((hostContext, configLogging) =>
     {
         configLogging.AddConsole();
         configLogging.AddDebug();
     })
    .RunConsoleAsync();