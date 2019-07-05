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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityGSF;
using Vectrosity;
using Debug = UnityEngine.Debug;

// ReSharper disable UnusedMember.Local
// ReSharper disable IntroduceOptionalParameters.Local
// ReSharper disable RedundantCast.0
// ReSharper disable once CheckNamespace
namespace ConnectionTester
{
    public class GraphLines : MonoBehaviour
    {
        #region [ Static ]

        private static Action s_editorExitingPlayMode;

        static GraphLines()
        {
        #if !UNITY_EDITOR
            // Setup path at run-time to load proper version of native sttp.net.lib.dll
            string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
            string dataPath = Application.dataPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            string dllPath = Path.Combine(dataPath, "Plugins");

            if (!currentPath?.Contains(dllPath) ?? false)
                Environment.SetEnvironmentVariable("PATH", $"{currentPath}{Path.PathSeparator}{dllPath}", EnvironmentVariableTarget.Process);
        #endif
        }

        public static void EditorExitingPlayMode() => s_editorExitingPlayMode?.Invoke();

        #endregion

        #region [ Nested Types ]

        // Defines a common set of methods for a line
        private interface ILine
        {
            Guid ID { get; }

            void Stop();
        }

        private class Scale
        {
            private const float ShrinkStartThreshold = 0.5F;
            private const float ShrinkStopThreshold = 0.9F;
            private const float ShrinkDelay = 1.0F;

            private readonly List<DataLine> m_lines;

            private float m_scaleMin = float.NaN;
            private float m_scaleMax = float.NaN;
            private readonly float m_graphScale;
            private readonly bool m_autoShrinkScale;
            private float m_timeUntilShrink;

            public Scale(float graphScale, bool autoShrinkScale)
            {
                m_graphScale = graphScale;
                m_autoShrinkScale = autoShrinkScale;
                m_lines = new List<DataLine>();
                m_timeUntilShrink = ShrinkDelay;
            }

            public void Add(DataLine line) => m_lines.Add(line);

            public void Update()
            {
                float displayMin = float.NaN;
                float displayMax = float.NaN;

                foreach (DataLine line in m_lines)
                {
                    foreach (float value in line.UnscaledData)
                    {
                        if (float.IsNaN(displayMin) || value < displayMin)
                            displayMin = value;

                        if (float.IsNaN(displayMax) || value > displayMax)
                            displayMax = value;
                    }
                }

                if (float.IsNaN(m_scaleMin) || displayMin < m_scaleMin)
                {
                    m_scaleMin = displayMin;
                    m_timeUntilShrink = ShrinkDelay;
                }

                if (float.IsNaN(m_scaleMax) || displayMax > m_scaleMax)
                {
                    m_scaleMax = displayMax;
                    m_timeUntilShrink = ShrinkDelay;
                }

                if (m_autoShrinkScale)
                {
                    if (m_timeUntilShrink > 0.0F)
                    {
                        if ((m_scaleMax - m_scaleMin) * ShrinkStartThreshold >= (displayMax - displayMin))
                            m_timeUntilShrink -= Time.deltaTime;
                        else
                            m_timeUntilShrink = ShrinkDelay;
                    }

                    if (m_timeUntilShrink <= 0.0F)
                    {
                        m_scaleMin += (displayMin - m_scaleMin) * Time.deltaTime * 5.0F;
                        m_scaleMax -= (m_scaleMax - displayMax) * Time.deltaTime * 5.0F;

                        if ((m_scaleMax - m_scaleMin) * ShrinkStopThreshold <= (displayMax - displayMin))
                            m_timeUntilShrink = ShrinkDelay;
                    }
                }

                ScaleLinePoints();
            }

            private void ScaleLinePoints()
            {
                float unscaledValue;
                Vector3 point;

                foreach (DataLine line in m_lines)
                {
                    for (int x = 0; x < line.UnscaledData.Length; x++)
                    {
                        unscaledValue = line.UnscaledData[x];

                        if (float.IsNaN(unscaledValue))
                            unscaledValue = MidPoint;

                        point = line.LinePoints[x];
                        point.z = -ScaleValue(unscaledValue);
                        line.LinePoints[x] = point;
                    }
                }
            }

            private float ScaleValue(float value) => (value - m_scaleMin) * (m_graphScale * 2.0F) / Range - m_graphScale;

