//******************************************************************************************************
//  LegendLine.cs - Gbtc
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
    // Creates a fixed 3D line using Vectrosity asset to draw line for legend
    public class LegendLine : ILine
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
}