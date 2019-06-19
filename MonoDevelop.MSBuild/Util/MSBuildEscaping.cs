// MSBuildProjectService.cs
//
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
//

using System.Collections.Generic;
using System.Globalization;
using System.IO;

//imported from MonoDevelop.Projects.MSBuild.MSBuildProjectService
namespace MonoDevelop.MSBuild.Util
{
	static class MSBuildEscaping
	{
		static readonly char[] specialCharacters = { '%', '$', '@', '(', ')', '\'', ';', '?' };
		static readonly Dictionary<char, string> specialCharactersEscaped;
		static readonly Dictionary<string, char> specialCharactersUnescaped;

		public static string EscapeString (string str)
		{
			int i = str.IndexOfAny (specialCharacters);
			if (i != -1) {
				var sb = new System.Text.StringBuilder ();
				int start = 0;
				while (i != -1) {
					sb.Append (str, start, i - start);
					sb.Append (specialCharactersEscaped[str[i]]);
					if (i >= str.Length)
						break;
					start = i + 1;
					i = str.IndexOfAny (specialCharacters, start);
				}
				if (start < str.Length)
					sb.Append (str, start, str.Length - start);
				return sb.ToString ();
			}
			return str;
		}

		public static string UnescapePath (string path)
		{
			if (string.IsNullOrEmpty (path))
				return path;

			if (!Platform.IsWindows)
				path = path.Replace ("\\", "/");

			return UnscapeString (path);
		}

		public static string UnscapeString (string str)
		{
			int i = str.IndexOf ('%');
			if (i != -1) {
				var sb = new System.Text.StringBuilder ();
				int start = 0;
				while (i != -1) {
					var sub = str.Substring (i + 1, 2);
					if (specialCharactersUnescaped.TryGetValue (sub, out char ch)) {
						sb.Append (str, start, i - start);
						sb.Append (ch);
					} else if (int.TryParse (sub, NumberStyles.HexNumber, null, out int c)) {
						sb.Append (str, start, i - start);
						sb.Append ((char)c);
					}
					start = i + 3;
					i = str.IndexOf ('%', start);
				}
				sb.Append (str, start, str.Length - start);
				return sb.ToString ();
			}
			return str;
		}

		public static string ToMSBuildPath (string baseDirectory, string absPath, bool normalize = true)
		{
			if (string.IsNullOrEmpty (absPath))
				return absPath;
			if (baseDirectory != null) {
				absPath = FilePathUtils.AbsoluteToRelativePath (baseDirectory, absPath);
				if (normalize)
					absPath = FilePathUtils.NormalizeRelativePath (absPath);
			}
			return EscapeString (absPath).Replace ('/', '\\');
		}

		internal static string ToMSBuildPathRelative (string baseDirectory, string absPath)
		{
			string filePath = ToMSBuildPath (baseDirectory, absPath);
			return FilePathUtils.AbsoluteToRelativePath (baseDirectory, filePath);
		}


		internal static string FromMSBuildPathRelative (string baseDirectory, string relPath)
		{
			string filePath = FromMSBuildPath (baseDirectory, relPath);
			return FilePathUtils.AbsoluteToRelativePath (baseDirectory, filePath);
		}

		public static string FromMSBuildPath (string basePath, string relPath)
		{
			FromMSBuildPath (basePath, relPath, out string res);
			return res;
		}

		internal static bool IsAbsoluteMSBuildPath (string path)
		{
			if (path.Length > 1 && char.IsLetter (path[0]) && path[1] == ':')
				return true;
			if (path.Length > 0 && path[0] == '\\')
				return true;
			return false;
		}

		internal static bool FromMSBuildPath (string basePath, string relPath, out string resultPath)
		{
			resultPath = relPath;

			if (string.IsNullOrEmpty (relPath))
				return false;

			string path = UnescapePath (relPath);

			if (char.IsLetter (path[0]) && path.Length > 1 && path[1] == ':') {
				if (Platform.IsWindows) {
					resultPath = path; // Return the escaped value
					return true;
				} else
					return false;
			}

			bool isRooted = Path.IsPathRooted (path);

			if (!isRooted && basePath != null) {
				path = Path.Combine (basePath, path);
				isRooted = Path.IsPathRooted (path);
			}

			// Return relative paths as-is, we can't do anything else with them
			if (!isRooted) {
				resultPath = FilePathUtils.NormalizeRelativePath (path);
				return true;
			}

			resultPath = FilePathUtils.GetFullPath (path);
			return true;
		}
	}
}