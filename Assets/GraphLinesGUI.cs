//******************************************************************************************************
//  GraphLinesGUI.cs - Gbtc
//
//  Copyright © 2021, Grid Protection Alliance.  All Rights Reserved.
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
//  01/01/2021 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityGSF;

// ReSharper disable CheckNamespace
namespace ConnectionTester
{
    // Since there is so much code dedicated to the subscription controls GUI,
    // the GUI specific code is separated here to make code more manageable
    partial class GraphLines
    {
        #region [ Members ]

        // Constants
        private const string IniFileName = "settings.ini";
        private const int ControlWindowActiveHeight = 130;
        private const int ControlWindowMinimizedHeight = 20;
        private const int MinGuiFontSize = 1;
        private const int MaxGuiFontSize = 3;
        private const int DefaultGuiFontSize = 2;
        private const bool DefaultArrowScrollsTarget = false;

        // Fields
        private string m_userINIPath;
        private Texture2D m_controlWindowTexture;       // Currently set to a solid dark blue
        private Rect m_controlWindowActiveLocation;     // GUI window rectangle when control area is open / expanded
        private Rect m_controlWindowMinimizedLocation;  // GUI window rectangle when control are is closed / minimized
        private bool m_controlWindowMinimized;          // Flag that determines control window minimization state
        private Vector2 m_scrollPosition;               // Current scroll position of control window (when scrollbars present)
        private int m_lastScreenHeight = -1;            // Last screen height (used to signal screen resize event)
        private int m_lastScreenWidth = -1;             // Last screen width (used to signal screen resize event)
        private bool m_lastMouseOverWindowTitle;        // Last mouse over control window title state (used to control link cursor)
        private int m_guiFontSize = DefaultGuiFontSize; // Current GUI font size of control window              

        #endregion

        #region [ Methods ]

        // Called from Unity Awake event handler
        private void LoadSettings()
        {
            string defaultIniPath = $"{Application.dataPath}/{IniFileName}";
            m_userINIPath = $"{Application.persistentDataPath}/{IniFileName}";

            // Copy INI file with default settings to user INI file if one doesn't exist
            if (File.Exists(defaultIniPath) && !File.Exists(m_userINIPath))
                File.Copy(defaultIniPath, m_userINIPath);

            // Load settings from INI file
            IniFile iniFile = new IniFile(m_userINIPath);

            m_connectionString = iniFile["Settings", "ConnectionString", DefaultConnectionString];
            m_filterExpression = iniFile["Settings", "FilterExpression", DefaultFilterExpression];
            m_startTime = iniFile["Settings", "StartTime", DefaultStartTime];
            m_stopTime = iniFile["Settings", "StopTime", DefaultStopTime];

            if (!int.TryParse(iniFile["Settings", "MaxSignals", DefaultMaxSignals], out m_maxSignals))
                m_maxSignals = DefaultMaxSignals;

            if (!bool.TryParse(iniFile["Settings", "AutoInitiateConnection", DefaultAutoInitiateConnection], out m_autoInitiateConnection))
                m_autoInitiateConnection = DefaultAutoInitiateConnection;

            if (!int.TryParse(iniFile["Settings", "StatusRows", DefaultStatusRows], out m_statusRows))
                m_statusRows = DefaultStatusRows;

            if (!int.TryParse(iniFile["Settings", "GuiSize", DefaultGuiFontSize], out m_guiFontSize))
                m_guiFontSize = DefaultGuiFontSize;

            if (!bool.TryParse(iniFile["Settings", "ArrowScrollsTarget", DefaultArrowScrollsTarget], out m_mouseOrbitScript.ArrowScrollsTarget))
                m_mouseOrbitScript.ArrowScrollsTarget = DefaultArrowScrollsTarget;

                // Validate deserialized GUI size
            if (m_guiFontSize < MinGuiFontSize)
                m_guiFontSize = MinGuiFontSize;

            if (m_guiFontSize > MaxGuiFontSize)
                m_guiFontSize = MaxGuiFontSize;

            Title = iniFile["Settings", "Title", DefaultTitle];
            LineWidth = int.Parse(iniFile["Settings", "LineWidth", DefaultLineWidth.ToString()]);
            LineDepthOffset = float.Parse(iniFile["Settings", "LineDepthOffset", DefaultLineDepthOffset.ToString(CultureInfo.InvariantCulture)]);
            PointsInLine = int.Parse(iniFile["Settings", "PointsInLine", DefaultPointsInLine.ToString()]);
            LegendFormat = iniFile["Settings", "LegendFormat", DefaultLegendFormat];
            StatusDisplayInterval = double.Parse(iniFile["Settings", "StatusDisplayInterval", DefaultStatusDisplayInterval.ToString(CultureInfo.InvariantCulture)]);

            // TODO: Add setting to control "left-to-right" line fill

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

            m_controlWindowMinimized = m_autoInitiateConnection;

            // For mobile applications we use a larger GUI font size.
            // Other deployments might benefit from this as well - larger
            // size modes may work also but are not tested
            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
                m_guiFontSize = Screen.height <= 720 ? 2 : 3;

            // Create a solid background for the control window
            m_controlWindowTexture = new Texture2D(1, 1);
            m_controlWindowTexture.SetPixel(0, 0, new Color32(10, 25, 70, 255));
            m_controlWindowTexture.Apply();
        }

