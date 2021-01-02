//******************************************************************************************************
//  MetadataFormatProvider.cs - Gbtc
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
using sttp;

// ReSharper disable CheckNamespace
namespace ConnectionTester
{
    // Exposes Metadata record in a string.Format expression
    public class MetadataFormatProvider : IFormattable
    {
        private readonly MeasurementMetadata m_metadata;
        private readonly string m_signalTypeAcronym;

        public MetadataFormatProvider(MeasurementMetadata metadata, string signalTypeAcronym)
        {
            m_metadata = metadata;
            m_signalTypeAcronym = signalTypeAcronym;
        }

        public string ToString(string propertyName, IFormatProvider provider)
        {
            if (propertyName.Equals("SignalTypeAcronym", StringComparison.OrdinalIgnoreCase))
                return m_signalTypeAcronym;

            return typeof(MeasurementMetadata).GetProperty(propertyName)?.GetValue(m_metadata).ToString() ?? $"<{propertyName}>";
        }
    }
}