using Service;
using WinMemoryCleaner.Core;

var applicationName = "Windows Memory Cache Cleaner";

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, config) =>
    {
        ctx.HostingEnvironment.ApplicationName = applicationName;
    })
    .UseWindowsService(options =>
    {
        options.ServiceName = applicationName;
    })
    .ConfigureServices(services =>
    {
        services
            .AddSingleton(typeof(LogHelper<>), typeof(LogHelper<>))
            .AddSingleton<MemoryHelper>()
            .AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();
