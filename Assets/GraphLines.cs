//******************************************************************************************************
//  GraphLines.cs - Gbtc
//
//  Copyright © 2019, Grid Protection Alliance.  All Rights Reserved.
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
//  11/29/2012 - J. Ritchie Carroll
//       Generated original version of source code.
//  06/23/2019 - J. Ritchie Carroll
//       Updated code to work with STTP API
//
//******************************************************************************************************

using sttp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using TMPro;
using UnityEngine;
using UnityGSF;
using Vectrosity;
using Timer = System.Timers.Timer;

// ReSharper disable CheckNamespace
namespace ConnectionTester
{
    public partial class GraphLines : MonoBehaviour
    {
    #region [ Static ]

        private static Action s_editorExitingPlayMode;

        static GraphLines()
        {
        #if !UNITY_EDITOR
            // Setup path at run-time to load proper version of native sttp.net.lib (.dll or .so)
            string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
            string pluginPath = Path.Combine(Path.GetFullPath("."), $"{Common.GetTargetName()}_Data", "Plugins");

        #if UNITY_STANDALONE_WIN
            if (IntPtr.Size == 8)
                pluginPath = Path.Combine(pluginPath, "x86_64");
            else
                pluginPath = Path.Combine(pluginPath, "x86");
        #endif

            if (!currentPath?.Contains(pluginPath) ?? false)
                Environment.SetEnvironmentVariable("PATH", $"{currentPath}{Path.PathSeparator}{pluginPath}", EnvironmentVariableTarget.Process);
        #endif
        }

        public static void EditorExitingPlayMode() => s_editorExitingPlayMode?.Invoke();

    #endregion

    #region [ Members ]

        // Constants
        public const string DefaultConnectionString = "server=localhost:7165;";
        public const string DefaultFilterExpression = "FILTER TOP 10 ActiveMeasurements WHERE SignalType='FREQ' OR SignalType LIKE '%PHM'";
        public const string DefaultStartTime = "*-5M";
        public const string DefaultStopTime = "*";
        public const int DefaultMaxSignals = 30;
        public const int DefaultProcesssInterval = 100;
        public const bool DefaultAutoInitiateConnection = false;
        public const int DefaultStatusRows = 10;
        public const string DefaultTitle = "STTP Connection Tester";
        public const int DefaultLineWidth = 4;
        public const float DefaultLineDepthOffset = 0.75F;
        public const int DefaultPointsInLine = 50;
        public const bool DefaultPointsScrollRight = true;
        public const bool DefaultUseSplineGraph = false;
        public const int DefaultSplineSegmentFactor = 3;
        public const bool DefaultGraphPoints = false;
        public const float DefaultGraphScale = 5.0F;
        public const double DefaultStatusDisplayInterval = 10000.0D;
        public const string DefaultLegendFormat = "{0:SignalTypeAcronym}: {0:Description} [{0:PointTag}]";
        public static readonly Color[] DefaultLineColors = { Color.blue, Color.yellow, Color.red, Color.white, Color.cyan, Color.magenta, Color.black, Color.gray };

        // Fields
        private readonly DataSubscriber m_subscriber;
        private readonly string m_version;
        private readonly string m_buildDate;
        private ConcurrentDictionary<string, Scale> m_scales;
        private ConcurrentDictionary<Guid, DataLine> m_dataLines;
        private ConcurrentQueue<IList<Measurement>> m_dataQueue;
        private string[] m_statusText;
        private Timer m_hideStatusTimer;
        private MouseOrbit m_mouseOrbitScript;
        private WaitHandle m_linesInitializedWaitHandle;

        // Subscription control window variables (managed via GraphLinesGUI.cs)
        private string m_connectionString = DefaultConnectionString;
        private string m_filterExpression = DefaultFilterExpression;
        private string m_startTime = DefaultStartTime;
        private string m_stopTime = DefaultStopTime;
        private int m_maxSignals = DefaultMaxSignals;
        private bool m_autoInitiateConnection = DefaultAutoInitiateConnection;
        private int m_processInterval = DefaultProcesssInterval;
        private int m_statusRows = DefaultStatusRows;

