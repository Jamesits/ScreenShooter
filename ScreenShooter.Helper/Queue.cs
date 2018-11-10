using System;
using System.Collections.Generic;

namespace ScreenShooter.Helper
{
    /// <summary>
    /// Simple generic FIFO queue with 2-level priority implementation
    /// </summary>
    /// <typeparam name="T">Any object</typeparam>
    public class Queue<T>
    {
        private readonly List<T> _priorityQueue = new List<T>();
        private readonly List<T> _queue = new List<T>();

        public int Count => _queue.Count + _priorityQueue.Count;

        public void Put(T item, bool isPriority)
        {
            if (isPriority) PutToFirst(item);
            else Put(item);
        }

        public void Put(T item)
        {
            _queue.Add(item);
        }

        public void PutToFirst(T item)
        {
            _priorityQueue.Add(item);
        }

        public T Get()
        {
            T ret;
            try
            {
                ret = _priorityQueue[0];
                _priorityQueue.RemoveAt(0);
                return ret;
            }
            catch (IndexOutOfRangeException)
            {

            }

            try
            {
                ret = _queue[0];
                _queue.RemoveAt(0);
                return ret;
            }
            catch (IndexOutOfRangeException)
            {
                
            }

            throw new IndexOutOfRangeException();
        }
    }
}
