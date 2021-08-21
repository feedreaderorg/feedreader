using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeedReader.TaskServer.Tasks
{
    public abstract class TaskBase : IHostedService, IDisposable
    {
        string Name { get; set; }
        TimeSpan MinimalInterval { get; set; }
        ILogger Logger { get; set; }
        CancellationTokenSource CancellationTokenSource { get; set; }
        Task InternalTask { get; set; }

        public TaskBase(string name, TimeSpan minimalInterval, ILogger logger)
        {
            Name = name;
            MinimalInterval = minimalInterval;
            Logger = logger;
        }

        public void Dispose()
        {
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource = new CancellationTokenSource();
            InternalTask = DoTaskAsync(CancellationTokenSource.Token);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            CancellationTokenSource?.Cancel();
            return InternalTask == null ? Task.CompletedTask : InternalTask;
        }

        async Task DoTaskAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation($"Task {Name} has been started.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var startTime = DateTime.Now;
                Logger.LogInformation($"Task {Name} is running once at: {startTime}");

                try
                {
                    await DoTaskOnceAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Task {Name} throws exception: {ex}");
                }

                var endTime = DateTime.Now;
                var elapsed = endTime - startTime;
                Logger.LogInformation($"Task {Name} is finished at: {endTime}, elapsed {elapsed.TotalMinutes} minutes");

                if (!cancellationToken.IsCancellationRequested)
                {
                    if (elapsed < MinimalInterval)
                    {
                        try
                        {
                            Task.Delay(MinimalInterval - elapsed).Wait(cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                }
            }

            Logger.LogInformation($"Task {Name} has been stopped.");
        }

        protected abstract Task DoTaskOnceAsync(CancellationToken cancellationToken);
    }
}