        // Called from Unity OnApplicationQuit event handler
        private void SaveSettings()
        {
            // Load existing INI file settings
            IniFile iniFile = new IniFile(m_userINIPath ?? $"{Application.persistentDataPath}/{IniFileName}");

            // Save any user updated settings to INI file
            iniFile["Settings", "ConnectionString"] = $"{m_connectionString};"; // See note below *
            iniFile["Settings", "FilterExpression"] = m_filterExpression;
            iniFile["Settings", "StartTime"] = m_startTime;
            iniFile["Settings", "StopTime"] = m_stopTime;
            iniFile["Settings", "GuiSize"] = m_guiFontSize.ToString();

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
        }

        // Called from Unity Update event handler
        private void CheckForScreenResize()
        {
            if (m_lastScreenHeight != Screen.height || m_lastScreenWidth != Screen.width)
                OnScreenResize();
        }

        // Called from Unity Update event handler
        private void CheckForFontSizeHotKeys(long currentTicks)
        {
            int orgGuiSize = m_guiFontSize;

            // Plus / Minus keys will increase / decrease font size
            if (Input.GetKey(KeyCode.Plus) || Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.Equals) && m_shiftIsDown)
                m_guiFontSize++;
            else if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
                m_guiFontSize--;

            if (m_guiFontSize < MinGuiFontSize)
                m_guiFontSize = MinGuiFontSize;

            if (m_guiFontSize > MaxGuiFontSize)
                m_guiFontSize = MaxGuiFontSize;

            if (m_guiFontSize != orgGuiSize)
            {
                m_lastKeyCheck = currentTicks;
                OnScreenResize();
            }
        }

        private void OnGUI()
        {
            Rect controlWindowLocation = m_controlWindowMinimized ? m_controlWindowMinimizedLocation : m_controlWindowActiveLocation;
            Event e = Event.current;
            GUI.skin = UISkin;

            GUIStyle windowStyle = new GUIStyle(GUI.skin.window) 
            {
                normal = { background = m_controlWindowTexture },
                richText = true
            };
            
            windowStyle.onNormal = windowStyle.normal;

            // Adjust font size for window title for larger GUI sizes
            if (m_guiFontSize > 1)
                windowStyle.fontSize = 11 * m_guiFontSize;

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
                else if (m_lastMouseOverWindowTitle)
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }

