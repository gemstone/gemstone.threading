﻿//******************************************************************************************************
//  ThreadContainerDedicated.cs - Gbtc
//
//  Copyright © 2014, Grid Protection Alliance.  All Rights Reserved.
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
//  3/8/2014 - Steven E. Chisholm
//       Generated original version of source code. 
//       
//******************************************************************************************************

using System;
using System.Threading;

namespace Gemstone.Threading;

internal class ThreadContainerDedicated
    : ThreadContainerBase
{
    private volatile bool m_shouldReRunImmediately;
    private volatile bool m_shouldReRunAfterDelay;
    private volatile int m_shouldReRunAfterDelayValue;

    private volatile bool m_shouldQuit;
    private Thread? m_thread;
    private ManualResetEvent? m_threadPausedWaitHandler;
    private ManualResetEvent? m_threadSleepWaitHandler;
    private volatile int m_sleepTime;
    private readonly bool m_isBackground;

    public ThreadContainerDedicated(Action<CallbackArgs> callback, Action disposeAndWaitCallback, bool isBackground, ThreadPriority priority, bool disposeOnShutdown)
        : base(callback, disposeAndWaitCallback, disposeOnShutdown)
    {
        m_isBackground = isBackground;

        if (disposeOnShutdown || !isBackground)
        {
            ShutdownHandler.TryRegisterCallback(Shutdown);
        }

        m_shouldReRunImmediately = false;
        m_shouldReRunAfterDelay = false;
        m_threadPausedWaitHandler = new ManualResetEvent(false);
        m_threadSleepWaitHandler = new ManualResetEvent(false);
        m_thread = new Thread(ThreadLoop) { IsBackground = isBackground, Priority = priority };
        m_thread.Start();
    }

    private void ThreadLoop()
    {
        while (true)
        {
            if (m_shouldQuit)
            {
                Quit();
                return;
            }

            if (m_shouldReRunImmediately)
            {
                m_shouldReRunImmediately = false;
                OnRunning();
            }
            else if (m_shouldReRunAfterDelay)
            {
                m_shouldReRunAfterDelay = false;

                if (m_shouldReRunAfterDelayValue != 0)
                {
                    //Set the foreground threads to background threads before sleeping.
                    if (!m_isBackground)
                        m_thread!.IsBackground = true;

                    m_threadSleepWaitHandler!.WaitOne(m_shouldReRunAfterDelayValue);

                    if (!m_isBackground)
                        m_thread!.IsBackground = false;
                }

                OnRunning();
            }
            else
            {
                //Set the foreground threads to background threads before sleeping.
                if (!m_isBackground)
                    m_thread!.IsBackground = true;

                m_threadPausedWaitHandler!.WaitOne(-1);

                if (m_sleepTime != 0)
                    m_threadSleepWaitHandler!.WaitOne(m_sleepTime);

                if (!m_isBackground)
                    m_thread!.IsBackground = false;

                OnRunning();
            }
        }
    }

    private void Quit()
    {
        m_threadPausedWaitHandler?.Dispose();
        m_threadPausedWaitHandler = null;
        m_threadSleepWaitHandler?.Dispose();
        m_threadSleepWaitHandler = null;
        m_thread = null;
    }

    protected override void InternalStart()
    {
        m_sleepTime = 0;
        m_threadSleepWaitHandler!.Set();
        m_threadPausedWaitHandler!.Set();
    }

    protected override void InternalStart_FromWorkerThread()
    {
        m_shouldReRunImmediately = true;
    }

    protected override void InternalStart(int delay)
    {
        m_sleepTime = delay;
        m_threadPausedWaitHandler!.Set();
    }

    protected override void InternalStart_FromWorkerThread(int delay)
    {
        m_shouldReRunAfterDelay = true;
        m_shouldReRunAfterDelayValue = delay;
        m_threadSleepWaitHandler!.Reset();
    }

    protected override void InternalCancelTimer()
    {
        m_sleepTime = 0;
        m_threadSleepWaitHandler!.Set();
        m_threadPausedWaitHandler!.Set();
    }

    protected override void InternalDoNothing_FromWorkerThread()
    {
        m_sleepTime = 0;
        m_threadPausedWaitHandler!.Reset();
        m_threadSleepWaitHandler!.Reset();
    }

    protected override void InternalDispose_FromWorkerThread()
    {
        m_shouldQuit = true;
    }



}
