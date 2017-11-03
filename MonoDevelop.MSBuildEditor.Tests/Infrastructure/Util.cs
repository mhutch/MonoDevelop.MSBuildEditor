// Util.cs
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

using System.IO;
using MonoDevelop.Core;
using MonoDevelop.Core.ProgressMonitoring;

namespace UnitTests
{
	public static class Util
	{
		static string rootDir;
		static int projectId = 1;

		public static string TestsRootDir {
			get {
				if (rootDir == null) {
					rootDir = Path.GetDirectoryName (typeof (Util).Assembly.Location);
				}
				return rootDir;
			}
		}

		public static string TmpDir {
			get { return Path.Combine (TestsRootDir, "tmp"); }
		}

		public static ProgressMonitor GetMonitor ()
		{
			return GetMonitor (true);
		}

		public static ProgressMonitor GetMonitor (bool ignoreLogMessages)
		{
			ConsoleProgressMonitor m = new ConsoleProgressMonitor ();
			m.IgnoreLogMessages = ignoreLogMessages;
			return m;
		}

		public static string GetSampleProject (params string [] projectName)
		{
			string srcDir = Path.Combine (Path.Combine (TestsRootDir, "test-projects"), Path.Combine (projectName));
			string projDir = srcDir;
			srcDir = Path.GetDirectoryName (srcDir);
			string tmpDir = CreateTmpDir (Path.GetFileName (projDir));
			CopyDir (srcDir, tmpDir);
			return Path.Combine (tmpDir, Path.GetFileName (projDir));
		}

		public static string GetSampleProjectPath (params string [] projectName)
		{
			return Path.Combine (Path.Combine (TestsRootDir, "test-projects"), Path.Combine (projectName));
		}

		public static string CreateTmpDir (string hint)
		{
			string tmpDir = Path.Combine (TmpDir, hint + "-" + projectId.ToString ());
			projectId++;

			if (!Directory.Exists (tmpDir))
				Directory.CreateDirectory (tmpDir);
			return tmpDir;
		}

		public static void ClearTmpDir ()
		{
			if (Directory.Exists (TmpDir))
				Directory.Delete (TmpDir, true);
			projectId = 1;
		}

		public static string ToWindowsEndings (string s)
		{
			return s.Replace ("\r\n", "\n").Replace ("\n", "\r\n");
		}

		public static string ToSystemEndings (string s)
		{
			if (!Platform.IsWindows)
				return s.Replace ("\r\n", "\n");
			else
				return s;
		}

		public static string ReadAllWithWindowsEndings (string fileName)
		{
			return File.ReadAllText (fileName).Replace ("\r\n", "\n").Replace ("\n", "\r\n");
		}

		static void CopyDir (string src, string dst)
		{
			if (Path.GetFileName (src) == ".svn")
				return;

			if (!Directory.Exists (dst))
				Directory.CreateDirectory (dst);

			foreach (string file in Directory.GetFiles (src))
				File.Copy (file, Path.Combine (dst, Path.GetFileName (file)));

			foreach (string dir in Directory.GetDirectories (src))
				CopyDir (dir, Path.Combine (dst, Path.GetFileName (dir)));
		}
	}
}