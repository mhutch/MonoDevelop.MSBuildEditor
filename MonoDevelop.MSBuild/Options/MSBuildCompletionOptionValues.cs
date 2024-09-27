// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Xml.Options;

namespace MonoDevelop.MSBuild.Options;

/// <summary>
/// Provides completion handlers with with pre-resolved information about the MSBuild and XML completion settings
/// </summary>
class MSBuildCompletionOptionValues (IOptionsReader options) : XmlCompletionOptionValues (options)
{
	public bool ShowPrivateSymbols { get; } = options.GetOption (MSBuildCompletionOptions.ShowPrivateSymbols);
}