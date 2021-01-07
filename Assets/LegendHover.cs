//******************************************************************************************************
//  LegendHover.cs - Gbtc
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
//  01/02/2021 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Vectrosity;

// ReSharper disable once CheckNamespace
namespace ConnectionTester
{
    // This script controls the value tooltip-style display - it should be directly
    // attatched to TextMeshPro control that displays legend
    [RequireComponent(typeof(TextMeshPro))]
    public class LegendHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private TextMeshPro m_legendMesh;
        private TextMeshPro m_displayValueMesh;
        private VectorLine m_border;
        private bool m_mouseIsOverLegendMesh;
        private int m_activeLinkIndex = -1;

        public GraphLines ParentScript; // GraphLines controls text to display, this script controls location and visibility
        public Image Container;         // Image which represents value tool-tip background, basically a text mesh container
        public Color BorderColor = Color.yellow;
        public int BorderWidth = 3;
        public int OrderInLayer = 1;

        protected void Awake()
        {
            m_legendMesh = gameObject.GetComponent<TextMeshPro>();
            m_displayValueMesh = ParentScript.DisplayValueMesh;
            CreateBorder();
        }

        protected void OnApplicationQuit()
        {
            DeleteBorder();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_mouseIsOverLegendMesh = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_mouseIsOverLegendMesh = false;
        }

        protected void LateUpdate()
        {
            if (m_mouseIsOverLegendMesh)
            {
                int linkIndex = TMP_TextUtilities.FindIntersectingLink(m_legendMesh, Input.mousePosition, Camera.main);
                
                // Clear previous link selection if one existed
                if (linkIndex == -1 && m_activeLinkIndex != -1 || linkIndex != m_activeLinkIndex)
                {
                    m_activeLinkIndex = -1;
                    ParentScript.TargetMeasurementID = Guid.Empty;
                    Container.gameObject.SetActive(false);
                    HideBorder();
                }

                // Handle new Link selection
                if (linkIndex != -1)
                {
                    // Make value display container follow mouse like a tool-tip, offset to the right of the mouse
                    RectTransformUtility.ScreenPointToWorldPointInRectangle(m_legendMesh.rectTransform, Input.mousePosition, Camera.main, out Vector3 worldPointInRectangle);
                    RectTransform containterRectTransform = Container.rectTransform;
                    worldPointInRectangle.x += containterRectTransform.localScale.x * containterRectTransform.rect.width / 1.8F;
                    worldPointInRectangle.y -= containterRectTransform.localScale.y * containterRectTransform.rect.height * 0.3F;
                    Container.transform.position = worldPointInRectangle;

                    if (linkIndex != m_activeLinkIndex)
                    {
                        m_activeLinkIndex = linkIndex;

                        TMP_TextInfo textInfo = m_legendMesh.textInfo;

                        // Highlight active legend row by drawing a border around it
                        ShowBorder(textInfo.lineInfo[linkIndex]);

                        // The the measurement ID of the selected from the embedded "<link>" tag
                        if (Guid.TryParse(textInfo.linkInfo[linkIndex].GetLinkID(), out Guid measurementID))
                        {
                            m_displayValueMesh.text = "<color=#DCDCDC>Waiting for value...";
                            ParentScript.TargetMeasurementID = measurementID;
                            Container.gameObject.SetActive(true);
                        }
                        else
                        {
                            ParentScript.TargetMeasurementID = Guid.Empty;
                        }
                    }
                }
            }
            else
            {
                ParentScript.TargetMeasurementID = Guid.Empty;
                Container.gameObject.SetActive(false);
                HideBorder();
            }
        }

        private void CreateBorder()
        {
            Transform drawTransform = m_legendMesh.transform;

            m_border = new VectorLine("LegendBorderLine", new List<Vector3>(2), ParentScript.LineMaterial, BorderWidth, LineType.Continuous)
            {
                color = BorderColor,
                drawTransform = drawTransform,
                layer = OrderInLayer
            };

            m_border.Draw3DAuto();
        }

        private void DeleteBorder()
        {
            m_border.StopDrawing3DAuto();
            VectorLine.Destroy(ref m_border);
            m_border = null;
        }

        private void ShowBorder(TMP_LineInfo lineInfo)
        {
            Transform legendTransform = m_legendMesh.transform;

            // Get line extents
            Vector3 bottomLeft = legendTransform.TransformPoint(lineInfo.lineExtents.min);
            Vector3 topRight = legendTransform.TransformPoint(lineInfo.lineExtents.max);

            // Add left/right padding to x-axis values
            bottomLeft.x -= 1.05F;
            topRight.x += 0.2F;

            // Adjust y-axis by legend location
            float legendY = legendTransform.position.y + 0.28F;
            bottomLeft.y -= legendY;
            topRight.y -= legendY;

            // Make sure border is drawn above (i.e., at a higher z-coordinate) than legend
            float borderZ = bottomLeft.z - 0.1F;
            bottomLeft.z = borderZ;
            topRight.z = borderZ;

            m_border.MakeRoundedRect(bottomLeft, topRight, 0.35F, 2);
            m_border.active = true;
        }

        private void HideBorder()
        {
            m_border.active = false;
        }
    }
}