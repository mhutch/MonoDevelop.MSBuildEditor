// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

namespace MonoDevelop.MSBuild.Tests.Editor.Mocks
{
	[Export (typeof (IMSBuildEnvironment))]
	class TestMSBuildEnvironment : NullMSBuildEnvironment
	{
	}
}
