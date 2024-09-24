// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable annotations

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MonoDevelop.MSBuild.Tests.Editor.Completion;

[Export (typeof (IMSBuildFileSystem))]
class TestMSBuildFileSystemExport : IMSBuildFileSystem
{
	public bool DirectoryExists (string basePath) => TestMSBuildFileSystem.Instance.DirectoryExists (basePath);
	public IEnumerable<string> GetDirectories (string basePath) => TestMSBuildFileSystem.Instance.GetDirectories (basePath);
	public IEnumerable<string> GetFiles (string basePath) => TestMSBuildFileSystem.Instance.GetFiles (basePath);
}

// FIXME: thread safety when we have multiple tests using this
class TestMSBuildFileSystem : TestDirectoryInfo, IMSBuildFileSystem
{
	TestMSBuildFileSystem () : base (null, null)
	{
	}

	public static TestMSBuildFileSystem Instance { get; } = new ();

	public bool DirectoryExists (string path) => GetDirectory (path) is not null;

	public IEnumerable<string> GetDirectories (string path) => GetDirectory (path) is TestDirectoryInfo info? info.GetDirectories () : Enumerable.Empty<string> ();

	public IEnumerable<string> GetFiles (string path) => GetDirectory (path) is TestDirectoryInfo info ? info.GetFiles () : Enumerable.Empty<string> ();

	public TestDirectoryInfo AddTestDirectory ([CallerMemberName] string? testName = null)
	{
		if (string.IsNullOrEmpty (testName)) {
			throw new ArgumentException ($"'{nameof (testName)}' cannot be null or empty.", nameof (testName));
		}
		return AddDirectory ($"/{testName}");
	}
}

class TestDirectoryInfo
{
	readonly static char[] separators = { '/', '\\' };

	readonly HashSet<string> files = new ();
	internal readonly Dictionary<string, TestDirectoryInfo> Directories = new ();
	readonly string? name;
	readonly TestDirectoryInfo? parent;
	string? path = null;

	public TestDirectoryInfo (string? name, TestDirectoryInfo? parent)
	{
		this.name = name;
		this.parent = parent;
	}

	public string Path => path ??= (name is null || parent is null)? "/" : (parent.Path == "/"? $"{parent.Path}{name}" : $"{parent.Path}/{name}");

	TestDirectoryInfo RootDir {
		get {
			var currentDir = this;
			while (currentDir.parent is not null) {
				currentDir = currentDir.parent;
			}
			return currentDir;
		}
	}

	public string Combine (string name) => $"{Path}/{name}";

	public TestDirectoryInfo? GetDirectory (string path)
	{
		var currentDir = this;
		bool isFirst = true;
		foreach (string segment in path.Split (separators)) {
			if (segment.Length == 0 || (isFirst && segment[segment.Length - 1] == ':')) {
				currentDir = RootDir;
			}
			else if (!currentDir.Directories.TryGetValue (segment, out currentDir)) {
				return null;
			}
			isFirst = false;
		}
		return currentDir;
	}

	public TestDirectoryInfo AddDirectory (string path)
	{
		var currentDir = this;
		bool isFirst = true;
		foreach (string segment in path.Split (separators)) {
			if (segment.Length == 0 || (isFirst && segment[segment.Length - 1] == ':')) {
				currentDir = RootDir;
			} else if (!currentDir.Directories.TryGetValue (segment, out var nextDir)) {
				nextDir = new TestDirectoryInfo (segment, currentDir);
				currentDir.Directories.Add (nextDir.name!, nextDir);
				currentDir = nextDir;
			}
			isFirst = false;
		}
		return currentDir;
	}

	public void AddFiles (params string[] files)
	{
		foreach (string file in files) {
			this.files.Add (file);
		}
	}

	public IEnumerable<string> GetFiles () => files;

	public IEnumerable<string> GetDirectories () => Directories.Keys;
}
