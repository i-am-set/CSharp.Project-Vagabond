using System;
using System.Collections.Generic;

namespace ProjectVagabond.Utils
{
    public interface IPoolable
    {
        bool IsPooled { get; set; }
        void Reset();
        void ReturnToPool();
    }

    public static class PoolManager
    {
        public static event Action OnClearAll;

        public static void ClearAll()
        {
            OnClearAll?.Invoke();
        }
    }

    public static class Pool<T> where T : IPoolable, new()
    {
        private static readonly Stack<T> _pool = new Stack<T>();
        public static int MaxCapacity { get; set; } = 1000;

        static Pool()
        {
            PoolManager.OnClearAll += Clear;
        }

        public static T Get()
        {
            T item = _pool.Count > 0 ? _pool.Pop() : new T();
            item.IsPooled = false;
            return item;
        }

        public static void Return(T item)
        {
            if (item == null || item.IsPooled) return;
            item.Reset();
            item.IsPooled = true;
            if (_pool.Count < MaxCapacity)
            {
                _pool.Push(item);
            }
        }

        public static void Clear()
        {
            _pool.Clear();
        }
    }
}