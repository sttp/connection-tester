//******************************************************************************************************
//  sttp_editor.cs - Gbtc
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
//  07/01/2019 - j. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// ReSharper disable once CheckNamespace
[InitializeOnLoad]
public class sttp_editor
{
    static sttp_editor()
    {
        // Setup path to load proper version of native sttp.net.lib.dll
        string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);

#if UNITY_EDITOR_32
        string dllPath = $"{Application.dataPath}{Path.DirectorySeparatorChar}sttp.net{Path.DirectorySeparatorChar}Plugins{Path.DirectorySeparatorChar}x86";
#elif UNITY_EDITOR_64
        string dllPath = $"{Application.dataPath}{Path.DirectorySeparatorChar}sttp.net{Path.DirectorySeparatorChar}Plugins{Path.DirectorySeparatorChar}x86_64";
#else // Player
        string dllPath = $"{Application.dataPath}{Path.DirectorySeparatorChar}Plugins";
#endif
        if (!currentPath?.Contains(dllPath) ?? false)
            Environment.SetEnvironmentVariable("PATH", $"{currentPath}{Path.PathSeparator}{dllPath}", EnvironmentVariableTarget.Process);
    }
}
