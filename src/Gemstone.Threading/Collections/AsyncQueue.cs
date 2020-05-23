﻿//******************************************************************************************************
//  AsyncQueue.cs - Gbtc
//
//  Copyright © 2012, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  11/08/2012 - J. Ritchie Carroll / Stephen C. Wills
//       Generated original version of source code.
//  12/13/2012 - Starlynn Danyelle Gilliam
//       Modified Header.
//
//******************************************************************************************************

using System;
using System.Collections.Concurrent;
using System.Threading;
using Gemstone.EventHandlerExtensions;
using Gemstone.Threading.SynchronizedOperations;

namespace Gemstone.Threading.Collections
{
    /// <summary>
    /// Creates a fast, light-weight asynchronous processing queue with very low contention.
    /// </summary>
    /// <typeparam name="T">Type of items to process.</typeparam>
    public class AsyncQueue<T>
    {
        #region [ Members ]

        // Events

        /// <summary>
        /// Event that is raised if an exception is encountered while attempting to processing an item in the <see cref="AsyncQueue{T}"/>.
        /// </summary>
        /// <remarks>
        /// Processing will not stop for any exceptions thrown by user processing function, but exceptions will be exposed through this event.
        /// </remarks>
        public event EventHandler<EventArgs<Exception>>? ProcessException;

        // Fields
        private readonly ConcurrentQueue<T> m_asyncQueue;
        private Action<T>? m_processItemFunction;
        private readonly ISynchronizedOperation m_processItemOperation;
        private int m_enabled;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new <see cref="AsyncQueue{T}"/>.
        /// </summary>
        public AsyncQueue()
        {
            m_asyncQueue = new ConcurrentQueue<T>();
            m_processItemOperation = new ShortSynchronizedOperation(TryProcessItem, OnProcessException);
            m_enabled = 1;
        }

        /// <summary>
        /// Creates a new <see cref="AsyncQueue{T}"/>.
        /// </summary>
        /// <param name="synchronizedOperationType">The type of synchronized operation to use to process items in the queue.</param>
        public AsyncQueue(SynchronizedOperationType synchronizedOperationType)
        {
            m_asyncQueue = new ConcurrentQueue<T>();
            m_enabled = 1;

            m_processItemOperation = synchronizedOperationType switch
            {
                SynchronizedOperationType.Short => new ShortSynchronizedOperation(TryProcessItem, OnProcessException),
                SynchronizedOperationType.Long => new LongSynchronizedOperation(TryProcessItem, OnProcessException),
                SynchronizedOperationType.LongBackground => new LongSynchronizedOperation(TryProcessItem, OnProcessException) { IsBackground = true },
                _ => throw new ArgumentOutOfRangeException(nameof(synchronizedOperationType)),
            };
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets the total number of items currently in the queue.
        /// </summary>
        public int Count => m_asyncQueue.Count;

        /// <summary>
        /// Gets or sets item processing function.
        /// </summary>
        public Action<T>? ProcessItemFunction
        {
            get => Interlocked.CompareExchange(ref m_processItemFunction, null, null);
            set => Interlocked.Exchange(ref m_processItemFunction, value);
        }

        /// <summary>
        /// Gets or sets flag that enables or disables processing.
        /// </summary>
        public bool Enabled
        {
            get => Interlocked.CompareExchange(ref m_enabled, 0, 0) != 0;
            set => Interlocked.Exchange(ref m_enabled, value ? 1 : 0);
        }

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Enqueues an item for processing.
        /// </summary>
        /// <param name="item">Item to be queued for processing.</param>
        public void Enqueue(T item)
        {
            if (m_processItemFunction is null)
                throw new NullReferenceException("No item processing function has been assigned - cannot enqueue item for processing.");

            // Queue item for processing
            if (Enabled)
            {
                m_asyncQueue.Enqueue(item);
                m_processItemOperation.RunAsync();
            }
        }

        // Attempts to dequeue and process an item from the queue.
        private void TryProcessItem()
        {
            if (m_asyncQueue.TryDequeue(out T item))
            {
                if (Enabled)
                    m_processItemFunction?.Invoke(item);

                if (!m_asyncQueue.IsEmpty)
                    m_processItemOperation.RunAsync();
            }
        }

        // Raises the ProcessException event.
        private void OnProcessException(Exception ex) => ProcessException?.SafeInvoke(this, new EventArgs<Exception>(ex));

        #endregion
    }
}
