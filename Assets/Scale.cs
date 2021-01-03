//******************************************************************************************************
//  Scale.cs - Gbtc
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

using System.Collections.Generic;
using UnityEngine;

// ReSharper disable CheckNamespace
namespace ConnectionTester
{
    public class Scale
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
                // Apply line scaling
                for (int x = 0; x < line.UnscaledData.Length; x++)
                {
                    float unscaledValue = line.UnscaledData[x];

                    if (float.IsNaN(unscaledValue))
                        unscaledValue = MidPoint;

                    line.LinePoints[x].z = -ScaleValue(unscaledValue);
                }

                line.Draw();
            }
        }

        private float ScaleValue(float value) => (value - m_scaleMin) * (m_graphScale * 2.0F) / Range - m_graphScale;

        private float Range => m_scaleMax - m_scaleMin;

        private float MidPoint => m_scaleMin + Range / 2.0F;
    }
}