            private float Range => m_scaleMax - m_scaleMin;

            private float MidPoint => m_scaleMin + Range / 2.0F;
        }

        // Creates a dynamically scaled 3D line using Vectrosity asset to draw line for data
        private class DataLine : ILine
        {
            private VectorLine m_vector;

            public DataLine(GraphLines parent, Guid id, int index)
            {
                ID = id;
                Index = index;

                UnscaledData = new float[parent.m_pointsInLine];

                m_vector = new VectorLine("DataLine" + index, new List<Vector3>(parent.m_pointsInLine), parent.m_lineMaterial, parent.m_lineWidth, LineType.Continuous);
                m_vector.color = parent.m_lineColors[index % parent.m_lineColors.Length];
                m_vector.drawTransform = parent.m_target;
                m_vector.Draw3DAuto();

                for (int x = 0; x < m_vector.points3.Count; x++)
                {
                    UnscaledData[x] = float.NaN;
                    m_vector.points3[x] = new Vector3(Mathf.Lerp(-5.0F, 5.0F, x / (float)m_vector.points3.Count), -((index + 1) * parent.m_lineDepthOffset + 0.05F), 0.0F);
                }
            }

            public Guid ID { get; }

            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            // ReSharper disable once MemberCanBePrivate.Local
            public int Index { get; }

            public Color VectorColor
            {
                get => m_vector.color;
                set => m_vector.color = value;
            }

            public float[] UnscaledData { get; }

            public List<Vector3> LinePoints => m_vector.points3;

            public void Stop()
            {
                if ((object)m_vector != null)
                {
                    m_vector.StopDrawing3DAuto();
                    VectorLine.Destroy(ref m_vector);
                }
            }

            public void UpdateValue(float newValue)
            {
                int x;

                // Move y position of all points to the left by one
                for (x = 0; x < m_vector.points3.Count - 1; x++)
                    UnscaledData[x] = UnscaledData[x + 1];

                UnscaledData[x] = newValue;
            }
        }

        // Creates a fixed 3D line using Vectrosity asset to draw line for legend
        private class LegendLine : ILine
        {
            private VectorLine m_vector;

            public LegendLine(GraphLines parent, Guid id, int index, Color color)
            {
                Transform transform = parent.m_legendMesh.transform;
                Vector3 position = transform.position;

                ID = id;
                m_vector = new VectorLine("LegendLine" + index, new List<Vector3>(2), parent.m_lineMaterial, parent.m_lineWidth, LineType.Discrete);
                m_vector.color = color;
                m_vector.drawTransform = transform;
                m_vector.Draw3DAuto();

                float spacing = parent.m_legendMesh.characterSize * 1.5F;

                // Position legend line relative to text descriptions
                Vector3 point1 = new Vector3(-2.0F, -(spacing / 2.0F + index * spacing), -position.z);
                Vector3 point2 = new Vector3(-0.5F, point1.y, point1.z);

                m_vector.points3[0] = point1;
                m_vector.points3[1] = point2;
            }

            public Guid ID { get; }

            public void Stop()
            {
                if ((object)m_vector != null)
                {
                    m_vector.StopDrawing3DAuto();
                    VectorLine.Destroy(ref m_vector);
                }
            }
        }

        // Exposes Metadata record in a string.Format expression
        private class MetadataFormatProvider : IFormattable
        {
            private readonly MeasurementMetadata m_metadata;
            private readonly string m_signalTypeAcronym;

            public MetadataFormatProvider(MeasurementMetadata metadata, string signalTypeAcronym)
            {
                m_metadata = metadata;
                m_signalTypeAcronym = signalTypeAcronym;
            }

            public string ToString(string propertyName, IFormatProvider provider)
            {
                if (propertyName.Equals("SignalTypeAcronym", StringComparison.OrdinalIgnoreCase))
                    return m_signalTypeAcronym;

                return typeof(MeasurementMetadata).GetProperty(propertyName)?.GetValue(m_metadata).ToString() ?? "<" + propertyName + ">";
            }
        }

        #endregion

        #region [ Members ]

        // Constants
        private const string IniFileName = "settings.ini";
        private const int ControlWindowActiveHeight = 130;
        private const int ControlWindowMinimizedHeight = 20;
        private const int MinGuiSize = 1;
        private const int MaxGuiSize = 3;

