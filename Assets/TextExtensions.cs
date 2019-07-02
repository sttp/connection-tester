//******************************************************************************************************
//  TextExtensions.cs - Gbtc
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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityEngine;

// Defines extension functions for text objects
// ReSharper disable once CheckNamespace
public static class TextExtensions
{
    /// <summary>
    /// Updates the text of a 3D Text object, even from a non-UI thread.
    /// </summary>
    /// <param name='mesh'>The text mesh to update.</param>
    /// <param name='text'>The new text to apply to the mesh.</param>
    public static void UpdateText(this TextMesh mesh, string text)
    {
        UIThread.Invoke(UpdateText, mesh, text);
    }

    // Text updates must occur on main UI thread
    private static void UpdateText(object[] args)
    {
        if ((object)args == null || args.Length < 2)
            return;

        TextMesh mesh = args[0] as TextMesh;
        string text = args[1] as string;

        if ((object)mesh != null && (object)text != null)
            mesh.text = text;
    }

    
    /// <summary>
    /// Encodes the specified Unicode character in proper Regular Expression format.
    /// </summary>
    /// <param name="item">Unicode character to encode in Regular Expression format.</param>
    /// <returns>Specified Unicode character in proper Regular Expression format.</returns>
    public static string RegexEncode(this char item)
    {
        return "\\u" + Convert.ToUInt16(item).ToString("x").PadLeft(4, '0');
    }

