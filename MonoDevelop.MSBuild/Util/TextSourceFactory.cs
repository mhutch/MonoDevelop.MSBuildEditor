// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Util
{
	static class TextSourceFactory
	{
		public static ITextSource CreateNewDocument (string filename)
			=> new StringTextSource (File.ReadAllText (filename), filename);
		public static ITextSource CreateNewDocument (string content, string filename)
			=> new StringTextSource (content, filename);

		class StringTextSource : ITextSource
		{
			readonly string content;

			public StringTextSource (string content, string filename)
			{
				this.content = content;
				FileName = filename;
			}

			public string FileName { get; set; }
			public int Length => content.Length;
			public TextReader CreateReader () => new StringReader (content);
			public char GetCharAt (int offset) => content[offset];
			public string GetTextBetween (int begin, int end) => content.Substring (begin, end - begin);
		}
	}
}