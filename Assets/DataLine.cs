//******************************************************************************************************
//  DataLine.cs - Gbtc
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
using UnityEngine;
using Vectrosity;

// ReSharper disable CheckNamespace
namespace ConnectionTester
{
    // Creates a dynamically scaled 3D line using Vectrosity asset to draw line for data
    public class DataLine : ILine
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
}