        // Run-time operation variables
        private int m_lastProcessInterval;
        private bool m_historicalSubscription;
        private long m_lastKeyCheck;
        private bool m_shiftIsDown;
        private bool m_connected;
        private bool m_connecting;
        private bool m_subscribed;
        private bool m_shuttingDown;

        // Public fields exposed to Unity UI interface

        // The following fields are assigned in Unity editor and are used to associate scene objects with GraphLines script,
        // note that the GraphLines script instance is currently associated with the "Main Camera" scene object
        public Transform Target;
        public TextMeshPro LegendMesh;
        public TextMeshPro DisplayValueMesh;
        public Guid TargetMeasurementID;
        public TextMesh StatusMesh;
        public Texture LineMaterial;
        public GUISkin UISkin;
        public Texture2D LinkCursor;

        // The following fields are used to tweak run-time behavior while debugging from within Unity editor
        public string Title = DefaultTitle;
        public int LineWidth = DefaultLineWidth;
        public float LineDepthOffset = DefaultLineDepthOffset;
        public int PointsInLine = DefaultPointsInLine;
        public bool PointsScrollRight = DefaultPointsScrollRight;
        public bool UseSplineGraph = DefaultUseSplineGraph;
        public int SplineSegmentFactor = DefaultSplineSegmentFactor;
        public bool GraphPoints = DefaultGraphPoints;
        public float GraphScale = DefaultGraphScale;
        public double StatusDisplayInterval = DefaultStatusDisplayInterval;
        public string LegendFormat = DefaultLegendFormat;
        public Color[] LineColors = DefaultLineColors;

    #endregion

    #region [ Constructors ]

        public GraphLines()
        {
            // Create a new data subscriber
            m_subscriber = new DataSubscriber(this);

            // Setup trigger to detect and handle exiting play mode
            s_editorExitingPlayMode = EndApplication;

            // Read version info from .NET assembly
            Assembly assembly = typeof(GraphLines).Assembly;
            AssemblyName assemblyInfo = assembly.GetName();
            DateTime buildDate = File.GetLastWriteTime(assembly.Location);

            m_version = $"{assemblyInfo.Version.Major}.{assemblyInfo.Version.Minor}.{assemblyInfo.Version.Build}";
            m_buildDate = $"{buildDate:yyyy-MM-dd HH:mm:ss}";

            // Update subscriber info to include information that source is STTP connection tester
            m_subscriber.GetAssemblyInfo(out string source, out string version, out string updatedOn);
            source = $"{DefaultTitle} version {m_version} updated on {m_buildDate}, using STTP library {source}";
            m_subscriber.SetAssemblyInfo(source, version, updatedOn);
        }

    #endregion

    #region [ Methods ]

        // Unity Event Handlers

        protected void Awake()
        {
            // Attempt to reference active mouse orbit script
            m_mouseOrbitScript = GetComponent<MouseOrbit>();

            // Load previous settings from INI file
            LoadSettings();

            // Create line dictionary and data queue
            m_scales = new ConcurrentDictionary<string, Scale>();
            m_dataLines = new ConcurrentDictionary<Guid, DataLine>();
            m_dataQueue = new ConcurrentQueue<IList<Measurement>>();

            // Initialize status rows and timer to hide status after a period of no updates
            m_statusText = new string[m_statusRows];

            for (int i = 0; i < m_statusRows; i++)
                m_statusText[i] = "";

            m_hideStatusTimer = new Timer
            {
                AutoReset = false,
                Interval = StatusDisplayInterval
            };

            m_hideStatusTimer.Elapsed += HideStatusTimer_Elapsed;

            VectorLine.SetCanvasCamera(Camera.main);
            VectorLine.canvas.hideFlags = HideFlags.HideInHierarchy;
        }