        // Fields
        private readonly DataSubscriber m_subscriber;
        private readonly string m_version;
        private readonly string m_buildDate;
        private ConcurrentDictionary<string, Scale> m_scales;
        private ConcurrentDictionary<Guid, DataLine> m_dataLines;
        private ConcurrentQueue<IList<Measurement>> m_dataQueue;
        private List<LegendLine> m_legendLines;
        private string[] m_statusText;
        private System.Timers.Timer m_hideStatusTimer;
        private MouseOrbit m_mouseOrbitScript;
        private WaitHandle m_linesInitializedWaitHandle;

        // Subscriber control window variables
        private Texture2D m_controlWindowTexture;
        private Rect m_controlWindowActiveLocation;
        private Rect m_controlWindowMinimizedLocation;
        private bool m_controlWindowMinimized;
        private int m_lastScreenHeight = -1;
        private int m_lastScreenWidth = -1;
        private string m_startTime = "*-5M";
        private string m_stopTime = "*";
        private int m_maxSignals = 30;
        private bool m_autoInitiateConnection;
        private int m_processInterval = 33;
        private int m_lastProcessInterval;
        private bool m_historicalSubscription;
        private Vector2 m_scrollPosition;
        private int m_guiSize = 1;
        private long m_lastKeyCheck;
        private bool m_connected;
        private bool m_subscribed;
        private bool m_shuttingDown;

        // Public fields exposed to Unity UI interface
        public string m_title = "STTP Connection Tester";
        public string m_connectionString = "server=localhost:7165;";
        public string m_filterExpression = "FILTER TOP 10 ActiveMeasurements WHERE SignalType='FREQ' OR SignalType LIKE 'VPH*'";
        public Texture m_lineMaterial;
        public int m_lineWidth = 4;
        public float m_lineDepthOffset = 0.75F;
        public int m_pointsInLine = 50;
        public Transform m_target;
        public float m_graphScale = 5.0F;
        public Color[] m_lineColors = { Color.blue, Color.yellow, Color.red, Color.white, Color.cyan, Color.magenta, Color.black, Color.gray };
        public TextMesh m_legendMesh;
        public TextMesh m_statusMesh;
        public int m_statusRows = 4;
        public double m_statusDisplayInterval = 10000.0D;
        public string m_legendFormat = "{0:SignalTypeAcronym}: {0:Description} [{0:PointTag}]";

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
        }

        #endregion

        #region [ Methods ]

        // Unity Event Handlers