            m_lastMouseOverWindowTitle = mouseOverWindowTitle;

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
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12 * m_guiFontSize
            };

            int size = 20 * m_guiFontSize;

            if (GUI.Button(new Rect(Screen.width - size, 0, size, size), "X", buttonStyle))
                EndApplication();

            GUIStyle versionLabelStyle = new GUIStyle(GUI.skin.label) 
            {
                fontSize = 11 * m_guiFontSize,
                alignment = TextAnchor.UpperLeft,
                richText = true
            };
            
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
            if (m_guiFontSize > 1)
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

                horizontalScrollbarStyle.fixedHeight *= m_guiFontSize * 0.75F;
                horizontalScrollbarThumbStyle.fixedHeight = horizontalScrollbarStyle.fixedHeight;
                horizontalScrollbarLeftButtonStyle.fixedHeight = horizontalScrollbarStyle.fixedHeight;
                horizontalScrollbarRightButtonStyle.fixedHeight = horizontalScrollbarStyle.fixedHeight;

                verticalScrollbarStyle.fixedWidth *= m_guiFontSize * 0.75F;
                verticalScrollbarThumbStyle.fixedWidth = verticalScrollbarStyle.fixedWidth;
                verticalScrollbarUpButtonStyle.fixedWidth = verticalScrollbarStyle.fixedWidth;
                verticalScrollbarDownButtonStyle.fixedWidth = verticalScrollbarStyle.fixedWidth;

                // This work was non-deterministic - should be a better way...
                labelStyle.fontSize = 11 * m_guiFontSize;
                textFieldStyle.fontSize = 11 * m_guiFontSize;
                buttonStyle.fontSize = 11 * m_guiFontSize;
                sliderStyle.fixedHeight *= m_guiFontSize;
                sliderThumbStyle.fixedHeight *= m_guiFontSize;
                sliderThumbStyle.padding.right *= m_guiFontSize;

                widthScalar = m_guiFontSize * 0.85F;
            }

            // Adjust vertical alignment for slider control for better vertical centering
            sliderStyle.margin.top += 5;
            sliderThumbStyle.padding.top += 5;

            // Text field contents will auto-stretch control window beyond screen extent,
            // so we add automatic scroll bars to the region in case things expand
            m_scrollPosition = m_guiFontSize > 1 ?
                GUILayout.BeginScrollView(m_scrollPosition, horizontalScrollbarStyle, verticalScrollbarStyle) :
                GUILayout.BeginScrollView(m_scrollPosition);

            GUILayout.BeginVertical();

            // Add some vertical padding with a blank row for larger GUI sizes (optional Row 0)
            GUIStyle blankLabelStyle = new GUIStyle(GUI.skin.label);

            if (m_guiFontSize > 1)
            {
                blankLabelStyle.fontSize = 6;

                for (int i = 1; i < m_guiFontSize; i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("", blankLabelStyle);
                    GUILayout.EndHorizontal();
                }
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

            // Add some vertical padding for larger font sizes with a blank row as separator for INI file path that follows
            if (m_guiFontSize > 1)
            {
                blankLabelStyle.fontSize = 1 + (m_guiFontSize - 1) * 2;

                GUILayout.BeginHorizontal();
                GUILayout.Label("", blankLabelStyle);
                GUILayout.EndHorizontal();
            }

            // Row 4 - INI file path
            GUILayout.BeginHorizontal();

            GUIStyle iniLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10 + (m_guiFontSize > 1 ? m_guiFontSize * (m_guiFontSize > 2 ? 3 : 1) : 0),
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.UpperCenter
            };

            GUILayout.Label($" Settings File = \"{Application.persistentDataPath}/{IniFileName}\" - Resolution = {Screen.width} x {Screen.height} - Build Date = {m_buildDate}", iniLabelStyle);

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void OnScreenResize()
        {
            m_lastScreenHeight = Screen.height;
            m_lastScreenWidth = Screen.width;

            // Make control window size adjustments for larger GUI sizes
            float heightScalar = m_guiFontSize > 1 ? m_guiFontSize * (m_guiFontSize > 2 ? 0.75F : 0.83F) : 1.0F;
            int heighOffset = (m_guiFontSize - 1) * 12;

            m_controlWindowActiveLocation = new Rect(0, Screen.height - ControlWindowActiveHeight * heightScalar, Screen.width, ControlWindowActiveHeight * heightScalar);
            m_controlWindowMinimizedLocation = new Rect(0, Screen.height - (ControlWindowMinimizedHeight + heighOffset), Screen.width, ControlWindowActiveHeight * heightScalar);
        }

        #endregion
    }
}