        protected void Start()
        {
            // Attempt to update title
            GameObject titleObject = GameObject.Find("Title");

            if (!(titleObject is null))
            {
                TextMesh titleMesh = titleObject.GetComponent<TextMesh>();

                if (!(titleMesh is null))
                    titleMesh.text = Title;
            }

            // If 3D text legend mesh property was not defined, attempt to look it up by name
            if (LegendMesh is null)
            {
                GameObject legendObject = GameObject.Find("Legend");

                if (!(legendObject is null))
                    LegendMesh = legendObject.GetComponent<TextMeshPro>();
            }

            // If 3D text status mesh property was not defined, attempt to look it up by name
            if (StatusMesh is null)
            {
                GameObject statusObject = GameObject.Find("Status");

                if (!(statusObject is null))
                    StatusMesh = statusObject.GetComponent<TextMesh>();
            }

            Physics.queriesHitTriggers = true;

            DisplayHelp();

            if (m_autoInitiateConnection)
                InitiateConnection();
        }

        private void DisplayHelp()
        {
            const string HelpText =
                "<b><color=lightblue>" +
                "Press '+' to increase font size, or '-' to decrease.\r\n" +
                "Press 'C' to connect, 'D' to disconnect.\r\n" +
                "Press 'R' to restore default graph location.\r\n" +
                "Press 'S' to toggle drawing splines or lines.\r\n" +
                "Press 'P' to toggle drawing points or lines.\r\n" +
                "Press 'M' to toggle status message display.\r\n" +
                "Press 'F1' for help page, or 'H' for this message." +
                "</color></b>";

            for (int i = 0; i < m_statusText.Length - 1; i++)
                m_statusText[i] = "";

            UpdateStatus(HelpText, 30000);
        }

        protected void Update()
        {
            // GUI drawn based on current window size
            CheckForScreenResize();

            // Nothing to update if we haven't subscribed yet
            if (m_subscribed)
            {
                // Make sure lines are initialized before trying to draw them
                if (!(m_linesInitializedWaitHandle is null))
                {
                    // Only wait one millisecond then try again at next update
                    if (m_linesInitializedWaitHandle.WaitOne(1))
                        m_linesInitializedWaitHandle = null;
                    else
                        return;
                }

                // Dequeue all new measurements and apply values to lines
                while (m_dataQueue.TryDequeue(out IList<Measurement> measurements))
                {
                    // ReSharper disable once ForCanBeConvertedToForeach
                    for (int i = 0; i < measurements.Count; i++)
                    {
                        Measurement measurement = measurements[i];
                        Guid measurementID = measurement.GetSignalID();

                        if (m_dataLines.TryGetValue(measurementID, out DataLine line))
                            line.UpdateValue((float)measurement.Value);

                        if (measurementID == TargetMeasurementID && !(DisplayValueMesh is null))
                            DisplayValueMesh.UpdateText($"Value: {measurement.Value:N3} @ {measurement.GetDateTime():yyyy-MM-dd HH:mm:ss.fff}");
                    }
                }

                // Update the scales to display the new measurements
                foreach (Scale scale in m_scales.Values)
                    scale.Update();
            }

            long currentTicks = DateTime.UtcNow.Ticks;

            // Check for hot key updates no faster than every 200 milliseconds
            if (currentTicks - m_lastKeyCheck > TimeSpan.TicksPerMillisecond * 200)
            {
                // Plus / Minus keys will increase / decrease GUI font size
                CheckForFontSizeHotKeys(currentTicks);

                // F1 key will launch help page
                if (Input.GetKey(KeyCode.F1))
                {
                    m_lastKeyCheck = currentTicks;
                    Process.Start("https://sttp.github.io/connection-tester/");
                }

                // Connect with "C" key
                if (Input.GetKey(KeyCode.C))
                {
                    m_lastKeyCheck = currentTicks;
                    InitiateConnection();
                }

                // Disconnect with "D" key
                if (Input.GetKey(KeyCode.D))
                {
                    m_lastKeyCheck = currentTicks;
                    TerminateConnection();
                }

                // Restore default graph location with "R" key
                if (Input.GetKey(KeyCode.R))
                {
                    m_lastKeyCheck = currentTicks;
                    m_mouseOrbitScript.Restore = true;
                }

                // Toggle drawing splines or lines with "S" key
                if (Input.GetKey(KeyCode.S))
                {
                    m_lastKeyCheck = currentTicks;
                    UseSplineGraph = !UseSplineGraph;
                    InitiateSubscription();
                }

                // Toggle drawing points or lines with "P" key
                if (Input.GetKey(KeyCode.P))
                {
                    m_lastKeyCheck = currentTicks;
                    GraphPoints = !GraphPoints;
                    InitiateSubscription();
                }

                // Toggle message display with "M" key
                if (Input.GetKey(KeyCode.M))
                {
                    m_lastKeyCheck = currentTicks;

                    if (string.IsNullOrWhiteSpace(StatusMesh.text))
                        UpdateStatus(null, Timeout.Infinite);
                    else
                        StatusMesh.UpdateText("");
                }

                // Show local help message with "H" key
                if (Input.GetKey(KeyCode.H))
                {
                    m_lastKeyCheck = currentTicks;
                    DisplayHelp();
                }
            }

            // Exit application with "ESC" key
            if (Input.GetKey(KeyCode.Escape))
                EndApplication();
        }

