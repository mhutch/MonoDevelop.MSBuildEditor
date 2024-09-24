// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Xml.Options;

namespace MonoDevelop.MSBuild.Options;

class MSBuildCompletionOptions
{
	/// <summary>
	/// Whether completion should show private symbols from files other than the current file. Private symbols are identified by the convention of prefixing them with an underscore.
	/// </summary>
	public static readonly Option<bool> ShowPrivateSymbols = new ("msbuild_show_private_symbols", true, false);
}
