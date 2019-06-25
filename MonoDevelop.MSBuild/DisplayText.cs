// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuild
{
	/// <summary>
	/// Represents texts that can be displated to the user
	/// </summary>
	public struct DisplayText
	{
		public DisplayText (string text, object displayElement = null)
		{
			this.Text = text;
			this.DisplayElement = displayElement;
		}

		/// <summary>
		/// An optional formatted display element for tooltip in the editor. If not provided, one will be created from the text.
		/// </summary>
		public object DisplayElement { get; }
		public string Text { get; }
		public bool IsEmpty => string.IsNullOrEmpty (Text);

		public static implicit operator DisplayText (string s)
		{
			return new DisplayText (s);
		}
	}
}