    /// <summary>
    /// Parses key/value pair expressions from a string. Parameter pairs are delimited by <paramref name="keyValueDelimiter"/>
    /// and multiple pairs separated by <paramref name="parameterDelimiter"/>. Supports encapsulated nested expressions.
    /// </summary>
    /// <param name="value">String containing key/value pair expressions to parse.</param>
    /// <param name="parameterDelimiter">Character that delimits one key/value pair from another.</param>
    /// <param name="keyValueDelimiter">Character that delimits key from value.</param>
    /// <param name="startValueDelimiter">Optional character that marks the start of a value such that value could contain other
    /// <paramref name="parameterDelimiter"/> or <paramref name="keyValueDelimiter"/> characters.</param>
    /// <param name="endValueDelimiter">Optional character that marks the end of a value such that value could contain other
    /// <paramref name="parameterDelimiter"/> or <paramref name="keyValueDelimiter"/> characters.</param>
    /// <param name="ignoreDuplicateKeys">Flag determines whether duplicates are ignored. If flag is set to <c>false</c> an
    /// <see cref="ArgumentException"/> will be thrown when all key parameters are not unique.</param>
    /// <returns>Dictionary of key/value pairs.</returns>
    /// <remarks>
    /// <para>
    /// Parses a string containing key/value pair expressions (e.g., "localPort=5001; transportProtocol=UDP; interface=0.0.0.0").
    /// This method treats all "keys" as case-insensitive. Nesting of key/value pair expressions is allowed by encapsulating the
    /// value using the <paramref name="startValueDelimiter"/> and <paramref name="endValueDelimiter"/> values (e.g., 
    /// "dataChannel={Port=-1;Clients=localhost:8800}; commandChannel={Port=8900}; dataFormat=FloatingPoint;"). There must be one
    /// <paramref name="endValueDelimiter"/> for each encountered <paramref name="startValueDelimiter"/> in the value or a
    /// <see cref="FormatException"/> will be thrown. Multiple levels of nesting is supported. If the <paramref name="ignoreDuplicateKeys"/>
    /// flag is set to <c>false</c> an <see cref="ArgumentException"/> will be thrown when all key parameters are not unique. Note
    /// that keys within nested expressions are considered separate key/value pair strings and are not considered when checking
    /// for duplicate keys.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">value is null.</exception>
    /// <exception cref="ArgumentException">All delimiters must be unique -or- all keys must be unique when
    /// <paramref name="ignoreDuplicateKeys"/> is set to <c>false</c>.</exception>
    /// <exception cref="FormatException">Total nested key/value pair expressions are mismatched -or- encountered
    /// <paramref name="endValueDelimiter"/> before <paramref name="startValueDelimiter"/>.</exception>
    [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
    public static Dictionary<string, string> ParseKeyValuePairs(this string value, char parameterDelimiter = ';', char keyValueDelimiter = '=', char startValueDelimiter = '{', char endValueDelimiter = '}', bool ignoreDuplicateKeys = true)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        if (parameterDelimiter == keyValueDelimiter ||
            parameterDelimiter == startValueDelimiter ||
            parameterDelimiter == endValueDelimiter ||
            keyValueDelimiter == startValueDelimiter ||
            keyValueDelimiter == endValueDelimiter ||
            startValueDelimiter == endValueDelimiter)
            throw new ArgumentException("All delimiters must be unique");

        Dictionary<string, string> keyValuePairs = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
        StringBuilder escapedValue = new StringBuilder();
        string escapedParameterDelimiter = parameterDelimiter.RegexEncode();
        string escapedKeyValueDelimiter = keyValueDelimiter.RegexEncode();
        string escapedStartValueDelimiter = startValueDelimiter.RegexEncode();
        string escapedEndValueDelimiter = endValueDelimiter.RegexEncode();
        string backslashDelimiter = '\\'.RegexEncode();
        string[] elements;
        string key, unescapedValue;
        bool valueEscaped = false;
        int delimiterDepth = 0;
        char character;

        // Escape any parameter or key/value delimiters within tagged value sequences
        //      For example, the following string:
        //          "normalKVP=-1; nestedKVP={p1=true; p2=false}")
        //      would be encoded as:
        //          "normalKVP=-1; nestedKVP=p1\\u003dtrue\\u003b p2\\u003dfalse")
        for (int x = 0; x < value.Length; x++)
        {
            character = value[x];

            if (character == startValueDelimiter)
            {
                if (!valueEscaped)
                {
                    valueEscaped = true;
                    continue;   // Don't add tag start delimiter to final value
                }

                // Handle nested delimiters
                delimiterDepth++;
            }

            if (character == endValueDelimiter)
            {
                if (valueEscaped)
                {
                    if (delimiterDepth > 0)
                    {
                        // Handle nested delimiters
                        delimiterDepth--;
                    }
                    else
                    {
                        valueEscaped = false;
                        continue;   // Don't add tag stop delimiter to final value
                    }
                }
                else
                {
                    throw new FormatException($"Failed to parse key/value pairs: invalid delimiter mismatch. Encountered end value delimiter \'{endValueDelimiter}\' before start value delimiter \'{startValueDelimiter}\'.");
                }
            }

            if (valueEscaped)
            {
                // Escape any delimiter characters inside nested key/value pair
                if (character == parameterDelimiter)
                    escapedValue.Append(escapedParameterDelimiter);
                else if (character == keyValueDelimiter)
                    escapedValue.Append(escapedKeyValueDelimiter);
                else if (character == startValueDelimiter)
                    escapedValue.Append(escapedStartValueDelimiter);
                else if (character == endValueDelimiter)
                    escapedValue.Append(escapedEndValueDelimiter);
                else if (character == '\\')
                    escapedValue.Append(backslashDelimiter);
                else
                    escapedValue.Append(character);
            }
            else
            {
                if (character == '\\')
                    escapedValue.Append(backslashDelimiter);
                else
                    escapedValue.Append(character);
            }
        }

        if (delimiterDepth != 0 || valueEscaped)
        {
            // If value is still escaped, tagged expression was not terminated
            if (valueEscaped)
                delimiterDepth = 1;

            throw new FormatException($"Failed to parse key/value pairs: invalid delimiter mismatch. Encountered more {(delimiterDepth > 0 ? "start value delimiters \'" + startValueDelimiter + "\'" : "end value delimiters \'" + endValueDelimiter + "\'")} than {(delimiterDepth < 0 ? "start value delimiters \'" + startValueDelimiter + "\'" : "end value delimiters \'" + endValueDelimiter + "\'")}.");
        }

        // Parse key/value pairs from escaped value
        foreach (string parameter in escapedValue.ToString().Split(parameterDelimiter))
        {
            // Parse out parameter's key/value elements
            elements = parameter.Split(keyValueDelimiter);

            if (elements.Length == 2)
            {
                // Get key expression
                key = elements[0].Trim();

                // Get unescaped value expression
                unescapedValue = elements[1].Trim().
                    Replace(escapedParameterDelimiter, parameterDelimiter.ToString()).
                    Replace(escapedKeyValueDelimiter, keyValueDelimiter.ToString()).
                    Replace(escapedStartValueDelimiter, startValueDelimiter.ToString()).
                    Replace(escapedEndValueDelimiter, endValueDelimiter.ToString()).
                    Replace(backslashDelimiter, "\\");

                // Add key/value pair to dictionary
                if (ignoreDuplicateKeys)
                {
                    // Add or replace key elements with unescaped value
                    keyValuePairs[key] = unescapedValue;
                }
                else
                {
                    // Add key elements with unescaped value throwing an exception for encountered duplicate keys
                    if (keyValuePairs.ContainsKey(key))
                        throw new ArgumentException($"Failed to parse key/value pairs: duplicate key encountered. Key \"{key}\" is not unique within the string: \"{value}\"");

                    keyValuePairs.Add(key, unescapedValue);
                }
            }
        }

        return keyValuePairs;
    }

    /// <summary>
    /// Turns source string into an array of string segments - each with a set maximum width - for parsing or displaying.
    /// </summary>
    /// <param name="value">Input string to break up into segments.</param>
    /// <param name="segmentSize">Maximum size of returned segment.</param>
    /// <returns>Array of string segments as parsed from source string.</returns>
    /// <remarks>Returns a single element array with an empty string if source string is null or empty.</remarks>
    public static string[] GetSegments(this string value, int segmentSize)
    {
        if (segmentSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(segmentSize), "segmentSize must be greater than zero.");

        if (string.IsNullOrEmpty(value))
            return new[] { "" };

        int totalSegments = (int)Math.Ceiling(value.Length / (double)segmentSize);
        string[] segments = new string[totalSegments];

        for (int x = 0; x < segments.Length; x++)
        {
            if (x * segmentSize + segmentSize >= value.Length)
                segments[x] = value.Substring(x * segmentSize);
            else
                segments[x] = value.Substring(x * segmentSize, segmentSize);
        }

        return segments;
    }
}
