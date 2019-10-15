﻿//******************************************************************************************************
//  PriorityQueue.cs - Gbtc
//
//  Copyright © 2019, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  10/05/2019 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace gemstone.threading.collections
{
    /// <summary>
    /// Represents a thread-safe prioritized first in-first out (FIFO) collection.
    /// </summary>
    /// <typeparam name="T">The type of elements contained in the queue.</typeparam>
    public sealed class PriorityQueue<T> : IProducerConsumerCollection<T>, IReadOnlyCollection<T>
    {
        #region [ Members ]

        // Fields
        private ConcurrentQueue<T>[] m_queues;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new instance of the <see cref="PriorityQueue{T}"/> class.
        /// </summary>
        public PriorityQueue()
            : this(1)
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="PriorityQueue{T}"/> class.
        /// </summary>
        /// <param name="priorityLevels">The number of priority levels to preallocate in the queue.</param>
        /// <exception cref="ArgumentException"><paramref name="priorityLevels"/> is less than or equal to 0</exception>
        public PriorityQueue(int priorityLevels)
        {
            if (priorityLevels <= 0)
                throw new ArgumentException("Priority queue must have at least one priority level.", nameof(priorityLevels));

            m_queues = new ConcurrentQueue<T>[priorityLevels];

            for (int i = 0; i < priorityLevels; i++)
                m_queues[i] = new ConcurrentQueue<T>();
        }

        /// <summary>
        /// Creates a new instance of the <see cref="PriorityQueue{T}"/> class.
        /// </summary>
        /// <param name="collection">A collection of items to be enqueued at the lowest priority.</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> is null</exception>
        public PriorityQueue(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            if (collection is PriorityQueue<T> priorityQueue)
            {
                InitializeFrom(priorityQueue);
                return;
            }

            m_queues = new ConcurrentQueue<T>[1];
            m_queues[0] = new ConcurrentQueue<T>();

            foreach (T item in collection)
                Enqueue(item, 0);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="PriorityQueue{T}"/> class.
        /// </summary>
        /// <param name="priorityQueue">Another priority queue of items to be enqueued in this queue at the same priority.</param>
        /// <exception cref="ArgumentNullException"><paramref name="priorityQueue"/> is null</exception>
        public PriorityQueue(PriorityQueue<T> priorityQueue)
        {
            if (priorityQueue == null)
                throw new ArgumentNullException(nameof(priorityQueue));

            InitializeFrom(priorityQueue);
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets the number of items in the queue.
        /// </summary>
        public int Count => Queues.Sum(queue => queue.Count);

        /// <summary>
        /// Indicates whether the <see cref="PriorityQueue{T}"/> is empty.
        /// </summary>
        public bool IsEmpty => Queues.All(queue => queue.IsEmpty);

        private ConcurrentQueue<T>[] Queues => Interlocked.CompareExchange(ref m_queues, null, null);

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => throw new NotSupportedException();

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Enqueues an item into the priority queue.
        /// </summary>
        /// <param name="item">The item to be enqueued.</param>
        /// <param name="priority">The priority at which the item should be queued. Larger numbers have higher priority!</param>
        /// <exception cref="ArgumentException"><paramref name="priority"/> is negative</exception>
        /// <remarks>
        /// This priority queue is implemented using an array of <see cref="ConcurrentQueue{T}"/>.
        /// The array index indicates the priority of tasks in each queue. For best performance,
        /// ensure that your code defines all priority levels consecutively, starting from 0.
        /// </remarks>
        public void Enqueue(T item, int priority)
        {
            if (priority < 0)
                throw new ArgumentException("Priority must be a nonnegative integer.", nameof(priority));

            ResizeQueues(priority);
            Queues[priority].Enqueue(item);
        }

        /// <summary>
        /// Dequeues an item from the priority queue.
        /// </summary>
        /// <param name="priority">The priority at which the item should be dequeued.</param>
        /// <param name="result">The item that was dequeued, or the default value if no item was dequeued.</param>
        /// <returns>True if an item was dequeued; false if the queue is empty.</returns>
        public bool TryDequeue(int priority, out T result)
        {
            ConcurrentQueue<T>[] queues = Queues;

            if (priority < 0 || priority >= queues.Length)
            {
                result = default;
                return false;
            }

            ConcurrentQueue<T> queue = queues[priority];
            return queue.TryDequeue(out result);
        }

        /// <summary>
        /// Dequeues an item from the priority queue.
        /// </summary>
        /// <param name="result">The item that was dequeued, or the default value if no item was dequeued.</param>
        /// <returns>True if an item was dequeued; false if the queue is empty.</returns>
        public bool TryDequeue(out T result)
        {
            foreach (ConcurrentQueue<T> queue in Queues.Reverse())
            {
                if (queue.TryDequeue(out result))
                    return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Tries to return an object from the beginning of the <see cref="PriorityQueue{T}"/> without removing it.
        /// </summary>
        /// <param name="priority">The priority at which to peek into the queue.</param>
        /// <param name="result">
        /// When this method returns, result contains an object from the beginning of the
        /// <see cref="PriorityQueue{T}"/> or an unspecified value if the operation failed.
        /// </param>
        /// <returns>true if an object was returned successfully; otherwise, false.</returns>
        public bool TryPeek(int priority, out T result)
        {
            ConcurrentQueue<T>[] queues = Queues;

            if (priority < 0 || priority >= queues.Length)
            {
                result = default;
                return false;
            }

            ConcurrentQueue<T> queue = queues[priority];
            return queue.TryPeek(out result);
        }

        /// <summary>
        /// Tries to return an object from the beginning of the <see cref="PriorityQueue{T}"/> without removing it.
        /// </summary>
        /// <param name="result">
        /// When this method returns, result contains an object from the beginning of the
        /// <see cref="PriorityQueue{T}"/> or an unspecified value if the operation failed.
        /// </param>
        /// <returns>true if an object was returned successfully; otherwise, false.</returns>
        public bool TryPeek(out T result)
        {
            foreach (ConcurrentQueue<T> queue in Queues.Reverse())
            {
                if (queue.TryPeek(out result))
                    return true;
            }

            result = default;
            return false;
        }

        /// <summary>
        /// Copies the <see cref="PriorityQueue{T}"/> elements to an existing
        /// one-dimensional <see cref="Array"/>, starting at the specified array index.
        /// </summary>
        /// <param name="array">
        /// The one-dimensional <see cref="Array"/> that is the destination of the elements copied
        /// from the <see cref="PriorityQueue{T}"/>. The <see cref="Array"/> must
        /// have zero-based indexing.
        /// </param>
        /// <param name="index">The zero-based index in array at which copying begins.</param>
        /// <exception cref="ArgumentNullException">array is a null reference (Nothing in Visual Basic).</exception>
        /// <exception cref="ArgumentOutOfRangeException">index is less than zero.</exception>
        /// <exception cref="ArgumentException">
        /// index is equal to or greater than the length of the array -or- The number of
        /// elements in the source <see cref="PriorityQueue{T}"/> is greater
        /// than the available space from index to the end of the destination array.
        /// </exception>
        public void CopyTo(T[] array, int index) =>
            ToArray().CopyTo(array, index);

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="PriorityQueue{T}"/>.
        /// </summary>
        /// <returns>An enumerator for the contents of the <see cref="PriorityQueue{T}"/>.</returns>
        public IEnumerator<T> GetEnumerator() =>
            Queues.Reverse().SelectMany(queue => queue).GetEnumerator();

        /// <summary>
        /// Copies the elements stored in the <see cref="PriorityQueue{T}"/> to a new array.
        /// </summary>
        /// <returns>A new array containing a snapshot of elements copied from the <see cref="PriorityQueue{T}"/>.</returns>
        public T[] ToArray() =>
            Queues.Reverse().SelectMany(queue => queue).ToArray();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        bool IProducerConsumerCollection<T>.TryAdd(T item)
        {
            // The IProducerConsumerCollection interface doesn't provide
            // priority so we just queue it at the lowest priority
            Enqueue(item, 0);
            return true;
        }

        bool IProducerConsumerCollection<T>.TryTake(out T item) => TryDequeue(out item);

        void ICollection.CopyTo(Array array, int index) =>
            ToArray().CopyTo(array, index);

        private void ResizeQueues(int minSize)
        {
            ConcurrentQueue<T>[] queues = Queues;

            if (queues.Length >= minSize)
                return;

            ConcurrentQueue<T>[] resizedQueues = queues;
            Array.Resize(ref resizedQueues, minSize);

            for (int i = queues.Length; i < minSize; i++)
                resizedQueues[i] = new ConcurrentQueue<T>();

            // Because the queue can only grow so many times before it reaches the minSize,
            // there is an upper bound on the number of iterations even in the worst case;
            // I can't justify the additional complexity of introducing SpinWait into this
            // loop since it's highly unlikely that this will ever yield
            while (true)
            {
                int length = queues.Length;
                queues = Interlocked.CompareExchange(ref m_queues, resizedQueues, queues);

                // ReSharper disable once PossibleNullReferenceException
                if (queues.Length >= minSize)
                    break;

                // If another thread updates the member variable while this thread was in the process of resizing,
                // there will be additional queues in the new array that must not be overwritten
                Array.Copy(queues, length, resizedQueues, length, queues.Length - length);
            }
        }

        private void InitializeFrom(PriorityQueue<T> priorityQueue)
        {
            int priorityLevels = priorityQueue.Queues.Length;
            m_queues = new ConcurrentQueue<T>[priorityLevels];

            for (int i = 0; i < priorityLevels; i++)
            {
                m_queues[i] = new ConcurrentQueue<T>();

                foreach (T item in priorityQueue.Queues[i])
                    m_queues[i].Enqueue(item);
            }
        }

        #endregion
    }
}
