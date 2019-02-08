// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuild
{
	public struct DisplayText
	{
		public DisplayText (string text, bool isMarkup = false)
		{
			this.Text = text;
			this.IsMarkup = isMarkup;
		}

		public bool IsMarkup { get; }
		public string Text { get; }
		public bool IsEmpty => string.IsNullOrEmpty (Text);

		public static implicit operator DisplayText (string s)
		{
			return new DisplayText (s);
		}

		public string AsMarkup ()
		{
			return IsMarkup ? Text : Markup.EscapeText (Text);
		}

		public string AsText ()
		{
			if (IsMarkup) {
				throw new NotImplementedException ();
			}
			return Text;
		}
	}
}
