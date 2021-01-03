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
        private GraphLines m_parent;
        private VectorLine m_vector;
        private readonly int m_segmentCount;

        public DataLine(GraphLines parent, Guid id, int index)
        {
            ID = id;
            Index = index;
            LinePoints = new Vector3[parent.PointsInLine];
            UnscaledData = new float[LinePoints.Length];

            int vectorCapacity;
            
            if (parent.UseSplineGraph)
            {
                int segmentFactor = parent.SplineSegmentFactor;

                if (segmentFactor < 1)
                    segmentFactor = 1;

                m_segmentCount = LinePoints.Length * segmentFactor;
                vectorCapacity = m_segmentCount + 1;
            }
            else
            {
                vectorCapacity = LinePoints.Length;
            }

            m_vector = new VectorLine($"DataLine{index}", new List<Vector3>(vectorCapacity), parent.LineMaterial, parent.LineWidth, parent.GraphPoints ? LineType.Points : LineType.Continuous)
            {
                color = parent.LineColors[index % parent.LineColors.Length],
                drawTransform = parent.Target
            };

            m_vector.Draw3DAuto();

            float zOffset = -((index + 1) * parent.LineDepthOffset + 0.05F);

            for (int x = 0; x < LinePoints.Length; x++)
            {
                UnscaledData[x] = float.NaN;
                LinePoints[x] = new Vector3(Mathf.Lerp(-5.0F, 5.0F, x / (float)LinePoints.Length), zOffset, 0.0F); // y and z axes intentionally transposed
            }

            m_parent = parent;
        }

        public Guid ID { get; }

        public int Index { get; }

        public Color VectorColor
        {
            get => m_vector.color;
            set => m_vector.color = value;
        }

        public float[] UnscaledData { get; }

        public Vector3[] LinePoints { get; }

        public void Draw()
        {
            if (m_parent.UseSplineGraph)
            {
                m_vector.MakeSpline(LinePoints, m_segmentCount);
            }
            else
            {
                for (int i = 0; i < LinePoints.Length; i++)
                    m_vector.points3[i] = LinePoints[i];
            }
        }

        public void Stop()
        {
            if (m_vector is null)
                return;

            m_vector.StopDrawing3DAuto();
            VectorLine.Destroy(ref m_vector);

            m_parent = null;
        }

        public void UpdateValue(float newValue)
        {
            int x;

            if (m_parent.PointsScrollRight)
            {
                // Move y position of all points to the right by one
                for (x = LinePoints.Length - 1; x > 0; x--)
                    UnscaledData[x] = UnscaledData[x - 1];
            }
            else
            {
                // Move y position of all points to the left by one
                for (x = 0; x < LinePoints.Length - 1; x++)
                    UnscaledData[x] = UnscaledData[x + 1];
            }

            UnscaledData[x] = newValue;
        }
    }
}