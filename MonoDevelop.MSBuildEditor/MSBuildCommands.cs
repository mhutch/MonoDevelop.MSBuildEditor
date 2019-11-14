// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Components.Commands;
using MonoDevelop.Core;

namespace MonoDevelop.MSBuildEditor
{
	public enum MSBuildCommands
	{
		ToggleShowPrivateSymbols
	}

	sealed class ToggleShowPrivateSymbolsHandler : CommandHandler
	{
		protected override void Update (CommandInfo info)
		{
			if (MSBuildOptions.ShowPrivateSymbols.Value) {
				info.Text = "Hide Private MSBuild Symbols";
			} else {
				info.Text = "Hide Private MSBuild Symbols";
			}
		}

		protected override void Run ()
		{
			MSBuildOptions.ShowPrivateSymbols.Value = !MSBuildOptions.ShowPrivateSymbols.Value;
		}
	}

	static class MSBuildOptions
	{
		static public ConfigurationProperty<bool> ShowPrivateSymbols { get; }
			= ConfigurationProperty.Create ("MSBuildEditor.ShowPrivateSymbols", false);
	}
}
