using System;
using System.Text;
using sttp;

namespace ConnectionTester
{
    public class DataSubscriber : SubscriberInstance
    {
        private readonly string m_name;
        private ulong m_processCount;

        private static readonly object s_consoleLock = new object();

        public DataSubscriber(string name) => m_name = name;

        protected override SubscriptionInfo CreateSubscriptionInfo()
        {
            SubscriptionInfo info = base.CreateSubscriptionInfo();

            // TODO: Modify subscription info properties as desired...

            // To set up a remotely synchronized subscription, set this flag
            // to true and add the framesPerSecond parameter to the
            // ExtraConnectionStringParameters. Additionally, the following
            // example demonstrates the use of some other useful parameters
            // when setting up remotely synchronized subscriptions.

            //info.RemotelySynchronized = true;
            //info.ExtraConnectionStringParameters = "framesPerSecond=30;timeResolution=10000;downsamplingMethod=Closest";
            //info.LagTime = 3.0;
            //info.LeadTime = 1.0;
            //info.UseLocalClockAsRealTime = false;

            // Other example properties (see SubscriptionInfo class in DataSubscriber.h for all properties)
            //info.Throttled = false;
            //info.IncludeTime = true;
            //info.UseMillisecondResolution = true;

            return info;
        }

        protected override void SetupSubscriberConnector(SubscriberConnector connector)
        {
            base.SetupSubscriberConnector(connector);

            // TODO: Modify connector properties as desired...
            //connector.SetMaxRetries(-1);
        }

        protected override void StatusMessage(string message)
        {
            // TODO: Make sure these messages get logged to an appropriate location
            // For now, the base class just displays to console:

            // Calls can come from multiple threads, so we impose a simple lock before write to console
            lock (s_consoleLock)
                Console.WriteLine($"[{m_name}] {message}\n");
        }

        protected override void ErrorMessage(string message)
        {
            // TODO: Make sure these messages get logged to an appropriate location
            // For now, the base class just displays to console:

            // Calls can come from multiple threads, so we impose a simple lock before write to console
            lock (s_consoleLock)
                Console.Error.WriteLine($"[{m_name}] {message}\n");
        }

        protected override void DataStartTime(DateTime startTime)
        {
            // TODO: This reports timestamp of very first received measurement (if useful)
            //Console.WriteLine($"Received first measurement at timestamp {startTime:yyyy-MM-dd HH:mm:ss.fff}");
        }

        protected override void ReceivedMetadata(ByteBuffer payload)
        {
            Console.WriteLine($"Received {payload.Count} bytes of metadata, parsing...");
            base.ReceivedMetadata(payload);
        }

        protected override void ParsedMetadata()
        {
            StatusMessage("Metadata successfully parsed.");
        }

        public override unsafe void ReceivedNewMeasurements(Measurement* measurements, int length)
        {
            // TODO: The following code could be used to generate frame based output, e.g., for IEEE C37.118
            // Start processing measurements
            //for (int i = 0; i < length; i++)
            //{
            //    Measurement measurement = measurements[i];

            //    // Get adjusted value
            //    double value = measurement.Value;

            //    // Get timestamp
            //    DateTime timestamp = measurement.GetDateTime();

            //    // Get signal ID
            //    Guid signalID = measurement.GetSignalID();

            //    // Handle per measurement quality flags
            //    MeasurementStateFlags qualityFlags = measurement.Flags;

            //    MeasurementMetadata measurementMetadata;

            //    // Find associated configuration for measurement
            //    if (TryFindTargetConfigurationFrame(signalID, out ConfigurationFrame configurationFrame))
            //    {
            //        // Lookup measurement metadata - it's faster to find metadata from within configuration frame
            //        if (TryGetMeasurementMetdataFromConfigurationFrame(signalID, configurationFrame, out measurementMetadata))
            //        {
            //            SignalReference reference = measurementMetadata.Reference;

            //            // reference.Acronym	<< target device acronym 
            //            // reference.Kind		<< kind of signal (see SignalKind in "Types.h"), like Frequency, Angle, etc
            //            // reference.Index    << for Phasors, Analogs and Digitals - this is the ordered "index"

            //            // TODO: Handle measurement processing here...
            //        }
            //    }
            //    else if (TryGetMeasurementMetdata(signalID, out measurementMetadata))
            //    {
            //        // Received measurement is not part of a defined configuration frame, e.g., a statistic
            //        SignalReference reference = measurementMetadata.Reference;
            //    }
            //}

            // TODO: *** Temporary Testing Code Below *** -- REMOVE BEFORE USE
            const ulong interval = 10 * 60;
            ulong measurementCount = (ulong)length;
            bool showMessage = m_processCount + measurementCount >= (m_processCount / interval + 1) * interval;

            m_processCount += measurementCount;

            // Only display messages every few seconds
            if (showMessage)
            {
                StringBuilder message = new StringBuilder();

                message.AppendLine($"{GetTotalMeasurementsReceived()} measurements received so far...");
                message.AppendLine(measurements[0].GetDateTime().ToString("yyyy-MM-dd HH:mm:ss.fff"));
                message.AppendLine($"Signal ID: {measurements[0].GetSignalID()}");
                message.AppendLine("\tPoint\t\t\t\t\tValue");

                for (int i = 0; i < length; i++)
                {
                    Measurement measurement = measurements[i];
                    message.AppendLine($"\t{measurement.GetSignalID()}\t{measurement.Value}");
                }

                StatusMessage(message.ToString());
            }
        }

        protected override void ConfigurationChanged()
        {
            StatusMessage("Configuration change detected. Metadata refresh requested.");
        }

        protected override void HistoricalReadComplete()
        {
            StatusMessage("Historical data read complete.");
        }

        protected override void ConnectionEstablished()
        {
            StatusMessage("Connection established.");
        }

        protected override void ConnectionTerminated()
        {
            StatusMessage("Connection terminated.");
        }
    {
    }
}
