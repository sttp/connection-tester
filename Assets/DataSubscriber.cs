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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable once CheckNamespace
public class DataSubscriber : SubscriberInstance
{
    private readonly GraphLines m_parent;
    private readonly ConcurrentDictionary<Guid, string> m_signalTypeAcronyms = new ConcurrentDictionary<Guid, string>();

    public DataSubscriber(GraphLines parent) => m_parent = parent;

    public bool TryGetSignalTypeAcronym(Guid signalID, out string signalTypeAcronym) => m_signalTypeAcronyms.TryGetValue(signalID, out signalTypeAcronym);

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

    public override void SubscriptionUpdated(SignalIndexCache signalIndexCache)
    {
        MeasurementMetadataMap measurementMetadata = new MeasurementMetadataMap();
        DeviceMetadataMap deviceMetadata = new DeviceMetadataMap();

        GetParsedMeasurementMetadata(measurementMetadata);
        GetParsedDeviceMetadata(deviceMetadata);

        HashSet<Guid> signalIDs = new HashSet<Guid>(signalIndexCache.GetSignalIDs());
        Dictionary<string, Dictionary<int, PhasorReference>> devicePhasors = new Dictionary<string, Dictionary<int, PhasorReference>>();

        foreach (MeasurementMetadata measurement in measurementMetadata.Values)
        {
            if (signalIDs.Contains(measurement.SignalID) && deviceMetadata.TryGetValue(measurement.DeviceAcronym, out DeviceMetadata device))
            {
                if (!devicePhasors.TryGetValue(device.Acronym, out Dictionary<int, PhasorReference> phasors))
                {
                    phasors = new Dictionary<int, PhasorReference>();

                    foreach (PhasorReference phasor in device.Phasors)
                        phasors[phasor.Phasor.SourceIndex] = phasor;

                    devicePhasors[device.Acronym] = phasors;
                }

                SignalKind signalKind = measurement.Reference.Kind;

                if (phasors.TryGetValue(measurement.PhasorSourceIndex, out PhasorReference phasorReference))
                    m_signalTypeAcronyms[measurement.SignalID] = Common.GetSignalTypeAcronym(signalKind, phasorReference.Phasor.Type[0]);
                else
                    m_signalTypeAcronyms[measurement.SignalID] = Common.GetSignalTypeAcronym(signalKind);
            }
        }

        m_parent.InitializeSubscription(signalIDs.ToArray());
    }

    // Since new measurements will continue to arrive and be queued even when screen is not visible, it
    // is important that unity application be set to "run in background" to avoid running out of memory
    public override unsafe void ReceivedNewMeasurements(Measurement* measurements, int length)
    {
        List<Measurement> queue = new List<Measurement>(length);

        for (int i = 0; i < length; i++)
            queue.Add(measurements[i]);

        m_parent.EnqueData(queue);
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
        m_parent.ClearSubscription();
    }
}