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
// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable RedundantCast.0
// ReSharper disable ArrangeObjectCreationWhenTypeEvident
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
            string pluginPath = Path.Combine(Path.GetFullPath("."), $"{Common.GetTargetName()}_Data", "Plugins");

            if (IntPtr.Size == 8)
                pluginPath = Path.Combine(pluginPath, "x86_64");
            else
                pluginPath = Path.Combine(pluginPath, "x86");

            if (!currentPath?.Contains(pluginPath) ?? false)
                Environment.SetEnvironmentVariable("PATH", $"{currentPath}{Path.PathSeparator}{pluginPath}", EnvironmentVariableTarget.Process);
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
                        if ((m_scaleMax - m_scaleMin) * ShrinkStartThreshold >= displayMax - displayMin)
                            m_timeUntilShrink -= Time.deltaTime;
                        else
                            m_timeUntilShrink = ShrinkDelay;
                    }

                    if (m_timeUntilShrink <= 0.0F)
                    {
                        m_scaleMin += (displayMin - m_scaleMin) * Time.deltaTime * 5.0F;
                        m_scaleMax -= (m_scaleMax - displayMax) * Time.deltaTime * 5.0F;

                        if ((m_scaleMax - m_scaleMin) * ShrinkStopThreshold <= displayMax - displayMin)
                            m_timeUntilShrink = ShrinkDelay;
                    }
                }

                ScaleLinePoints();
            }

            private void ScaleLinePoints()
            {
                foreach (DataLine line in m_lines)
                {
                    for (int x = 0; x < line.UnscaledData.Length; x++)
                    {
                        float unscaledValue = line.UnscaledData[x];

                        if (float.IsNaN(unscaledValue))
                            unscaledValue = MidPoint;

                        Vector3 point = line.LinePoints[x];
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
                UnscaledData = new float[parent.PointsInLine];

                m_vector = new VectorLine($"DataLine{index}", new List<Vector3>(parent.PointsInLine), parent.LineMaterial, parent.LineWidth, LineType.Continuous)
                {
                    color = parent.LineColors[index % parent.LineColors.Length],
                    drawTransform = parent.Target
                };

                m_vector.Draw3DAuto();

                for (int x = 0; x < m_vector.points3.Count; x++)
                {
                    UnscaledData[x] = float.NaN;
                    m_vector.points3[x] = new Vector3(Mathf.Lerp(-5.0F, 5.0F, x / (float)m_vector.points3.Count), -((index + 1) * parent.LineDepthOffset + 0.05F), 0.0F);
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
                if (m_vector is null)
                    return;

                m_vector.StopDrawing3DAuto();
                VectorLine.Destroy(ref m_vector);
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
                Transform transform = parent.LegendMesh.transform;
                Vector3 position = transform.position;

                ID = id;
                
                m_vector = new VectorLine($"LegendLine{index}", new List<Vector3>(2), parent.LineMaterial, parent.LineWidth, LineType.Discrete)
                {
                    color = color, 
                    drawTransform = transform
                };
                
                m_vector.Draw3DAuto();

                float spacing = parent.LegendMesh.characterSize * 1.96F;

                // Position legend line relative to text descriptions
                Vector3 point1 = new Vector3(-2.0F, -(spacing / 2.0F + index * spacing), -position.z);
                Vector3 point2 = new Vector3(-0.5F, point1.y, point1.z);

                m_vector.points3[0] = point1;
                m_vector.points3[1] = point2;
            }

            public Guid ID { get; }

            public void Stop()
            {
                if (m_vector is null)
                    return;

                m_vector.StopDrawing3DAuto();
                VectorLine.Destroy(ref m_vector);
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

                return typeof(MeasurementMetadata).GetProperty(propertyName)?.GetValue(m_metadata).ToString() ?? $"<{propertyName}>";
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
        private bool m_lastMouseOverWindow;
        private int m_guiSize = 2;
        private long m_lastKeyCheck;
        private bool m_shiftIsDown;
        private bool m_connected;
        private bool m_connecting;
        private bool m_subscribed;
        private bool m_shuttingDown;

        // Public fields exposed to Unity UI interface
        public string Title = "STTP Connection Tester";
        public string ConnectionString = "server=localhost:7165 ;";
        public string FilterExpression = "FILTER TOP 10 ActiveMeasurements WHERE SignalType='FREQ' OR SignalType LIKE 'VPH*'";
        public Texture LineMaterial;
        public int LineWidth = 4;
        public float LineDepthOffset = 0.75F;
        public int PointsInLine = 50;
        public Transform Target;
        public float GraphScale = 5.0F;
        public Color[] LineColors = { Color.blue, Color.yellow, Color.red, Color.white, Color.cyan, Color.magenta, Color.black, Color.gray };
        public TextMesh LegendMesh;
        public TextMesh StatusMesh;
        public int StatusRows = 4;
        public double StatusDisplayInterval = 10000.0D;
        public string LegendFormat = "{0:SignalTypeAcronym}: {0:Description} [{0:PointTag}]";
        public GUISkin UISkin;
        public Texture2D LinkCursor;

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
            string defaultIniPath = $"{Application.dataPath}/{IniFileName}";
            string userIniPath = $"{Application.persistentDataPath}/{IniFileName}";

            // Copy INI file with default settings to user INI file if one doesn't exist
            if (File.Exists(defaultIniPath) && !File.Exists(userIniPath))
                File.Copy(defaultIniPath, userIniPath);

            // Load settings from INI file
            IniFile iniFile = new IniFile(userIniPath);

            Title = iniFile["Settings", "Title", Title];
            ConnectionString = iniFile["Settings", "ConnectionString", ConnectionString];
            FilterExpression = iniFile["Settings", "FilterExpression", FilterExpression];
            LineWidth = int.Parse(iniFile["Settings", "LineWidth", LineWidth.ToString()]);
            LineDepthOffset = float.Parse(iniFile["Settings", "LineDepthOffset", LineDepthOffset.ToString(CultureInfo.InvariantCulture)]);
            PointsInLine = int.Parse(iniFile["Settings", "PointsInLine", PointsInLine.ToString()]);
            LegendFormat = iniFile["Settings", "LegendFormat", LegendFormat];
            StatusRows = int.Parse(iniFile["Settings", "StatusRows", StatusRows.ToString()]);
            StatusDisplayInterval = double.Parse(iniFile["Settings", "StatusDisplayInterval", StatusDisplayInterval.ToString(CultureInfo.InvariantCulture)]);

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
            #if UNITY_EDITOR
                Debug.Log($"ERROR: {ex.Message}");
            #endif
            }

            // Attempt to reference active mouse orbit script
            m_mouseOrbitScript = GetComponent<MouseOrbit>();

            // Create line dictionary and data queue
            m_scales = new ConcurrentDictionary<string, Scale>();
            m_dataLines = new ConcurrentDictionary<Guid, DataLine>();
            m_dataQueue = new ConcurrentQueue<IList<Measurement>>();
            m_legendLines = new List<LegendLine>();

            // Initialize status rows and timer to hide status after a period of no updates
            m_statusText = new string[StatusRows];

            for (int i = 0; i < StatusRows; i++)
                m_statusText[i] = "";

            m_hideStatusTimer = new System.Timers.Timer
            {
                AutoReset = false,
                Interval = StatusDisplayInterval
            };
            
            m_hideStatusTimer.Elapsed += m_hideStatusTimer_Elapsed;

            // For mobile applications we use a larger GUI font size.
            // Other deployments might benefit from this as well - larger
            // size modes may work also but are not tested
            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
                m_guiSize = Screen.height <= 720 ? 2 : 3;

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
                    LegendMesh = legendObject.GetComponent<TextMesh>();
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
                "Press '+' to increase font size, or '-' to decrease.\r\n" +
                "Press 'C' to connect, 'D' to disconnect.\r\n" +
                "Press 'R' to restore default graph location.\r\n" +
                "Press 'M' to toggle status message display.\r\n" +
                "Press 'F1' for help page, or 'H' for this message.";

            for (int i = 0; i < m_statusText.Length - 1; i++)
                m_statusText[i] = "";

            UpdateStatus(HelpText, 30000);
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
                if (Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.Equals) && m_shiftIsDown)
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

                // Restore default graph location with "R" key
                if (Input.GetKey(KeyCode.R))
                {
                    m_lastKeyCheck = currentTicks;
                    m_mouseOrbitScript.Restore = true;
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

        private void OnGUI()
        {
            Rect controlWindowLocation = m_controlWindowMinimized ? m_controlWindowMinimizedLocation : m_controlWindowActiveLocation;
            Event e = Event.current;

            GUI.skin = UISkin;
            GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = m_controlWindowTexture;
            windowStyle.onNormal = windowStyle.normal;
            windowStyle.richText = true;

            // Adjust font size for window title for larger GUI sizes
            if (m_guiSize > 1)
                windowStyle.fontSize = 11 * m_guiSize;

            bool mouseOverWindowTitle;

            if (m_controlWindowMinimized)
            {
                mouseOverWindowTitle = controlWindowLocation.Contains(e.mousePosition);
            }
            else
            {
                Rect controlWindowTitleLocation = new Rect(controlWindowLocation.x, controlWindowLocation.y, controlWindowLocation.width, Screen.height - m_controlWindowMinimizedLocation.y);
                mouseOverWindowTitle = controlWindowTitleLocation.Contains(e.mousePosition);
            }

            string controlWindowTitle = $"<b>[</b> <color=yellow>Subscription Controls</color> <b>]</b>{(mouseOverWindowTitle ? $" - <i>Click to {(m_controlWindowMinimized ? "Expand" : "Minimize")}</i>" : "")}";

            // Create subscription control window
            GUILayout.Window(0, controlWindowLocation, DrawControlsWindow, controlWindowTitle, windowStyle, GUILayout.MaxWidth(Screen.width));

            // Handle click events to show/hide control window
            if (!(LinkCursor is null))
            {
                if (mouseOverWindowTitle)
                    Cursor.SetCursor(LinkCursor, Vector2.zero, CursorMode.Auto);
                else if (m_lastMouseOverWindow)
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }

            m_lastMouseOverWindow = mouseOverWindowTitle;

            if (e.isMouse && Input.GetMouseButtonUp(0))
            {
                if (mouseOverWindowTitle)
                {
                    // If mouse is over control window title during click, show or hide control window
                    if (m_controlWindowMinimized)
                    {
                        m_controlWindowMinimized = false;
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    }
                    else
                    {
                        m_controlWindowMinimized = true;
                    }
                }
                else if (!controlWindowLocation.Contains(e.mousePosition))
                {
                    // If user clicks on main window, minimize control window
                    m_controlWindowMinimized = true;
                }
            }

            m_shiftIsDown = e.shift;

            // Mouse based camera orbit is disabled while control window is active
            if (!(m_mouseOrbitScript is null))
                m_mouseOrbitScript.IsActive = m_controlWindowMinimized;

            // Make sure no text boxes have focus when control window is minimized
            // so any hot keys do not get added to active text fields
            if (m_controlWindowMinimized)
                GUI.FocusControl(null);

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
            versionLabelStyle.richText = true;
            GUILayout.Label($"<color=yellow>v{m_version}</color>", versionLabelStyle);
        }

        private void DrawControlsWindow(int windowID)
        {
            float widthScalar = 1.0F;

            GUIStyle horizontalScrollbarStyle = null;
            GUIStyle verticalScrollbarStyle = null;
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            GUIStyle sliderStyle = new GUIStyle(GUI.skin.horizontalSlider);
            GUIStyle sliderThumbStyle = new GUIStyle(GUI.skin.horizontalSliderThumb);

            // Handle larger sizes for GUI elements
            if (m_guiSize > 1)
            {
                Dictionary<string, GUIStyle> customStyles = GUI.skin.customStyles.ToDictionary(style => style.name, StringComparer.OrdinalIgnoreCase);

                horizontalScrollbarStyle = new GUIStyle(GUI.skin.horizontalScrollbar) { name = "sizedhorizontal" };
                GUIStyle horizontalScrollbarThumbStyle = new GUIStyle(GUI.skin.horizontalScrollbarThumb) { name = "sizedhorizontalthumb" };
                GUIStyle horizontalScrollbarLeftButtonStyle = new GUIStyle(GUI.skin.horizontalScrollbarLeftButton) { name = "sizedhorizontalleftbutton" };
                GUIStyle horizontalScrollbarRightButtonStyle = new GUIStyle(GUI.skin.horizontalScrollbarRightButton) { name = "sizedhorizontalrightbutton" };

                customStyles[horizontalScrollbarStyle.name] = horizontalScrollbarStyle;
                customStyles[horizontalScrollbarThumbStyle.name] = horizontalScrollbarThumbStyle;
                customStyles[horizontalScrollbarLeftButtonStyle.name] = horizontalScrollbarLeftButtonStyle;
                customStyles[horizontalScrollbarRightButtonStyle.name] = horizontalScrollbarRightButtonStyle;

                verticalScrollbarStyle = new GUIStyle(GUI.skin.verticalScrollbar) { name = "sizedvertical" };
                GUIStyle verticalScrollbarThumbStyle = new GUIStyle(GUI.skin.verticalScrollbarThumb) { name = "sizedverticalthumb" };
                GUIStyle verticalScrollbarUpButtonStyle = new GUIStyle(GUI.skin.verticalScrollbarUpButton) { name = "sizedverticalupbutton" };
                GUIStyle verticalScrollbarDownButtonStyle = new GUIStyle(GUI.skin.verticalScrollbarDownButton) { name = "sizedverticaldownbutton" };

                customStyles[verticalScrollbarStyle.name] = verticalScrollbarStyle;
                customStyles[verticalScrollbarThumbStyle.name] = verticalScrollbarThumbStyle;
                customStyles[verticalScrollbarUpButtonStyle.name] = verticalScrollbarUpButtonStyle;
                customStyles[verticalScrollbarDownButtonStyle.name] = verticalScrollbarDownButtonStyle;

                GUI.skin.customStyles = customStyles.Values.ToArray();

                horizontalScrollbarStyle.fixedHeight *= m_guiSize * 0.75F;
                horizontalScrollbarThumbStyle.fixedHeight = horizontalScrollbarStyle.fixedHeight;
                horizontalScrollbarLeftButtonStyle.fixedHeight = horizontalScrollbarStyle.fixedHeight;
                horizontalScrollbarRightButtonStyle.fixedHeight = horizontalScrollbarStyle.fixedHeight;

                verticalScrollbarStyle.fixedWidth *= m_guiSize * 0.75F;
                verticalScrollbarThumbStyle.fixedWidth = verticalScrollbarStyle.fixedWidth;
                verticalScrollbarUpButtonStyle.fixedWidth = verticalScrollbarStyle.fixedWidth;
                verticalScrollbarDownButtonStyle.fixedWidth = verticalScrollbarStyle.fixedWidth;

                // This work was non-deterministic - should be a better way...
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
            m_scrollPosition = m_guiSize > 1 ? 
                GUILayout.BeginScrollView(m_scrollPosition, horizontalScrollbarStyle, verticalScrollbarStyle) :
                GUILayout.BeginScrollView(m_scrollPosition);
            
            GUILayout.BeginVertical();

            // Add some vertical padding with a blank row for larger GUI sizes (optional Row 0)
            GUIStyle blankLabelStyle = new GUIStyle(GUI.skin.label);

            if (m_guiSize > 1)
            {
                blankLabelStyle.fontSize = 6;

                for (int i = 1; i < m_guiSize; i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("", blankLabelStyle);
                    GUILayout.EndHorizontal();
                }
            }

            // Row 1 - server connection string
            GUILayout.BeginHorizontal();

            GUILayout.Label(" Connection String:", labelStyle, GUILayout.Width(112 * widthScalar));
            ConnectionString = GUILayout.TextField(ConnectionString, textFieldStyle);

            // Reconnect using new connection string
            if (GUILayout.Button("Connect", buttonStyle, GUILayout.Width(100 * widthScalar)))
                InitiateConnection();

            GUILayout.EndHorizontal();

            // Row 2 - filter expression
            GUILayout.BeginHorizontal();

            GUILayout.Label(" Filter Expression:", labelStyle, GUILayout.Width(108 * widthScalar));
            FilterExpression = GUILayout.TextField(FilterExpression, textFieldStyle);

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

            // Add some vertical padding for larger font sizes with a blank row as separator for INI file path that follows
            if (m_guiSize > 1)
            {
                blankLabelStyle.fontSize = 1 + (m_guiSize - 1) * 2;

                GUILayout.BeginHorizontal();
                GUILayout.Label("", blankLabelStyle);
                GUILayout.EndHorizontal();
            }

            // Row 4 - INI file path
            GUILayout.BeginHorizontal();

            GUIStyle iniLabelStyle = new GUIStyle(GUI.skin.label);
            iniLabelStyle.fontSize = 10 + (m_guiSize > 1 ? m_guiSize * (m_guiSize > 2 ? 3 : 1): 0);
            iniLabelStyle.fontStyle = FontStyle.Italic;
            iniLabelStyle.alignment = TextAnchor.UpperCenter;

            GUILayout.Label($" Settings File = \"{Application.persistentDataPath}/{IniFileName}\" - Resolution = {Screen.width} x {Screen.height} - Build Date = {m_buildDate}", iniLabelStyle);

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
                UpdateStatus($"WARNING: Reduced {count:N0} subscribed measurements to {m_maxSignals:N0}, current configured maximum.");
            }

            // Create a new line for each subscribed measurement, this should be done in
            // advance of updating the legend so the line colors will already be defined
            m_linesInitializedWaitHandle = UIThread.Invoke(CreateDataLines, subscribedMeasurementIDs);
        }

        internal void EnqueData(IList<Measurement> measurements) => m_dataQueue.Enqueue(measurements);

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

                // Append newest status text
                statusText = string.Join(Environment.NewLine, statusText.GetSegments(95));
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
        private void m_hideStatusTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
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

                // Add formatted metadata label expression to legend text
                legendText.AppendFormat(LegendFormat, new MetadataFormatProvider(metadata, signalType));
                legendText.AppendLine();
            }

            // Update text for 3D text labels object with subscribed point tag names
            LegendMesh.UpdateText(legendText.ToString());

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
            if (args is null || args.Length < 1)
                return;

            Guid id = (Guid)args[0];

            // Attempt to look up associated data line (for line color)
            if (m_dataLines.TryGetValue(id, out DataLine dataLine))
                m_legendLines.Add(new LegendLine(this, id, m_legendLines.Count, dataLine.VectorColor));
        }

        // Connects or reconnects to a data publisher
        private void InitiateConnection()
        {
            m_connecting = true;

            // Shutdown any existing connection
            TerminateConnection();

            // Attempt to extract server name from connection string
            Dictionary<string, string> settings = ConnectionString?.ParseKeyValuePairs();

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
            m_subscriber.FilterExpression = FilterExpression;

            if (!historical)
                return;

            m_subscriber.EstablishHistoricalRead(m_startTime, m_stopTime);
            m_subscriber.SetHistoricalReplayInterval(m_processInterval);
            m_lastProcessInterval = m_processInterval;
            
            UpdateStatus($"Starting historical replay at {(m_processInterval == 0 ? "fast as possible" : $"{m_processInterval}ms")} playback speed...");
        }

        internal void ConnectionEstablished()
        {
            m_connected = true;
            m_connecting = false;
            m_controlWindowMinimized = true;
        }

        internal void ConnectionTerminated()
        {
            m_connected = false;
            m_connecting = false;
            ClearSubscription();
        }

        private void OnScreenResize()
        {
            m_lastScreenHeight = Screen.height;
            m_lastScreenWidth = Screen.width;

            // Make control window size adjustments for larger GUI sizes
            float heightScalar = m_guiSize > 1 ? m_guiSize * (m_guiSize > 2 ? 0.75F : 0.83F) : 1.0F;
            int heighOffset = (m_guiSize - 1) * 12;

            m_controlWindowActiveLocation = new Rect(0, Screen.height - ControlWindowActiveHeight * heightScalar, Screen.width, ControlWindowActiveHeight * heightScalar);
            m_controlWindowMinimizedLocation = new Rect(0, Screen.height - (ControlWindowMinimizedHeight + heighOffset), Screen.width, ControlWindowActiveHeight * heightScalar);
        }

        private void EraseLine(object[] args)
        {
            if (args is null || args.Length < 1)
                return;

            if (!(args[0] is ILine line))
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
            m_scales?.Clear();

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
            IniFile iniFile = new IniFile($"{Application.persistentDataPath}/{IniFileName}");

            // Apply any user updated settings to INI file. Note that semi-colons are
            // treated as comments in INI files so we suffix connection string with a
            // semi-colon since this string can contain valid semi-colons - only the
            // last one will be treated as a comment prefix and removed at load.
            iniFile["Settings", "ConnectionString"] = $"{ConnectionString} ;"; // See note below *
            iniFile["Settings", "FilterExpression"] = FilterExpression;
            iniFile["Settings", "StartTime"] = m_startTime;
            iniFile["Settings", "StopTime"] = m_stopTime;
            iniFile["Settings", "GuiSize"] = m_guiSize.ToString();

            // * Trailing semi-colon is intentational. Since optional connection string parameters
            // will be separated by semi-colon and INI files treat trailing semi-colons as comment 
            // markers, the connection string will always need a semi-colon at the end of the line
            // when serialized to prevent removal of optional connection string parameters on load.

            // Attempt to save INI file updates
            try
            {
                iniFile.Save();
            }
            catch (Exception ex)
            {
            #if UNITY_EDITOR
                Debug.Log($"ERROR: {ex.Message}");
            #endif
            }

            m_subscriber?.Dispose();
        }

        #endregion
    }
}