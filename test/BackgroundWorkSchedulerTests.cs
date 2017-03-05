using System;
using System.Threading;
using BackgroundWork;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Http;

namespace BackgroundWorker.Tests
{
    public class BackgroundWorkSchedulerTests
    {
        private Mock<ILogger<BackgroundWorkScheduler>> _logger;
        private Mock<IOptions<BackgroundWorkSchedulerOptions>> _options;

        public BackgroundWorkSchedulerTests()
        {
            _logger = new Mock<ILogger<BackgroundWorkScheduler>>();
            _options = new Mock<IOptions<BackgroundWorkSchedulerOptions>>();
            _options.SetupGet(x => x.Value).Returns(new BackgroundWorkSchedulerOptions { Timeout = TimeSpan.FromSeconds(2) });
        }

        [Fact]
        public void EnqueueThrowsWhenWorkNull()
        {
            var schedulerToTest = new BackgroundWorkScheduler(_logger.Object, _options.Object);
            Assert.Throws<ArgumentNullException>(() => schedulerToTest.QueueWork(null));
        }

        [Fact]
        public void CannotQueueWorkAfterStop()
        {
            var schedulerToTest = new BackgroundWorkScheduler(_logger.Object, _options.Object);

            schedulerToTest.Stop();

            Assert.Throws<InvalidOperationException>(() => schedulerToTest.QueueWork((ct) => { return; }));
        }

        [Fact]
        public void CanThrowInBackgroundTask()
        {
            var schedulerToTest = new BackgroundWorkScheduler(_logger.Object, _options.Object);

            schedulerToTest.QueueWork((ct) =>
            {
                throw new Exception();
            });

            //Give time for task to actually start....yuck.
            Thread.Sleep(500);

            ((IHostedService)schedulerToTest).Stop();

            //TODO: Needing to check logging is probably an indicator
            //that some refactoring would help.
            _logger.Verify(logger => logger.Log(LogLevel.Error,
                                                It.IsAny<EventId>(),
                                                It.IsAny<object>(),
                                                It.IsAny<Exception>(),
                                                It.IsAny<Func<object, Exception, string>>()), Times.AtLeastOnce());
        }

        [Fact]
        public void CanShutdownWellBehavingOnGoingWork()
        {
            var schedulerToTest = new BackgroundWorkScheduler(_logger.Object, _options.Object);

            bool complete = false;
            schedulerToTest.QueueWork((ct) =>
            {
                ct.WaitHandle.WaitOne();
                complete = true;
            });

            Thread.Sleep(500);

            ((IHostedService)schedulerToTest).Stop();
            Assert.True(complete);
        }

        [Fact]
        public void CannotShutdownIgnorantTaskLogsError()
        {
            var schedulerToTest = new BackgroundWorkScheduler(_logger.Object, _options.Object);

            schedulerToTest.QueueWork((ct) =>
            {
                while (true) { Thread.Sleep(1); };
            });

            Thread.Sleep(500);

            ((IHostedService)schedulerToTest).Stop();

            _logger.Verify(logger => logger.Log(LogLevel.Error,
                                                It.IsAny<EventId>(),
                                                It.IsAny<object>(),
                                                It.IsAny<Exception>(),
                                                It.IsAny<Func<object, Exception, string>>()), Times.AtLeastOnce());
        }

        [Fact]
        public void CanUseThrowOnCancel()
        {
            var schedulerToTest = new BackgroundWorkScheduler(_logger.Object, _options.Object);

            schedulerToTest.QueueWork((ct) =>
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    Thread.Sleep(1);
                };
            });

            Thread.Sleep(500);
            ((IHostedService)schedulerToTest).Stop();

            _logger.Verify(logger => logger.Log(LogLevel.Information,
                                    It.IsAny<EventId>(),
                                    It.IsAny<object>(),
                                    It.IsAny<Exception>(),
                                    It.IsAny<Func<object, Exception, string>>()), Times.AtLeastOnce());
            _logger.Verify(logger => logger.Log(LogLevel.Error,
                                    It.IsAny<EventId>(),
                                    It.IsAny<object>(),
                                    It.IsAny<Exception>(),
                                    It.IsAny<Func<object, Exception, string>>()), Times.Never());
        }
    }
}
