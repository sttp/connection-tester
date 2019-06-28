//******************************************************************************************************
//  DataSubscriber.cs - Gbtc
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
//  06/23/2019 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using sttp;
using System;
using System.Collections.Generic;
using System.Linq;

public class DataSubscriber : SubscriberInstance
{
    private readonly GraphLines m_parent;
    private ulong m_processCount;

    private static readonly object s_consoleLock = new object();

    public DataSubscriber(GraphLines parent) => m_parent = parent;

    protected override void StatusMessage(string message)
    {
        m_parent.UpdateStatus(message);
    }

    protected override void ErrorMessage(string message)
    {
        StatusMessage($"ERROR: {message}");
    }

    protected override void DataStartTime(DateTime startTime)
    {
        StatusMessage($"Received first measurement at timestamp {startTime:yyyy-MM-dd HH:mm:ss.fff}");

        // At the moment we first receive data we know that we've successfully subscribed,
        // so we go ahead an cache list of measurement signal IDs (we may not know what
        // these are in advance if we used a FILTER expression to subscribe to points)
        GetParsedMeasurementMetadata(m_parent.m_measurementMetadata);        
    }

    protected override void ReceivedMetadata(ByteBuffer payload)
    {
        StatusMessage($"Received {payload.Count} bytes of metadata, parsing...");
        base.ReceivedMetadata(payload);
    }

    protected override void ParsedMetadata()
    {
        StatusMessage("Metadata successfully parsed.");
    }

    // Since new measurements will continue to arrive and be queued even when screen is not visible, it
    // is important that unity application be set to "run in background" to avoid running out of memory
    public override unsafe void ReceivedNewMeasurements(Measurement* measurements, int length)
    {
        List<Measurement> queue = new List<Measurement>(length);

        int count = m_parent.m_subscribedMeasurementIDs.Count;

        for (int i = 0; i < length; i++)
        {
            Measurement measurement = measurements[i];
            m_parent.m_subscribedMeasurementIDs.Add(measurement.GetSignalID());
            queue.Add(measurements[i]);
        }

        if (count < m_parent.m_subscribedMeasurementIDs.Count)
            m_parent.InitializeSubscription(m_parent.m_subscribedMeasurementIDs.ToArray());

        m_parent.m_dataQueue.Enqueue(queue);
    }

    protected override void ConfigurationChanged()
    {
        StatusMessage("Configuration change detected. Metadata refresh requested.");
    }

    protected override void HistoricalReadComplete()
    {
        StatusMessage("Historical data read complete. Restarting real-time subscription...");

        // After processing of a historical query has completed, return to the real-time subscription
        m_parent.InitiateSubscription();
    }

    protected override void ConnectionEstablished()
    {
        StatusMessage("Connection established.");
    }

    protected override void ConnectionTerminated()
    {
        StatusMessage("Connection terminated.");

        if (IsSubscribed())
            m_parent.ClearSubscription();
    }
}