// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using ProjectFileTools.NuGetSearch.Contracts;

namespace MonoDevelop.MSBuild.Tests.Editor.Mocks;

[Export (typeof (IPackageFeedRegistryProvider))]
[Name ("Test Package Feed Registry Provider")]
internal class TestPackageFeedRegistryProvider : IPackageFeedRegistryProvider
{
	public IReadOnlyList<string> ConfiguredFeeds => Array.Empty<string> ();
}
