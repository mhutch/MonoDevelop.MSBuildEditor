// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;
using System.Linq;
using Microsoft.CodeAnalysis.EditorConfig.Parsing;
using MonoDevelop.MSBuild.Editor.Options;
using MonoDevelop.Xml.Options;

namespace MonoDevelop.MSBuild.Editor.Options;

partial class EditorConfigOptionsService
{
	class EditorConfigOptionsReaderImpl (PotentialEditorConfigFile potentialConfigWithAddRef, string file) : EditorConfigOptionsReader
	{
		readonly PotentialEditorConfigFile potentialConfig = potentialConfigWithAddRef;

		protected override void Dispose (bool disposing)
		{
			potentialConfig.ReleaseRef();
		}

		bool TryGetOption<T> (EditorConfigFile<RawEditorConfigOption> editorConfig, Option<T> option, out T? value)
		{
			// find the last option that matches the file path
			foreach(var o in editorConfig.Options.Reverse()) {
				if(!string.Equals(o.name, option.Name, StringComparison.Ordinal) || !o.Section.SupportsFilePath(file, SectionMatch.Any)) {
					continue;
				}

				if (option.Serializer.TryParse(o.value, out value)) {
					return true;
				}
			}

			value = default;
			return false;
		}

		public override bool TryGetOption<T> (Option<T> option, out T? value) where T : default
		{
			value = default;

			if(!option.IsEditorConfigOption) {
				return false;
			}

			var potentialConfig = this.potentialConfig;
			do {
				if (potentialConfig.ParsedFile is not null) {
					if (TryGetOption(potentialConfig.ParsedFile, option, out value)) {
						return true;
					}
				}

				potentialConfig = potentialConfig.Parent;
			} while (potentialConfig is not null);

			return false;
		}
	}
}