        protected void Awake()
        {
            string defaultIniPath = Application.dataPath + "/" + IniFileName;
            string userIniPath = Application.persistentDataPath + "/" + IniFileName;

            // Copy INI file with default settings to user INI file if one doesn't exist
            if (File.Exists(defaultIniPath) && !File.Exists(userIniPath))
                File.Copy(defaultIniPath, userIniPath);

            // Load settings from INI file
            IniFile iniFile = new IniFile(userIniPath);

            m_title = iniFile["Settings", "Title", m_title];
            m_connectionString = iniFile["Settings", "ConnectionString", m_connectionString];
            m_filterExpression = iniFile["Settings", "FilterExpression", m_filterExpression];
            m_lineWidth = int.Parse(iniFile["Settings", "LineWidth", m_lineWidth.ToString()]);
            m_lineDepthOffset = float.Parse(iniFile["Settings", "LineDepthOffset", m_lineDepthOffset.ToString(CultureInfo.InvariantCulture)]);
            m_pointsInLine = int.Parse(iniFile["Settings", "PointsInLine", m_pointsInLine.ToString()]);
            m_legendFormat = iniFile["Settings", "LegendFormat", m_legendFormat];
            m_statusRows = int.Parse(iniFile["Settings", "StatusRows", m_statusRows.ToString()]);
            m_statusDisplayInterval = double.Parse(iniFile["Settings", "StatusDisplayInterval", m_statusDisplayInterval.ToString(CultureInfo.InvariantCulture)]);
            m_startTime = iniFile["Settings", "StartTime", m_startTime];
            m_stopTime = iniFile["Settings", "StopTime", m_stopTime];
            m_maxSignals = int.Parse(iniFile["Settings", "MaxSignals", m_maxSignals.ToString()]);
            m_autoInitiateConnection = bool.Parse(iniFile["Settings", "AutoInitiateConnection", m_autoInitiateConnection.ToString()]);
            m_guiSize = int.Parse(iniFile["Settings", "GuiSize", m_guiSize.ToString()]);
            m_controlWindowMinimized = m_autoInitiateConnection;

            // Validate deserialized GUI size
            if (m_guiSize < MinGuiSize)
                m_guiSize = MinGuiSize;

            if (m_guiSize > MaxGuiSize)
                m_guiSize = MaxGuiSize;

            // Attempt to save INI file updates (e.g., to restore any missing settings)
            try
            {
                iniFile.Save();
            }
            catch (Exception ex)
            {
                Debug.Log("ERROR: " + ex.Message);
            }

            // Attempt to reference active mouse orbit script
            m_mouseOrbitScript = GetComponent<MouseOrbit>();

            // Create line dictionary and data queue
            m_scales = new ConcurrentDictionary<string, Scale>();
            m_dataLines = new ConcurrentDictionary<Guid, DataLine>();
            m_dataQueue = new ConcurrentQueue<IList<Measurement>>();
            m_legendLines = new List<LegendLine>();

            // Initialize status rows and timer to hide status after a period of no updates
            m_statusText = new string[m_statusRows];

            for (int i = 0; i < m_statusRows; i++)
                m_statusText[i] = "";

            m_hideStatusTimer = new System.Timers.Timer();
            m_hideStatusTimer.AutoReset = false;
            m_hideStatusTimer.Interval = m_statusDisplayInterval;
            m_hideStatusTimer.Elapsed += m_hideStatusTimer_Elapsed;

            // For mobile applications we use a larger GUI font size.
            // Other deployments might benefit from this as well - larger
            // size modes may work also but are not tested
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                case RuntimePlatform.IPhonePlayer:
                    if (Screen.height <= 720)
                        m_guiSize = 2;  // 720P
                    else
                        m_guiSize = 3;  // 1080P or higher
                    break;
            }

            // Create a solid background for the control window
            m_controlWindowTexture = new Texture2D(1, 1);
            m_controlWindowTexture.SetPixel(0, 0, new Color32(10, 25, 70, 255));
            m_controlWindowTexture.Apply();

