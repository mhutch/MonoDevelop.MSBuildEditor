// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Tests.Helpers;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests;

partial class MSBuildDocumentTest
{
	[OneTimeSetUp]
	public void LoadMSBuild () => MSBuildTestHelpers.RegisterMSBuildAssemblies ();
}
