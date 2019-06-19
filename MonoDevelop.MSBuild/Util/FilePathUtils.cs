//
// FileService.cs
//
// Author:
//   Mike Krüger <mkrueger@novell.com>
//   Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (C) 2007 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;

// ported from MonoDevelop.Core.FileService
namespace MonoDevelop.MSBuild.Util
{
	static class FilePathUtils
	{
		delegate bool PathCharsAreEqualDelegate (char a, char b);

		static PathCharsAreEqualDelegate PathCharsAreEqual = Platform.IsWindows || Platform.IsMac ?
			(PathCharsAreEqualDelegate)PathCharsAreEqualCaseInsensitive : PathCharsAreEqualCaseSensitive;

		static bool IsSeparator (char ch)
		{
			return ch == Path.DirectorySeparatorChar || ch == Path.AltDirectorySeparatorChar || ch == Path.VolumeSeparatorChar;
		}

		static char ToOrdinalIgnoreCase (char c)
		{
			return (((uint)c - 'a') <= ((uint)'z' - 'a')) ? (char)(c - 0x20) : c;
		}

		static bool PathCharsAreEqualCaseInsensitive (char a, char b)
		{
			a = ToOrdinalIgnoreCase (a);
			b = ToOrdinalIgnoreCase (b);

			return a == b;
		}

		static bool PathCharsAreEqualCaseSensitive (char a, char b)
		{
			return a == b;
		}

		public unsafe static string AbsoluteToRelativePath (string baseDirectoryPath, string absPath)
		{
			if (!Path.IsPathRooted (absPath) || string.IsNullOrEmpty (baseDirectoryPath))
				return absPath;

			absPath = GetFullPath (absPath);
			baseDirectoryPath = GetFullPath (baseDirectoryPath).TrimEnd (Path.DirectorySeparatorChar);

			fixed (char* bPtr = baseDirectoryPath, aPtr = absPath) {
				var bEnd = bPtr + baseDirectoryPath.Length;
				var aEnd = aPtr + absPath.Length;
				char* lastStartA = aEnd;
				char* lastStartB = bEnd;

				int indx = 0;
				// search common base path
				var a = aPtr;
				var b = bPtr;
				while (a < aEnd) {
					if (!PathCharsAreEqual (*a, *b))
						break;
					if (IsSeparator (*a)) {
						indx++;
						lastStartA = a + 1;
						lastStartB = b;
					}
					a++;
					b++;
					if (b >= bEnd) {
						if (a >= aEnd || IsSeparator (*a)) {
							indx++;
							lastStartA = a + 1;
							lastStartB = b;
						}
						break;
					}
				}
				if (indx == 0)
					return absPath;

				if (lastStartA >= aEnd)
					return ".";

				// handle case a: some/path b: some/path/deeper...
				if (a >= aEnd) {
					if (IsSeparator (*b)) {
						lastStartA = aEnd;
						lastStartB = b;
					}
				}

				// look how many levels to go up into the base path
				int goUpCount = 0;
				while (lastStartB < bEnd) {
					if (IsSeparator (*lastStartB))
						goUpCount++;
					lastStartB++;
				}
				int remainingPathLength = (int)(aEnd - lastStartA);
				int size = 0;
				if (goUpCount > 0)
					size = goUpCount * 2 + goUpCount - 1;
				if (remainingPathLength > 0) {
					if (goUpCount > 0)
						size++;
					size += remainingPathLength;
				}

				var result = new char[size];
				fixed (char* rPtr = result) {
					// go paths up
					var r = rPtr;
					for (int i = 0; i < goUpCount; i++) {
						*(r++) = '.';
						*(r++) = '.';
						if (i != goUpCount - 1 || remainingPathLength > 0) // If there is no remaining path, there is no need for a trailing slash
							*(r++) = Path.DirectorySeparatorChar;
					}
					// copy the remaining absulute path
					while (lastStartA < aEnd)
						*(r++) = *(lastStartA++);
				}
				return new string (result);
			}
		}

		public static string RelativeToAbsolutePath (string baseDirectoryPath, string relPath)
		{
			return Path.GetFullPath (Path.Combine (baseDirectoryPath, relPath));
		}

		static readonly char[] invalidPathChars = Path.GetInvalidPathChars ();
		static readonly char[] invalidFileNameChars = Path.GetInvalidFileNameChars ();

		public static bool IsValidPath (string fileName)
		{
			if (string.IsNullOrWhiteSpace (fileName))
				return false;
			if (fileName.IndexOfAny (invalidPathChars) >= 0)
				return false;
			return true;
		}

		public static bool IsValidFileName (string fileName)
		{
			if (string.IsNullOrWhiteSpace (fileName))
				return false;
			if (fileName.IndexOfAny (invalidFileNameChars) >= 0)
				return false;
			return true;
		}

		public static string GetFullPath (string path)
		{
			if (path == null)
				throw new ArgumentNullException (nameof(path));
			if (!Platform.IsWindows || path.IndexOf ('*') == -1)
				return Path.GetFullPath (path);

			// On Windows, GetFullPath doesn't work if the path contains wildcards.
			path = path.Replace ("*", wildcardMarker);
			path = Path.GetFullPath (path);
			return path.Replace (wildcardMarker, "*");
		}

		static readonly string wildcardMarker = "_" + Guid.NewGuid ().ToString () + "_";

		public static string NormalizeRelativePath (string path)
		{
			if (path.Length == 0)
				return string.Empty;

			int i;
			for (i = 0; i < path.Length; ++i) {
				if (path[i] != Path.DirectorySeparatorChar && path[i] != ' ')
					break;
			}

			var maxLen = path.Length - 1;
			while (i < maxLen) {
				if (path[i] != '.' || path[i + 1] != Path.DirectorySeparatorChar)
					break;

				i += 2;
				while (i < maxLen && path[i] == Path.DirectorySeparatorChar)
					i++;
			}

			int j;
			for (j = maxLen; j > i; --j) {
				if (path[j] != Path.DirectorySeparatorChar) {
					j++;
					break;
				}
			}

			if (j - i == 1 && path[i] == '.')
				return string.Empty;

			return path.Substring (i, j - i);
		}
	}
}