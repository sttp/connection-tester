//******************************************************************************************************
//  EditorEventManager.cs - Gbtc
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
//  07/01/2019 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace ConnectionTester.Editor
{
    [InitializeOnLoad]
    public class EditorEventManager
    {
        static EditorEventManager()
        {
            // Setup path to load proper version of native sttp.net.lib.dll from inside Unity editor
            string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
            string dataPath = Application.dataPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        #if UNITY_EDITOR_32
            string dllPath = Path.Combine(dataPath, "sttp.net", "Plugins", "x86");
        #else
            string dllPath = Path.Combine(dataPath, "sttp.net", "Plugins", "x86_64");
        #endif

            if (!currentPath?.Contains(dllPath) ?? false)
                Environment.SetEnvironmentVariable("PATH", $"{currentPath}{Path.PathSeparator}{dllPath}", EnvironmentVariableTarget.Process);

            EditorApplication.playModeStateChanged += PlayModeStateChangedHandler;
        }

        private static void PlayModeStateChangedHandler(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                GraphLines.EditorExitingPlayMode();
        }
    }
}