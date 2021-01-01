//******************************************************************************************************
//  UIThread.cs - Gbtc
//
//  Copyright © 2015, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  01/14/2013 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace UnityGSF
{
    // Generally you should only apply this class to a single game object (e.g., Main Camera),
    // multiple instances would simply compete to process queued method calls
    public class UIThread : MonoBehaviour
    {
        #region [ Methods ]

        protected void Start()
        {
            s_uiThread = Thread.CurrentThread;
        }

        // Execute any queued methods on UI thread...
        protected void FixedUpdate()
        {
            while (s_methodCalls.TryDequeue(out Tuple<Action<object[]>, object[], ManualResetEventSlim> methodCall))
            {
                Action<object[]> method = methodCall.Item1;
                object[] args = methodCall.Item2;
                ManualResetEventSlim resetEvent = methodCall.Item3;

                method(args);
                resetEvent.Set();
            }
        }

        #endregion

        #region [ Static ]

        // Static Fields

        // Capture UI thread so items can be executed on running thread as an optimization when possible
        private static Thread s_uiThread;

        // Set up a pre-signaled handle for calls that are executed on UI thread, i.e., not queued
        private static readonly WaitHandle s_signaledHandle;

        // Queue of methods and parameters
        private static readonly ConcurrentQueue<Tuple<Action<object[]>, object[], ManualResetEventSlim>> s_methodCalls;

        // Static Constructor
        static UIThread()
        {
            s_signaledHandle = new ManualResetEventSlim(true).WaitHandle;
            s_methodCalls = new ConcurrentQueue<Tuple<Action<object[]>, object[], ManualResetEventSlim>>();
        }

        // Static Methods

        /// <summary>
        /// Invokes the specified method on the main UI thread.
        /// </summary>
        /// <param name="method">Delegate of method to invoke on main thread.</param>
        /// <returns>WaitHandle that can be used to wait for queued method to execute.</returns>
        public static WaitHandle Invoke(Action<object[]> method)
        {
            if (s_uiThread == Thread.CurrentThread)
            {
                // Already running on UI thread, OK to execute immediately
                method(null);
                return s_signaledHandle;
            }

            ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

            s_methodCalls.Enqueue(new Tuple<Action<object[]>, object[], ManualResetEventSlim>(method, null, resetEvent));

            return resetEvent.WaitHandle;
        }

        /// <summary>
        /// Invokes the specified method on the main UI thread.
        /// </summary>
        /// <param name="method">Delegate of method to invoke on main thread.</param>
        /// <param name="args">Method parameters, if any.</param>
        /// <returns>WaitHandle that can be used to wait for queued method to execute.</returns>
        public static WaitHandle Invoke(Action<object[]> method, params object[] args)
        {
            if (s_uiThread == Thread.CurrentThread)
            {
                // Already running on UI thread, OK to execute immediately
                method(args);
                return s_signaledHandle;
            }

            ManualResetEventSlim resetEvent = new ManualResetEventSlim(false);

            s_methodCalls.Enqueue(new Tuple<Action<object[]>, object[], ManualResetEventSlim>(method, args, resetEvent));

            return resetEvent.WaitHandle;
        }

        #endregion
    }
}