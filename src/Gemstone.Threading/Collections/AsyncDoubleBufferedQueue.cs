﻿//******************************************************************************************************
//  AsyncDoubleBufferedQueue.cs - Gbtc
//
//  Copyright © 2013, Grid Protection Alliance.  All Rights Reserved.
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
//  06/14/2013 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using Gemstone.EventHandlerExtensions;

namespace Gemstone.Threading.Collections
{
    /// <summary>
    /// Combines <see cref="AsyncQueue{T}"/> and <see cref="DoubleBufferedQueue{T}"/> to provide
    /// a low-contention, double-buffered queue suitable for multiple-producer, single-consumer
    /// scenarios.
    /// </summary>
    /// <typeparam name="T">Type of items being queued.</typeparam>
    public class AsyncDoubleBufferedQueue<T>
    {
        #region [ Members ]

        // Events

        /// <summary>
        /// Event that is raised if an exception is encountered while attempting to processing an item in the <see cref="AsyncDoubleBufferedQueue{T}"/>.
        /// </summary>
        /// <remarks>
        /// Processing will not stop for any exceptions thrown by user processing function, but exceptions will be exposed through this event.
        /// </remarks>
        public event EventHandler<EventArgs<Exception>>? ProcessException;

        // Fields
        private readonly AsyncQueue<IEnumerable<T>> m_asyncQueue;
        private readonly DoubleBufferedQueue<T> m_doubleBufferedQueue;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new instance of the <see cref="AsyncDoubleBufferedQueue{T}"/> class.
        /// </summary>
        public AsyncDoubleBufferedQueue()
        {
            m_asyncQueue = new AsyncQueue<IEnumerable<T>>();
            m_doubleBufferedQueue = new DoubleBufferedQueue<T>();

            m_asyncQueue.ProcessItemFunction = item => m_doubleBufferedQueue.Enqueue(item);
            m_asyncQueue.ProcessException += (sender, args) => ProcessException?.SafeInvoke(sender ?? this, args);
            m_doubleBufferedQueue.ProcessException += (sender, args) => ProcessException?.SafeInvoke(sender ?? this, args);
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets or sets item processing function.
        /// </summary>
        public Action<IList<T>>? ProcessItemsFunction
        {
            get => m_doubleBufferedQueue.ProcessItemsFunction;
            set => m_doubleBufferedQueue.ProcessItemsFunction = value;
        }

        /// <summary>
        /// Gets the number of items in the queue.
        /// </summary>
        public int Count => m_asyncQueue.Count + m_doubleBufferedQueue.Count;

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Enqueues a collection of items into the async double-buffered queue.
        /// </summary>
        /// <param name="items">The items to be queued.</param>
        public void Enqueue(IEnumerable<T> items) => m_asyncQueue.Enqueue(items);

        #endregion
    }
}
