//******************************************************************************************************
//  MouseOrbit.cs - Gbtc
//
//  Copyright © 2015, Grid Protection Alliance.  All Rights Reserved.
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
//
//******************************************************************************************************

using System;
using UnityEngine;

// ReSharper disable CheckNamespace
namespace UnityGSF
{
    public class MouseOrbit : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 50.0F;
        public float MaxDistance = 1000.0F;
        public float MinDistance = 0.3F;
        public float XSpeed = 300.0F;
        public float YSpeed = 300.0F;
        public float XAngle;
        public float YAngle;
        public float ZoomRate = 50.0F;
        public int MouseDownFrames = 10;
        public float MinX = -9999.0F;
        public float ArrowSpeed = 0.15F;
        public bool IsActive = true;
        public bool Restore;
        public bool ArrowScrollsTarget = false;

        private float m_xOffset;
        private float m_yOffset;
        private bool m_buttonDown;
        private bool m_rotate;
        private int m_downCount;

        private float m_originalX;
        private float m_originalY;
        private float m_originalDistance;

        public MouseOrbit()
        {
            m_yOffset = 0.0F;
            m_xOffset = 0.0F;
        }

        // Use this for initialization
        protected void Start()
        {
            Vector3 angles = transform.eulerAngles;
            m_originalX = XAngle = angles.x;
            m_originalY = YAngle = angles.y;
            m_originalDistance = Distance;
        }

        private bool TickVariableRestoration(ref float currentValue, float targetValue, float speed)
        {
            if (Math.Abs(currentValue - targetValue) < 0.001F)
                return true;

            float sign = currentValue > targetValue ? -1.0F : 1.0F;

            currentValue += sign * Time.deltaTime * speed;

            if (sign > 0.0F && currentValue > targetValue || sign < 0.0F && currentValue < targetValue)
            {
                currentValue = targetValue;
                return true;
            }

            return false;
        }

        // Update is called once per frame
        protected void Update()
        {
            if (!Target || !IsActive)
                return;

            if (Restore)
            {
                bool xOffsetRestoreComplete = TickVariableRestoration(ref m_xOffset, 0.0F, 300.0F);
                bool yOffsetRestoreComplete = TickVariableRestoration(ref m_yOffset, 0.0F, 300.0F);

                if (xOffsetRestoreComplete && yOffsetRestoreComplete)
                {
                    bool distanceRestoreComplete = TickVariableRestoration(ref Distance, m_originalDistance, 1000.0F);

                    if (distanceRestoreComplete)
                    {
                        XAngle %= 360.0F;
                        YAngle %= 360.0F;

                        bool xRestoreComplete = TickVariableRestoration(ref XAngle, m_originalX, 300.0F);
                        bool yRestoreComplete = TickVariableRestoration(ref YAngle, m_originalY, 300.0F);

                        // Stop restore when all orbital variables have been restored
                        if (xRestoreComplete && yRestoreComplete)
                            Restore = false;
                    }
                }
            }
            else if (Input.touchCount >= 2)
            {
                // Handle pinch gesture
                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                Vector2 curDist = touch0.position - touch1.position;
                Vector2 prevDist = touch0.position - touch0.deltaPosition - (touch1.position - touch1.deltaPosition);

                float delta = (curDist.magnitude - prevDist.magnitude) / (ZoomRate / 5.0F);
                Distance -= delta;
                Restore = false;
            }
            else
            {
                // Mouse button functions also work for touch input on Android
                if (Input.GetMouseButtonDown(0))
                    m_buttonDown = true;
                else if (Input.GetMouseButtonUp(0))
                    m_buttonDown = false;
                   
                // Only start rotation after a few frames - this allows human
                // multi-touch interaction a moment to engage since it's rare that
                // two fingers will actually hit the screen at the exact same time
                if (m_buttonDown)
                {
                    m_downCount++;
                    m_rotate = m_downCount >= MouseDownFrames;
                }
                else
                {
                    m_downCount = 0;
                    m_rotate = false;
                }
                    
                if (m_rotate)
                {
                    if (Input.mousePosition.x > MinX)
                    {
                        XAngle += Input.GetAxis("Mouse X") * XSpeed * Time.deltaTime;
                        YAngle -= Input.GetAxis("Mouse Y") * YSpeed * Time.deltaTime;
                        Restore = false;
                    }
                }

                float scrollWheel = Input.GetAxis("Mouse ScrollWheel");

                if (scrollWheel != 0.0F)
                {
                    Distance += -Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime * ZoomRate * Mathf.Abs(Distance);
                    Restore = false;
                }
            }

            // Validate distance
            if (Distance < MinDistance)
                Distance = MinDistance;
            else if (Distance > MaxDistance)
                Distance = MaxDistance;

            Quaternion rotation = Quaternion.Euler(YAngle, XAngle, 0); // X/Y values swapped for better mouse rotation orientation
            Vector3 distance3 = new Vector3(0.0F, 0.0F, -Distance);
            Vector3 position = rotation * distance3 + Target.position;

            // Handle X/Y camera offset movement based on arrow keys
            if (Input.GetKey(KeyCode.RightArrow))
            {
                m_xOffset += (ArrowScrollsTarget ? -1 : 1) * ArrowSpeed;
                Restore = false;
            }
            else if (Input.GetKey(KeyCode.LeftArrow))
            {
                m_xOffset += (ArrowScrollsTarget ? 1 : -1) * ArrowSpeed;
                Restore = false;
            }

            if (Input.GetKey(KeyCode.UpArrow))
            {
                m_yOffset += (ArrowScrollsTarget ? -1 : 1) * ArrowSpeed;
                Restore = false;
            }
            else if (Input.GetKey(KeyCode.DownArrow))
            {
                m_yOffset += (ArrowScrollsTarget ? 1 : -1) * ArrowSpeed;
                Restore = false;
            }

            position.x += m_xOffset;
            position.y += m_yOffset;

            transform.rotation = rotation;
            transform.position = position;
        }
    }
}
