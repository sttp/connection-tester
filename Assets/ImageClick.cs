//******************************************************************************************************
//  ImageClick.cs - Gbtc
//
//  Copyright © 2020, Grid Protection Alliance.  All Rights Reserved.
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
//  12/30/2020 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System.Diagnostics;
using UnityEngine;
using UnityEngine.EventSystems;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace UnityGSF
{
    public class ImageClick : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public string URL;
        public Texture2D LinkCursor;
        public TextMesh ToolTip;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!string.IsNullOrWhiteSpace(URL))
                Process.Start(URL);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!(LinkCursor is null))
                Cursor.SetCursor(LinkCursor, Vector2.zero, CursorMode.Auto);

            if (!(ToolTip is null) && !string.IsNullOrWhiteSpace(URL))
                ToolTip.UpdateText(URL);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!(LinkCursor is null))
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            if (!(ToolTip is null) && !string.IsNullOrWhiteSpace(URL))
                ToolTip.UpdateText("");
        }
    }
}