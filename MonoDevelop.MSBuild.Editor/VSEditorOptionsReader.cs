// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Collections.Generic;

using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Options;
using MonoDevelop.Xml.Options;

namespace MonoDevelop.MSBuild.Editor;

class VSEditorOptionsReader (IEditorOptions editorOptions) : IOptionsReader
{
	static readonly Dictionary<string, string> optionsMap = new () {
		{ TextFormattingOptions.IndentSize.Name, DefaultOptions.IndentSizeOptionName},
		{ TextFormattingOptions.InsertFinalNewline.Name, DefaultOptions.InsertFinalNewLineOptionName },
		// { TextFormattingOptions.MaxLineLength.Name, NO EQUIVALENT },
		{ TextFormattingOptions.NewLine.Name, DefaultOptions.NewLineCharacterOptionName },
		{ TextFormattingOptions.TabSize.Name, DefaultOptions.TabSizeOptionName},
		{ TextFormattingOptions.TrimTrailingWhitespace.Name, DefaultOptions.TrimTrailingWhiteSpaceOptionName },
		{ TextFormattingOptions.ConvertTabsToSpaces.Name, DefaultOptions.ConvertTabsToSpacesOptionName },
		{ MSBuildEditorOptions.ReplicateNewlineCharacter.Name, DefaultOptions.ReplicateNewLineCharacterOptionName },
	};

	public bool TryGetOption<T> (Option<T> option, out T? value)
	{
		if(optionsMap.TryGetValue(option.Name, out var mappedName)) {
			value = editorOptions.GetOptionValue<T> (mappedName);
			return true;
		}
		value = default;
		return false;
	}
}