            VectorLine.SetCanvasCamera(Camera.main);
            VectorLine.canvas.hideFlags = HideFlags.HideInHierarchy;
        }

        protected void Start()
        {
            // Attempt to update title
            GameObject titleObject = GameObject.Find("Title");

            if ((object)titleObject != null)
            {
                TextMesh titleMesh = titleObject.GetComponent<TextMesh>();

                if ((object)titleMesh != null)
                    titleMesh.text = m_title;
            }

            // If 3D text legend mesh property was not defined, attempt to look it up by name
            if ((object)m_legendMesh == null)
            {
                GameObject legendObject = GameObject.Find("Legend");

                if ((object)legendObject != null)
                    m_legendMesh = legendObject.GetComponent<TextMesh>();
            }

            // If 3D text status mesh property was not defined, attempt to look it up by name
            if ((object)m_statusMesh == null)
            {
                GameObject statusObject = GameObject.Find("Status");

                if ((object)statusObject != null)
                    m_statusMesh = statusObject.GetComponent<TextMesh>();
            }

            UpdateStatus("Press '+' to increase font size, '-' to decrease.");
            UpdateStatus("Press 'F1' for help page.");

            if (m_autoInitiateConnection)
                InitiateConnection();
        }

        protected void Update()
        {
            // Check for screen resize
            if (m_lastScreenHeight != Screen.height || m_lastScreenWidth != Screen.width)
                OnScreenResize();

            // Nothing to update if we haven't subscribed yet
            if (m_subscribed)
            {
                // Make sure lines are initialized before trying to draw them
                if ((object)m_linesInitializedWaitHandle != null)
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
                    for (int i = 0; i < measurements.Count; i++)
                    {
                        Measurement measurement = measurements[i];

                        if (m_dataLines.TryGetValue(measurement.GetSignalID(), out DataLine line))
                            line.UpdateValue((float)measurement.Value);
                    }
                }

                // Update the scales to display the new measurements
                foreach (Scale scale in m_scales.Values)
                    scale.Update();
            }

            long currentTicks = DateTime.UtcNow.Ticks;

            if (currentTicks - m_lastKeyCheck > TimeSpan.TicksPerMillisecond * 200)
            {
                int orgGuiSize = m_guiSize;

                // Plus / Minus keys will increase / decrease font size
                if (Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.KeypadPlus))
                    m_guiSize++;
                else if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
                    m_guiSize--;

                if (m_guiSize < MinGuiSize)
                    m_guiSize = MinGuiSize;

                if (m_guiSize > MaxGuiSize)
                    m_guiSize = MaxGuiSize;

                if (m_guiSize != orgGuiSize)
                {
                    m_lastKeyCheck = currentTicks;
                    OnScreenResize();
                }

                // F1 key will launch help page
                if (Input.GetKey(KeyCode.F1))
                {
                    m_lastKeyCheck = currentTicks;
                    Process.Start("https://github.com/sttp/connection-tester/tree/master/Docs");
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
            }

            // Exit application with "ESC" key
            if (Input.GetKey(KeyCode.Escape))
                EndApplication();
        }

        private void OnGUI()
        {
            Rect controlWindowLocation = m_controlWindowMinimized ? m_controlWindowMinimizedLocation : m_controlWindowActiveLocation;

            GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = m_controlWindowTexture;
            windowStyle.onNormal = windowStyle.normal;

            // Adjust font size for window title for larger GUI sizes
            if (m_guiSize > 1)
                windowStyle.fontSize = 11 * m_guiSize;

            // Create subscription control window
            GUILayout.Window(0, controlWindowLocation, DrawControlsWindow, "Subscription Controls", windowStyle, GUILayout.MaxWidth(Screen.width));

            // Handle click events to show/hide control window
            Event e = Event.current;

            if (e.isMouse && Input.GetMouseButtonUp(0))
            {
                bool mouseOverWindow = controlWindowLocation.Contains(e.mousePosition);

                // If mouse is over minimized control window during click, "pop-up" control window
                if (mouseOverWindow && m_controlWindowMinimized)
                    m_controlWindowMinimized = false;
                else if (!m_controlWindowMinimized)
                    m_controlWindowMinimized = true;
            }

            // Mouse based camera orbit is disabled while control window is active
            if ((object)m_mouseOrbitScript != null)
                m_mouseOrbitScript.isActive = m_controlWindowMinimized;

            // Add a close application button on the main screen, this is handy
            // on mobile deployments where hitting ESC button is not so easy
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 12 * m_guiSize;
            int size = 20 * m_guiSize;

            if (GUI.Button(new Rect(Screen.width - size, 0, size, size), "X", buttonStyle))
                EndApplication();

            GUIStyle versionLabelStyle = new GUIStyle(GUI.skin.label);
            versionLabelStyle.fontSize = 11 * m_guiSize;
            versionLabelStyle.alignment = TextAnchor.UpperLeft;
            GUILayout.Label($"v{m_version}", versionLabelStyle);
        }

        private void DrawControlsWindow(int windowID)
        {
            GUIStyle horizontalScrollbarThumbStyle = new GUIStyle(GUI.skin.horizontalScrollbarThumb);
            GUIStyle verticalScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb);
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            GUIStyle sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            GUIStyle sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);

            float widthScalar = 1.0F;

            // Handle larger sizes for GUI elements
            if (m_guiSize > 1)
            {
                // This work was non-deterministic - should be a better way...
                horizontalScrollbarThumbStyle.fixedHeight *= (m_guiSize * 0.75F);
                verticalScrollbarThumbStyle.fixedWidth *= (m_guiSize * 0.75F);
                labelStyle.fontSize = 11 * m_guiSize;
                textFieldStyle.fontSize = 11 * m_guiSize;
                buttonStyle.fontSize = 11 * m_guiSize;
                sliderStyle.fixedHeight *= m_guiSize;
                sliderThumbStyle.fixedHeight *= m_guiSize;
                sliderThumbStyle.padding.right *= m_guiSize;

                widthScalar = m_guiSize * 0.85F;
            }

            // Adjust vertical alignment for slider control for better vertical centering
            sliderStyle.margin.top += 5;
            sliderThumbStyle.padding.top += 5;

            // Text field contents will auto-stretch control window beyond screen extent,
            // so we add automatic scroll bars to the region in case things expand
            m_scrollPosition = GUILayout.BeginScrollView(m_scrollPosition, horizontalScrollbarThumbStyle, verticalScrollbarThumbStyle);
            GUILayout.BeginVertical();

            // Until a better way is found, just adding some vertical padding
            // with a blank row for larger GUI sizes (optional Row 0)
            if (m_guiSize > 1)
            {
                GUIStyle blankLabelStyle = new GUIStyle(GUI.skin.label);
                blankLabelStyle.fontSize = 4;

                GUILayout.BeginHorizontal();
                GUILayout.Label("", blankLabelStyle);
                GUILayout.EndHorizontal();
            }

            // Row 1 - server connection string
            GUILayout.BeginHorizontal();

            GUILayout.Label(" Connection String:", labelStyle, GUILayout.Width(112 * widthScalar));
            m_connectionString = GUILayout.TextField(m_connectionString, textFieldStyle);

            // Reconnect using new connection string
            if (GUILayout.Button("Connect", buttonStyle, GUILayout.Width(100 * widthScalar)))
                InitiateConnection();

            GUILayout.EndHorizontal();

            // Row 2 - filter expression
            GUILayout.BeginHorizontal();

            GUILayout.Label(" Filter Expression:", labelStyle, GUILayout.Width(108 * widthScalar));
            m_filterExpression = GUILayout.TextField(m_filterExpression, textFieldStyle);

            // Resubscribe using new filter expression
            if (GUILayout.Button("Update", buttonStyle, GUILayout.Width(100 * widthScalar)))
                InitiateSubscription();

            GUILayout.EndHorizontal();

            // Row 3 - historical query
            GUILayout.BeginHorizontal();

            GUILayout.Label(" Start Time:", labelStyle, GUILayout.Width(70 * widthScalar));
            m_startTime = GUILayout.TextField(m_startTime, textFieldStyle);

            GUILayout.Label(" Stop Time:", labelStyle, GUILayout.Width(70 * widthScalar));
            m_stopTime = GUILayout.TextField(m_stopTime, textFieldStyle);

            GUILayout.Label("Process Interval:", labelStyle, GUILayout.Width(100 * widthScalar));
            m_processInterval = (int)GUILayout.HorizontalSlider((float)m_processInterval, 0.0F, 300.0F, sliderStyle, sliderThumbStyle, GUILayout.Width(125 * widthScalar));

            // Dynamically update processing interval when user moves slider control
            if (m_subscribed && m_processInterval != m_lastProcessInterval)
            {
                if (m_lastProcessInterval > 0)
                {
                    m_subscriber.SetHistoricalReplayInterval(m_processInterval);
                    UpdateStatus($"Changing historical replay speed to {(m_processInterval == 0 ? "fast as possible" : $"{m_processInterval}ms")}...");
                }

                m_lastProcessInterval = m_processInterval;
            }

            // Resubscribe with historical replay parameters
            if (GUILayout.Button("Replay", buttonStyle, GUILayout.Width(100 * widthScalar)))
                InitiateSubscription(true);

            GUILayout.EndHorizontal();

            // Row 4 - INI file path
            GUILayout.BeginHorizontal();

            GUIStyle iniLabelStyle = new GUIStyle(GUI.skin.label);
            iniLabelStyle.fontSize = 10 + (m_guiSize > 1 ? m_guiSize * 4 : 0);
            iniLabelStyle.fontStyle = FontStyle.Italic;
            iniLabelStyle.alignment = TextAnchor.UpperCenter;

            GUILayout.Label($" Settings File = \"{Application.persistentDataPath + "/" + IniFileName}\" - Resolution = {Screen.width} x {Screen.height} - Build Date = {m_buildDate}", iniLabelStyle);

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        internal void InitializeSubscription(Guid[] subscribedMeasurementIDs)
        {
            int count = subscribedMeasurementIDs.Length;

            if (count > m_maxSignals)
            {
                subscribedMeasurementIDs = subscribedMeasurementIDs.Take(m_maxSignals).ToArray();
                UpdateStatus($"Reduced {count:N0} subscribed measurements to {m_maxSignals:N0}, configured maximum.");
            }

            // Create a new line for each subscribed measurement, this should be done in
            // advance of updating the legend so the line colors will already be defined
            m_linesInitializedWaitHandle = UIThread.Invoke(CreateDataLines, subscribedMeasurementIDs);
        }

        internal void EnqueData(IList<Measurement> measurements) => m_dataQueue.Enqueue(measurements);

        internal void UpdateStatus(string statusText)
        {
            StringBuilder cumulativeStatusText = new StringBuilder();

            // Keep a finite list of status updates - rolling older text up
            for (int i = 0; i < m_statusText.Length - 1; i++)
            {
                m_statusText[i] = m_statusText[i + 1];
                cumulativeStatusText.Append($"{m_statusText[i]}\r\n");
            }

            statusText = string.Join("\r\n", statusText.GetSegments(85));

            // Append newest status text
            m_statusText[m_statusText.Length - 1] = statusText;
            cumulativeStatusText.Append($"{statusText}\r\n");

            // Show text on 3D status text object
            m_statusMesh.UpdateText(cumulativeStatusText.ToString());

            // Reset timer to hide status after a period of no updates
            if ((object)m_hideStatusTimer != null)
            {
                m_hideStatusTimer.Stop();
                m_hideStatusTimer.Start();
            }
        }

        // Hide status text after a period of no updates
        private void m_hideStatusTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (!m_shuttingDown)
                m_statusMesh?.UpdateText("");
        }

        // Create a new data line for each subscribed measurement
        private void CreateDataLines(object[] args)
        {
            Guid[] subscribedMeasurementIDs;
            DataLine line;
            Scale scale;

            m_subscribed = false;

            if ((object)args == null || args.Length < 1)
                return;

            subscribedMeasurementIDs = args[0] as Guid[];

            if ((object)subscribedMeasurementIDs == null || (object)m_scales == null || (object)m_dataLines == null)
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

                line = new DataLine(this, measurementID, m_dataLines.Count);
                scale = m_scales.GetOrAdd(signalType, type => new Scale(m_graphScale, autoShrinkScale));
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
            Guid[] subscribedMeasurementIDs = state as Guid[];

            if ((object)subscribedMeasurementIDs == null)
                return;

            StringBuilder legendText = new StringBuilder();

            // Go through each subscribed measurement ID and look up its associated metadata
            foreach (Guid measurementID in subscribedMeasurementIDs)
            {
                // Lookup metadata record where SignalID column is our measurement ID
                if (m_subscriber.TryGetMeasurementMetdata(measurementID, out MeasurementMetadata metadata))
                {
                    m_subscriber.TryGetSignalTypeAcronym(measurementID, out string signalType);

                    if (string.IsNullOrWhiteSpace(signalType))
                        signalType = "UNST"; // Unknown signal type

                    // Add formatted metadata label expression to legend text
                    legendText.AppendFormat(m_legendFormat, new MetadataFormatProvider(metadata, signalType));
                    legendText.AppendLine();
                }
            }

            // Update text for 3D text labels object with subscribed point tag names
            m_legendMesh.UpdateText(legendText.ToString());

            // Create a legend line for each subscribed point
            m_legendLines.Clear();

            foreach (Guid measurementID in subscribedMeasurementIDs)
            {
                // Lines must be created on the UI thread
                UIThread.Invoke(CreateLegendLine, measurementID);
            }
        }

        // Create a new legend line
        private void CreateLegendLine(object[] args)
        {
            if ((object)args == null || args.Length < 1)
                return;

            Guid id = (Guid)args[0];

            // Attempt to look up associated data line (for line color)
            if (m_dataLines.TryGetValue(id, out DataLine dataLine))
                m_legendLines.Add(new LegendLine(this, id, m_legendLines.Count, dataLine.VectorColor));
        }

        // Connects or reconnects to a data publisher
        private void InitiateConnection()
        {
            // Shutdown any existing connection
            TerminateConnection();

            // Attempt to extract server name from connection string
            Dictionary<string, string> settings = m_connectionString?.ParseKeyValuePairs();

            if ((object)settings == null || !settings.TryGetValue("server", out string server) || string.IsNullOrEmpty(server))
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

            UpdateStatus($"Attempting connection to \"{hostname}:{port}\"...");

            InitiateSubscription();

            // Initialize subscriber
            m_subscriber.Initialize(hostname, port, udpPort);
            m_subscriber.ConnectAsync();
        }

        // Subscribes or resubscribes to real-time or historical stream using current filter expression
        internal void InitiateSubscription(bool historical = false)
        {
            if (m_subscribed || m_historicalSubscription)
                ClearSubscription();

            m_historicalSubscription = historical;
            m_subscriber.FilterExpression = m_filterExpression;

            if (historical)
            {
                m_subscriber.EstablishHistoricalRead(m_startTime, m_stopTime);
                m_subscriber.SetHistoricalReplayInterval(m_processInterval);
                m_lastProcessInterval = m_processInterval;
                UpdateStatus($"Starting historical replay at {(m_processInterval == 0 ? "fast as possible" : $"{m_processInterval}ms")} playback speed...");
            }
        }

        internal void ConnectionEstablished()
        {
            m_connected = true;
            m_controlWindowMinimized = true;
        }

        internal void ConnectionTerminated()
        {
            m_connected = false;
            ClearSubscription();
        }

        private void OnScreenResize()
        {
            m_lastScreenHeight = Screen.height;
            m_lastScreenWidth = Screen.width;

            // Make control window size adjustments for larger GUI sizes
            float heightScalar = m_guiSize > 1 ? m_guiSize * 0.80F : 1.0F;
            int heighOffset = m_guiSize > 1 ? 12 : 0;

            m_controlWindowActiveLocation = new Rect(0, Screen.height - ControlWindowActiveHeight * heightScalar, Screen.width, ControlWindowActiveHeight * heightScalar);
            m_controlWindowMinimizedLocation = new Rect(0, Screen.height - (ControlWindowMinimizedHeight + heighOffset), Screen.width, ControlWindowActiveHeight * heightScalar);
        }

        private void EraseLine(object[] args)
        {
            if ((object)args == null || args.Length < 1)
                return;

            ILine line = args[0] as ILine;

            if ((object)line == null)
                return;

            line.Stop();
        }

        // Clears an existing subscription
        internal void ClearSubscription()
        {
            m_subscribed = false;

            // Reset subscription state
            m_linesInitializedWaitHandle = null;

            // Clear out existing scales
            if ((object)m_scales != null)
                m_scales.Clear();

            // Erase data lines
            if (m_dataLines?.Count > 0)
            {
                foreach (DataLine dataLine in m_dataLines.Values)
                    UIThread.Invoke(EraseLine, dataLine);

                m_dataLines.Clear();
            }

            // Erase legend lines
            if (m_legendLines?.Count > 0)
            {
                foreach (LegendLine legendLine in m_legendLines)
                    UIThread.Invoke(EraseLine, legendLine);

                m_legendLines.Clear();
            }

            // Clear legend text
            if (!m_shuttingDown)
                m_legendMesh?.UpdateText("");
        }

        // Terminates an existing connection to a data publisher
        private void TerminateConnection()
        {
            ClearSubscription();

            if (m_connected)
            {
                UpdateStatus("Terminating current connection...");
                m_subscriber?.Disconnect();
            }
        }

        private void EndApplication()
        {
            m_shuttingDown = true;

            TerminateConnection();

            if ((object)m_hideStatusTimer != null)
            {
                m_hideStatusTimer.Elapsed -= m_hideStatusTimer_Elapsed;
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
            // Load existing INI file settings
            IniFile iniFile = new IniFile(Application.persistentDataPath + "/" + IniFileName);

            // Apply any user updated settings to INI file. Note that semi-colons are
            // treated as comments in INI files so we suffix connection string with a
            // semi-colon since this string can contain valid semi-colons - only the
            // last one will be treated as a comment prefix and removed at load.
            iniFile["Settings", "ConnectionString"] = m_connectionString + ";";
            iniFile["Settings", "FilterExpression"] = m_filterExpression;
            iniFile["Settings", "StartTime"] = m_startTime;
            iniFile["Settings", "StopTime"] = m_stopTime;
            iniFile["Settings", "GuiSize"] = m_guiSize.ToString();

            // Attempt to save INI file updates
            try
            {
                iniFile.Save();
            }
            catch (Exception ex)
            {
                Debug.Log("ERROR: " + ex.Message);
            }

            m_subscriber?.Dispose();
        }

        #endregion
    }
}