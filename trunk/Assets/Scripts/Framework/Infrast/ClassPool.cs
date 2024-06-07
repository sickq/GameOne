namespace Infrast
{
    using System;
    using System.Collections.Generic;

    public class ClassPool<T> where T : class
    {
        private const int DEFAULT_CAPACITY = 16;

        private Func<T, T> _constructFunc;
        private Action<T> _releaseFunc;
        private Queue<T> _poolQueue;
        private int _capacity;

        public ClassPool(int capacity = DEFAULT_CAPACITY, int reserved = 0)
            : this(null, null, capacity, reserved)
        {

        }

        public ClassPool(Func<T, T> instantiateFunc, int capacity = DEFAULT_CAPACITY, int reserved = 0)
            : this(instantiateFunc, null, capacity, reserved)
        {

        }

        public ClassPool(Func<T, T> constructFunc, Action<T> releaseFunc, int capacity = DEFAULT_CAPACITY, int reserved = 0)
        {
            _constructFunc = constructFunc;
            _releaseFunc = releaseFunc;
            _poolQueue = new Queue<T>(capacity);
            _capacity = capacity;
            if (_constructFunc != null)
            {
                for (int i = 0; i < reserved && i < _capacity; i++)
                {
                    var item = _constructFunc(null);
                    _poolQueue.Enqueue(item);
                }
            }
        }

        public T New()
        {
            return _constructFunc != null ? _constructFunc(null) : null;
        }

        public T Reset(T item)
        {
            if (_releaseFunc != null)
            {
                _releaseFunc.Invoke(item);
            }
            return _constructFunc != null ? _constructFunc(item) : item;
        }

        public T TryGet()
        {
            T item = null;
            if (_poolQueue.Count > 0)
            {
                item = _poolQueue.Dequeue();
            }
#if ENABLE_STATS
            else
            {
                trygetCountMissed++;
            }
            trygetCountTotal++;
#endif
            return _constructFunc != null ? _constructFunc(item) : item;
        }

        public void Recycle(T item)
        {
            if (_releaseFunc != null)
            {
                _releaseFunc.Invoke(item);
            }
            if (_poolQueue.Count < _capacity)
            {
                _poolQueue.Enqueue(item);
            }
#if ENABLE_STATS
            else
            {
                recycleCountMissed++;
            }
            recycleCountTotal++;
#endif
        }

#if ENABLE_STATS
        public int trygetCountTotal { get; private set; }
        public int trygetCountMissed { get; private set; }

        public int recycleCountTotal { get; private set; }
        public int recycleCountMissed { get; private set; }

        public void PrintStats(string prefix)
        {
            if (trygetCountMissed > _capacity || recycleCountMissed > 0)
            {
                Logging.WarningFormat("{0}    TryGet:{1}/{2}    Recycle:{3}/{4}    Capacity:{5}/{6}", prefix,
                    trygetCountTotal, trygetCountMissed, recycleCountTotal, recycleCountMissed, _poolQueue.Count, _capacity);
            }
        }

        public void ResetStats()
        {
            trygetCountTotal = trygetCountMissed = 0;
            recycleCountTotal = recycleCountMissed = 0;
        }
#endif
    }
}