        internal void InitializeSubscription(Guid[] subscribedMeasurementIDs)
        {
            int count = subscribedMeasurementIDs.Length;

            if (count > m_maxSignals)
            {
                subscribedMeasurementIDs = subscribedMeasurementIDs.Take(m_maxSignals).ToArray();
                UpdateStatus($"WARNING: Reduced {count:N0} subscribed measurements to {m_maxSignals:N0}, current configured maximum.");
            }

            // Create a new line for each subscribed measurement, this should be done in
            // advance of updating the legend so the line colors will already be defined
            m_linesInitializedWaitHandle = UIThread.Invoke(CreateDataLines, subscribedMeasurementIDs);

            // Set initial replay interval as soon as subscription is ready
            if (m_historicalSubscription)
                m_subscriber.SetHistoricalReplayInterval(m_processInterval);
        }

        internal void EnqueData(IList<Measurement> measurements) =>
            m_dataQueue.Enqueue(measurements);

        internal void UpdateStatus(string statusText, int displayInterval = 0)
        {
            StringBuilder cumulativeStatusText = new StringBuilder();

            if (string.IsNullOrWhiteSpace(statusText))
            {
                cumulativeStatusText.Append(string.Join(Environment.NewLine, m_statusText));
            }
            else
            {
                // Keep a finite list of status updates - rolling older text up
                for (int i = 0; i < m_statusText.Length - 1; i++)
                {
                    m_statusText[i] = m_statusText[i + 1];
                    cumulativeStatusText.AppendLine(m_statusText[i]);
                }

                // Wrap long lines
                statusText = string.Join(Environment.NewLine, statusText.GetSegments(95));

                // Add color highlighting to warnnings and errors
                if (statusText.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase))
                    statusText = $"<color=yellow>{statusText}</color>";
                else if (statusText.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                    statusText = $"<color=red>{statusText}</color>";

                // Append newest status text
                m_statusText[m_statusText.Length - 1] = statusText;
                cumulativeStatusText.Append(statusText);
            }

            // Show text on 3D status text object
            StatusMesh.UpdateText(cumulativeStatusText.ToString());

            if (m_hideStatusTimer is null)
                return;

            m_hideStatusTimer.Stop();

            if (displayInterval == Timeout.Infinite)
                return;

            // Reset timer to hide status after a period of no updates
            m_hideStatusTimer.Interval = displayInterval > 0 ? displayInterval : StatusDisplayInterval;
            m_hideStatusTimer.Start();
        }

