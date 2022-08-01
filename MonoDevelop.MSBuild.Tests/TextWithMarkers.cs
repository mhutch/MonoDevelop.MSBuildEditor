// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Tests
{
	/// <summary>
	/// Represents text with various marked positions.
	/// </summary>
	public class TextWithMarkers
	{
		TextWithMarkers (string text, char[] markerChars, List<int>[] markedPositionsById)
		{
			Text = text;
			this.markerChars = markerChars;
			this.markedPositionsById = markedPositionsById;
		}

		readonly char[] markerChars;
		readonly List<int>[] markedPositionsById;

		/// <summary>
		/// The text with the marker characters removed
		/// </summary>
		public string Text { get; }

		int GetMarkerId (char? markerChar)
		{
			int markerId;
			if (markerChar is null) {
				if (markedPositionsById.Length != 1) {
					throw new ArgumentException ("More than one marker char was used in this document, you must specify which one", nameof (markerChar));
				}
				markerId = 0;
			} else {
				markerId = Array.IndexOf (markerChars, markerChar);
				if (markerId < 0) {
					throw new ArgumentException ($"The character '{markerChar}' was not used as a marker", nameof (markerChar));
				}
			}
			return markerId;
		}

		/// <summary>
		/// Gets all the marked positions for the specified marker character (optional if only one was used).
		/// </summary>
		public IList<int> GetMarkedPositions (char? markerChar = null) => markedPositionsById[GetMarkerId (markerChar)];

		public int GetMarkedPosition (char? markerChar = null)
		{
			var id = GetMarkerId (markerChar);
			var positions = markedPositionsById[id];

			if (positions.Count != 1) {
				throw new ArgumentException ($"Found multiple markers for char '{markerChars[id]}'", nameof (markerChar));
			}

			return positions[0];
		}

		public TextSpan GetMarkedSpan (char? markerChar = null)
		{
			var id = GetMarkerId (markerChar);
			var positions = markedPositionsById[id];

			if (positions.Count != 2) {
				throw new ArgumentException ($"Found {positions.Count} markers for char '{markerChars[id]}', must have exactly 2 to treat as a span", nameof (markerChar));
			}

			int start = positions[0];
			int end = positions[1];

			return TextSpan.FromBounds (start, end);
		}

		public TextSpan[] GetMarkedSpans (char? markerChar = null)
		{
			var id = GetMarkerId (markerChar);
			var markers = markedPositionsById[id];

			if (markers.Count % 2 != 0) {
				throw new ArgumentException ($"Found {markers.Count} markers for char '{markerChars[id]}', must have even number to treat as spans", nameof (markerChar));
			}

			var spans = new TextSpan [markers.Count / 2];

			for (int i = 0; i < spans.Length; i++) {
				int j = i * 2;
				int start = markers[j];
				int end = markers[j + 1];
				spans[i] = TextSpan.FromBounds (start, end);
			}

			return spans;
		}

		public static TextWithMarkers Parse (string textWithMarkers, params char[] markerChars)
		{
			var markerIndices = Array.ConvertAll (markerChars, c => new List<int> ());

			var sb = new StringBuilder (textWithMarkers.Length);

			for (int i = 0; i < textWithMarkers.Length; i++) {
				var c = textWithMarkers[i];
				int markerId = Array.IndexOf (markerChars, c);
				if (markerId > -1) {
					markerIndices[markerId].Add (sb.Length);
				} else {
					sb.Append (c);
				}
			}

			return new (sb.ToString (), markerChars, markerIndices);
		}

		public static TextWithMarkers Parse (string textWithMarkers, char markerChar)
		{
			var markerIndices = new List<int> ();

			var sb = new StringBuilder (textWithMarkers.Length);

			for (int i = 0; i < textWithMarkers.Length; i++) {
				var c = textWithMarkers[i];
				if (c == markerChar) {
					markerIndices.Add (sb.Length);
				} else {
					sb.Append (c);
				}
			}

			return new (sb.ToString (), new[] { markerChar }, new[] { markerIndices });
		}
	}
}
