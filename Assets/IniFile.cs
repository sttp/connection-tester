//******************************************************************************************************
//  IniFile.cs - Gbtc
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
//  01/14/2012 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

// ReSharper disable CheckNamespace
namespace UnityGSF
{
    public class IniFile
    {
        #region [ Members ]

        // Fields
        private readonly string m_fileName;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> m_iniData;

        #endregion

        #region [ Constructors ]

        public IniFile(string fileName)
        {
            m_fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            m_iniData = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.CurrentCultureIgnoreCase);
            Load();
        }

        #endregion

        #region [ Properties ]

        public string this[string sectionName, string entryName]
        {
            get
            {
                ConcurrentDictionary<string, string> section = m_iniData.GetOrAdd(sectionName, CreateNewSection);
                return section.GetOrAdd(entryName, (string)null);
            }
            set
            {
                ConcurrentDictionary<string, string> section = m_iniData.GetOrAdd(sectionName, CreateNewSection);
                section[entryName] = value;
            }
        }

        public string this[string sectionName, string entryName, string defaultValue]
        {
            get
            {
                ConcurrentDictionary<string, string> section = m_iniData.GetOrAdd(sectionName, CreateNewSection);
                return section.GetOrAdd(entryName, defaultValue);
            }
        }

        public string this[string sectionName, string entryName, object defaultValue]
        {
            get
            {
                ConcurrentDictionary<string, string> section = m_iniData.GetOrAdd(sectionName, CreateNewSection);
                return section.GetOrAdd(entryName, defaultValue?.ToString() ?? string.Empty);
            }
        }

        #endregion

        #region [ Methods ]

        public void Load()
        {
            if (!File.Exists(m_fileName))
                return;

            using (StreamReader reader = new StreamReader(m_fileName))
            {
                ConcurrentDictionary<string, string> section = null;
                string line = reader.ReadLine();

                while (!(line is null))
                {
                    line = RemoveComments(line);

                    if (line.Length > 0)
                    {
                        // Check for new section				
                        int startBracketIndex = line.IndexOf('[');

                        if (startBracketIndex == 0)
                        {
                            int endBracketIndex = line.IndexOf(']');

                            if (endBracketIndex > 1)
                            {
                                string sectionName = line.Substring(startBracketIndex + 1, endBracketIndex - 1);

                                if (!string.IsNullOrWhiteSpace(sectionName))
                                    section = m_iniData.GetOrAdd(sectionName, CreateNewSection);
                            }
                        }

                        if (section is null)
                            throw new InvalidOperationException("INI file did not begin with a [section]");

                        // Check for key/value pair
                        int equalsIndex = line.IndexOf("=", StringComparison.Ordinal);

                        if (equalsIndex > 0)
                        {
                            string key = line.Substring(0, equalsIndex).Trim();

                            if (!string.IsNullOrEmpty(key))
                                section[key] = line.Substring(equalsIndex + 1).Trim();
                        }
                    }

                    line = reader.ReadLine();
                }
            }
        }

        // Saving INI file will strip comments - sorry :-(
        public void Save() => Save(m_fileName);

        public void Save(string fileName)
        {
            if (fileName is null)
                throw new ArgumentNullException(nameof(fileName));

            using (StreamWriter writer = new StreamWriter(fileName))
            {
                foreach (KeyValuePair<string, ConcurrentDictionary<string, string>> section in m_iniData)
                {
                    writer.WriteLine("[{0}]", section.Key);

                    foreach (KeyValuePair<string, string> entry in section.Value)
                        writer.WriteLine("{0} = {1}", entry.Key, entry.Value);

                    writer.WriteLine();
                }
            }
        }

        private static ConcurrentDictionary<string, string> CreateNewSection(string sectionName) => 
            new ConcurrentDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

        #endregion

        #region [ Static ]

        // Static Methods

        // Remove any comments from key value string
        private static string RemoveComments(string keyValue)
        {
            // Remove any trailing comments from key value
            keyValue = keyValue.Trim();

            int commentIndex = keyValue.LastIndexOf(';');

            if (commentIndex > -1)
                keyValue = keyValue.Substring(0, commentIndex).Trim();

            return keyValue;
        }

        #endregion
    }
}