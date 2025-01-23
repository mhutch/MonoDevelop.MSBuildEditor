// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Build.Shared;
using Microsoft.CodeAnalysis.EditorConfig.Parsing;
using MonoDevelop.MSBuild.Editor.Options;

namespace MonoDevelop.MSBuild.Editor.Options;

partial class EditorConfigOptionsService
{
	Dictionary<string, PotentialEditorConfigFile> editorConfigFileByDirectory = new(
		FileUtilities.PathComparison == StringComparison.OrdinalIgnoreCase
			? StringComparer.OrdinalIgnoreCase
			: StringComparer.Ordinal);

	public EditorConfigOptionsReader GetOptionsForSourceFile(string file)
	{
		var directory = Path.GetDirectoryName(file) ?? throw new ArgumentException("File has no directory", nameof(file));

		// TODO: more fine-grained locking
		lock(editorConfigFileByDirectory) {
			var potentialConfig = GetPotentialEditorConfigWithAddRef(directory);
			return new EditorConfigOptionsReaderImpl(potentialConfig, file);
		}
	}

	/// <summary>
	/// Gets the <see cref="PotentialEditorConfigFile"/> for the specified directory, creating it if necessary.
	/// The returned object will have its reference count incremented and the caller is responsible for releasing it.
	/// </summary>
	/// <param name="directory"></param>
	/// <returns></returns>
	PotentialEditorConfigFile GetPotentialEditorConfigWithAddRef(string directory)
	{
		if (editorConfigFileByDirectory.TryGetValue(directory, out var editorConfig)) {
			editorConfig.AddRef();
			return editorConfig;
		}

		editorConfig = new PotentialEditorConfigFile(this, directory);
		editorConfigFileByDirectory[directory] = editorConfig;

		if (File.Exists (editorConfig.FilePath)) {
			var text = File.ReadAllText(editorConfig.FilePath);

			var accumulator = new EditorConfigOptionsAccumulator ();
			var parsed = EditorConfigParser.Parse<
				EditorConfigFile<RawEditorConfigOption>,
				RawEditorConfigOption,
				EditorConfigOptionsAccumulator> (text, editorConfig.FilePath, accumulator);

			editorConfig.ParsedFile = parsed;
		}

		editorConfig.AddRef();
		return editorConfig;
	}

	static void UpdatePotentialEditorConfig (PotentialEditorConfigFile editorConfig)
	{
		if (!File.Exists (editorConfig.FilePath)) {
			editorConfig.ParsedFile = null;
			return;
		}

		var text = File.ReadAllText(editorConfig.FilePath);

		var accumulator = new EditorConfigOptionsAccumulator ();
		var parsed = EditorConfigParser.Parse<
			EditorConfigFile<RawEditorConfigOption>,
			RawEditorConfigOption,
			EditorConfigOptionsAccumulator> (text, editorConfig.FilePath, accumulator);

		editorConfig.ParsedFile = parsed;
	}

	void UpdateParents(PotentialEditorConfigFile editorConfig)
	{
		for (int i = 0; i < 50; i++) {
			bool isRoot = editorConfig.ParsedFile is {} parsed && parsed.Options.Any(o => o.Section.IsGlobal && o.name == "root" && o.value == "true");

			if (isRoot) {
				if (editorConfig.Parent is not null) {
					editorConfig.Parent.ReleaseRef();
					editorConfig.Parent = null;
				}
				return;
			}

			if (editorConfig.Parent is not null) {
				return;
			}

			if (editorConfig.IsRoot) {
				return;
			}

			// we know this is not null because IsRoot checks whether it is null
			var parentDirectory = Path.GetDirectoryName(editorConfig.Directory)!;

			editorConfig = editorConfig.Parent = GetPotentialEditorConfigWithAddRef(parentDirectory);
		}

		throw new InvalidOperationException("EditorConfig hierarchy is too deep");
	}
}