        // Hide status text after a period of no updates
        private void HideStatusTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!m_shuttingDown && !(StatusMesh is null))
                StatusMesh.UpdateText("");
        }

        // Create a new data line for each subscribed measurement
        private void CreateDataLines(object[] args)
        {
            m_subscribed = false;

            if (args is null || args.Length < 1)
                return;

            if (!(args[0] is Guid[] subscribedMeasurementIDs) || m_scales is null || m_dataLines is null)
                return;

            m_scales.Clear();
            m_dataLines.Clear();

            foreach (Guid measurementID in subscribedMeasurementIDs)
            {
                bool autoShrinkScale = false;

                if (m_subscriber.TryGetSignalTypeAcronym(measurementID, out string signalType))
                {
                    switch (signalType)
                    {
                        case "IPHM":
                        case "VPHM":
                        case "FREQ":
                        case "ALOG":
                        case "CALC":
                            autoShrinkScale = true;
                            break;
                    }
                }
                else
                {
                    signalType = "UNKNOWN";
                }

                DataLine line = new DataLine(this, measurementID, m_dataLines.Count);
                Scale scale = m_scales.GetOrAdd(signalType, _ => new Scale(GraphScale, autoShrinkScale));
                scale.Add(line);

                m_dataLines.TryAdd(measurementID, line);
            }

            m_subscribed = true;

            // Update legend - we do this on a different thread since we've already
            // waited around for initial set of lines to be created on a UI thread,
            // no need to keep UI thread operations pending
            ThreadPool.QueueUserWorkItem(UpdateLegend, subscribedMeasurementIDs);
        }

        private void UpdateLegend(object state)
        {
            if (!(state is Guid[] subscribedMeasurementIDs))
                return;

            StringBuilder legendText = new StringBuilder();

            // Go through each subscribed measurement ID and look up its associated metadata
            foreach (Guid measurementID in subscribedMeasurementIDs)
            {
                // Lookup metadata record where SignalID column is our measurement ID
                if (!m_subscriber.TryGetMeasurementMetdata(measurementID, out MeasurementMetadata metadata))
                    continue;

                m_subscriber.TryGetSignalTypeAcronym(measurementID, out string signalType);

                if (string.IsNullOrWhiteSpace(signalType))
                    signalType = "UNST"; // Unknown signal type

                if (m_dataLines.TryGetValue(measurementID, out DataLine dataLine))
                {
                    // Surround legend line with link tagged with measurement ID so hover can display value
                    legendText.Append($"<link=\"{measurementID}\">");

                    // Add a "dash" with matching graph data line color for reference // <line-height=99%>
                    legendText.Append($"<color=#{ColorUtility.ToHtmlStringRGB(dataLine.VectorColor)}><b><size=+7><voffset=-0.07em>—</voffset></size></b></color><space=0.2em>");

                    // Add formatted metadata label expression to legend text
                    legendText.AppendFormat(LegendFormat, new MetadataFormatProvider(metadata, signalType));

                    // Close link and start new line
                    legendText.AppendLine("</link>");
                }
            }

            // Update text for 3D text labels object with subscribed point tag names
            LegendMesh.UpdateText(legendText.ToString());
        }

        // Connects or reconnects to a data publisher
        private void InitiateConnection(bool historical = false)
        {
            m_connecting = true;

            // Shutdown any existing connection
            TerminateConnection();

            // Attempt to extract server name from connection string
            Dictionary<string, string> settings = m_connectionString?.ParseKeyValuePairs();

            if (settings is null || !settings.TryGetValue("server", out string server) || string.IsNullOrEmpty(server))
            {
                UpdateStatus("ERROR: Cannot connect - no \"server\" parameter defined in connection string. For example: \"server=localhost:7165\"");
                return;
            }

            string[] parts = server.Split(':');
            string hostname;
            ushort port, udpPort = 0;

            if (parts.Length == 0)
            {
                hostname = server;
                port = 7165;
            }
            else
            {
                hostname = parts[0].Trim();

                if (!ushort.TryParse(parts[1].Trim(), out port))
                    port = 7165;
            }

            // Attempt to extract possible data channel setting from connection string.
            // For example, adding "; dataChannel=9191" to the connection string
            // would request that the data publisher send data to the subscriber over
            // UDP on port 9191. Technically this is part of the subscription info but
            // we allow this definition in the connection string for this application.
            if (settings.TryGetValue("dataChannel", out string dataChannel) && !string.IsNullOrWhiteSpace(dataChannel))
                ushort.TryParse(dataChannel, out udpPort);

            UpdateStatus($"Attempting connection to \"{hostname}:{port}\"{(udpPort > 0 ? $" with UDP data channel on port \"{udpPort}\"" : "")}...");

            // Check if user has requested to not use payload compression in the connection string
            m_subscriber.PayloadDataCompressed = !settings.TryGetValue("compression", out string setting) || !bool.TryParse(setting, out bool value) || value;

            if (udpPort > 0 && settings.ContainsKey("compression") && m_subscriber.PayloadDataCompressed)
                UpdateStatus("WARNING: Requested compression with UDP payload ignored.");

            InitiateSubscription(historical);

            // Initialize subscriber
            m_subscriber.Initialize(hostname, port, udpPort);
            m_subscriber.ConnectAsync();
        }

        // Subscribes or resubscribes to real-time or historical stream using current filter expression
        internal void InitiateSubscription(bool historical = false)
        {
            if (historical && !(m_connecting || m_connected))
            {
                InitiateConnection(true);
                return;
            }

            if (m_subscribed)
            {
                if (historical && !m_historicalSubscription)
                {
                    InitiateConnection(true);
                    return;
                }

                ClearSubscription();
            }

            m_historicalSubscription = historical;
            m_controlWindowMinimized = !historical;

            if (historical)
            {
                // Set historical replay parameters for a temporal connection
                m_subscriber.EstablishHistoricalRead(m_startTime, m_stopTime);
                UpdateStatus($"Starting historical replay at {(m_processInterval == 0 ? "fast as possible" : $"{m_processInterval}ms")} playback speed...");
            }
            else
            {
                // Clear historical replay parameters for a real-time connection
                m_subscriber.EstablishHistoricalRead(string.Empty, string.Empty);
                UpdateStatus("Starting real-time subscription...");
            }

            // Updating filter expression invokes a resubscribe, if already subscribed
            m_subscriber.FilterExpression = m_filterExpression;
        }

        internal void ConnectionEstablished()
        {
            m_connected = true;
            m_connecting = false;
            m_controlWindowMinimized = !m_historicalSubscription;
        }

        internal void ConnectionTerminated()
        {
            m_connected = false;
            m_connecting = false;
            ClearSubscription();
        }

        // Clears an existing subscription
        internal void ClearSubscription()
        {
            void eraseLine(object[] args) =>
                ((ILine)args[0]).Stop();

            m_subscribed = false;

            // Reset subscription state
            m_linesInitializedWaitHandle = null;

            // Clear out existing scales
            m_scales?.Clear();

            // Erase data lines
            if (m_dataLines?.Count > 0)
            {
                foreach (DataLine dataLine in m_dataLines.Values)
                    UIThread.Invoke(eraseLine, dataLine);

                m_dataLines.Clear();
            }

            // Clear legend text
            if (!m_shuttingDown && !(LegendMesh is null))
                LegendMesh.UpdateText("");
        }

        // Terminates an existing connection to a data publisher
        private void TerminateConnection()
        {
            ClearSubscription();

            if (m_subscriber is null)
                return;

            if (m_connected || m_connecting || m_subscriber.Connected)
                UpdateStatus("Terminating current connection...");

            m_subscriber.Disconnect();
        }

        private void EndApplication()
        {
            m_shuttingDown = true;

            TerminateConnection();

            if (!(m_hideStatusTimer is null))
            {
                m_hideStatusTimer.Elapsed -= HideStatusTimer_Elapsed;
                m_hideStatusTimer.Dispose();
            }

            m_hideStatusTimer = null;

        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
        }

        protected void OnApplicationQuit()
        {
            // Save any user updated settings
            SaveSettings();

            // Dispose of data subscriber
            m_subscriber?.Dispose();
        }

    #endregion
    }
}