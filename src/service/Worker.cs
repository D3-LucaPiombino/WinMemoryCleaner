using WinMemoryCleaner.Core;

namespace Service
{
    using static Enums.Memory.Area;
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly MemoryHelper _memoryHelper;

        public Worker(
            ILogger<Worker> logger,
            MemoryHelper memoryHelper
        )
        {
            _logger = logger;
            _memoryHelper = memoryHelper;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMinutes(5);
            _logger.LogInformation("Worker starterd at: {time}", DateTimeOffset.Now);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Next clean up will in {interval} at {time}", interval, DateTimeOffset.Now.Add(interval));
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

                    _memoryHelper.Clean(
                        CombinedPageList |
                        ModifiedPageList |
                        ProcessesWorkingSet |
                        StandbyList |
                        //StandbyListLowPriority|
                        SystemWorkingSet
                    );
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Worker stopped.");
                }
            }
        }
    }
}