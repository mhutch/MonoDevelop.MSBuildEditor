// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace MonoDevelop.MSBuild.Language
{
	public interface ITextSource
	{
		string FileName { get; }
		int Length { get; }
		char GetCharAt (int offset);
		string GetTextBetween (int begin, int end);
		TextReader CreateReader ();
	}
}