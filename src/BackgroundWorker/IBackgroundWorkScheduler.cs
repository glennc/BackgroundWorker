using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace BackgroundWork
{
    public interface IBackgroundWorkScheduler
    {
        void QueueWork(Action<CancellationToken> work);
    }
}
