// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis.EditorConfig.Parsing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.PooledObjects;
using Microsoft.CodeAnalysis.Diagnostics;

using MonoDevelop.Xml.Options;

namespace MonoDevelop.MSBuild.Editor.Options;

class EditorConfigOptionsAccumulator : IEditorConfigOptionAccumulator<EditorConfigFile<RawEditorConfigOption>, RawEditorConfigOption>
	{
		readonly ArrayBuilder<RawEditorConfigOption> options = ArrayBuilder<RawEditorConfigOption>.GetInstance();

		static readonly HashSet<string> coreProperties = TextFormattingOptions
			.GetAll()
			.Select(t => t.Name)
			.Concat([ "root" ])
			.ToHashSet (AnalyzerConfigOptions.KeyComparer);

		static bool IsPropertyOfInterest (string propertyName)
			=> propertyName.StartsWith ("msbuild_", StringComparison.OrdinalIgnoreCase) ||
				propertyName.StartsWith ("xml_", StringComparison.OrdinalIgnoreCase) ||
				coreProperties.Contains (propertyName);

		bool isRoot = false;

		public void ProcessSection (Section section, IReadOnlyDictionary<string, (string value, TextLine? line)> properties)
		{
			foreach (var property in properties) {
				var propertyName = property.Key.Trim();
				if (string.Equals(propertyName, "root")) {
					isRoot = true;
					continue;
				}
				if (IsPropertyOfInterest (property.Key)) {
					options.Add (new RawEditorConfigOption (section, property.Key, property.Value.value.Trim(), property.Value.line?.Span));
				}
			}
		}

		public EditorConfigFile<RawEditorConfigOption> Complete (string? filePath)
		{
			return new EditorConfigFile<RawEditorConfigOption> (filePath, options.ToImmutableAndFree ());
		}
	}
