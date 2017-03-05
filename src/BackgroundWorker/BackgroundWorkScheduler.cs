using System;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BackgroundWork
{
    public class BackgroundWorkScheduler : IBackgroundWorkScheduler, IHostedService, IDisposable
    {
        CancellationTokenSource _cancellationTokenSource;
        private ILogger<BackgroundWorkScheduler> _logger;
        private int _workInProgress = 0;
        private BackgroundWorkSchedulerOptions _options;

        public BackgroundWorkScheduler(ILogger<BackgroundWorkScheduler> logger,
                                       IOptions<BackgroundWorkSchedulerOptions> options)
        {
            _cancellationTokenSource = new CancellationTokenSource();

            _logger = logger;
            _options = options.Value;
        }

        private void CancelTasks()
        {
            lock (this)
            {
                try
                {
                    _cancellationTokenSource.Cancel();
                }
                catch(AggregateException ex)
                {
                    foreach (var inner in ex.InnerExceptions)
                    {
                        if (inner is TaskCanceledException cancelled)
                        {
                            _logger.LogInformation("Background work cancelled during shutdown.");
                        }
                        else
                        {
                            _logger.LogError("Error cancelling background work {0}", ex);
                        }
                    }
                }
                catch(Exception ex)
                {
                    _logger.LogError("Exception occured cancelling background work: {0}", ex);
                }
            }
        }

        public void QueueWork(Action<CancellationToken> work)
        {
            if(work == null)
            {
                throw new ArgumentNullException($"Work cannot be null.");
            }

            if(_cancellationTokenSource.IsCancellationRequested)
            {
                //TODO: Exception here might be a bit harsh. Could be better to log and return instead.
                //But it definately lets people know that their work hasn't been queueud.
                throw new InvalidOperationException($"You cannot queue new background work when shutdown has started.");
            }

            //Using Unsafe to avoid copying ExecutionContext. Not sure this is mandatory now.
            //Locking and incrementing in the queued task to make sure we get the thread back to servicing requests
            //as fast as possible.
            ThreadPool.UnsafeQueueUserWorkItem(state =>
            {
                lock(this)
                {
                    if(_cancellationTokenSource.IsCancellationRequested)
                    {
                        return;
                    }
                    else
                    {
                        _workInProgress++;
                    }
                }

                RunWork((Action<CancellationToken>)state);
            }, work);
        }

        private void RunWork(Action<CancellationToken> work)
        {
            //TODO: Maybe we should have  BackgroundWork struct that encapsulates the work, a name, and maybe a timeout value.
            //Then we could wrap a bunch of trace/information messages around the work and give a little bit of control.
            //Not completely convinced it is worth it though, you can always add the info you want in the tasks you queue.
            var token = _cancellationTokenSource.Token;
            try
            {
                work(token);
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException operationCanceled && operationCanceled.CancellationToken == token)
                {
                    _logger.LogInformation($"Work cancelled by throwing {nameof(OperationCanceledException)}");
                    return;
                }

                _logger.LogError($"Exception occured in background task: {ex}");
            }
            finally
            {
                lock(this)
                {
                    _workInProgress--;
                }
            }
        }

        public void Start()
        {
            if(_cancellationTokenSource.IsCancellationRequested)
            {
                //TODO: We can probably allow this and re-instantiate the cancellationTokenSource if there is no work in progress.
                throw new InvalidOperationException($"The {nameof(BackgroundWorkScheduler)} cannot be started again after it has stopped.");
            }
        }

        public void Stop()
        {
            //It will be possible to stop the scheduler outside of normal application shutdown,
            //in which case we need to cancel any current tasks.
            CancelTasks();

            for (int i = 0; i < _options.Timeout.TotalMilliseconds; i++)
            {
                int curentWorkInProgress;
                lock (this)
                {
                    curentWorkInProgress = _workInProgress;
                }

                if (curentWorkInProgress == 0)
                {
                    return;
                }

                Thread.Sleep(1);
            }
            _logger.LogError("Unable to gracefully shutdown all background work.");
        }

        public void Dispose()
        {
            _cancellationTokenSource.Dispose();
        }
    }
}
