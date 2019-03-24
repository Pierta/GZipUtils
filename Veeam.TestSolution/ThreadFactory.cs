using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace Veeam.TestSolution
{
    internal class ThreadFactory
    {
        private readonly Semaphore _semaphore = new Semaphore(Environment.ProcessorCount, Environment.ProcessorCount);

        private Action<object> _worker;

        private ConcurrentDictionary<int, Thread> _threads { get; set; }

        public ThreadFactory(Action<object> worker)
        {
            _threads = new ConcurrentDictionary<int, Thread>();
            _worker = worker;
        }

        public bool IsAlive => _threads.Any(t => t.Value.IsAlive);

        public Thread CreateNew()
        {
            _semaphore.WaitOne();

            ParameterizedThreadStart threadDelegate = new ParameterizedThreadStart(_worker);
            threadDelegate += (input) =>
            {
                _threads.TryRemove(Thread.CurrentThread.ManagedThreadId, out Thread threadToRemove);
                _semaphore.Release();
            };
            Thread newThread = new Thread(threadDelegate);
            _threads.TryAdd(newThread.ManagedThreadId, newThread);
            return newThread;
        }
    }
}