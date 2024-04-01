// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.SdkResolution;

namespace MonoDevelop.MSBuild.Tests.Editor.Mocks;

// Subclass CurrentProcessMSBuildEnvironment so we get real value for ToolsPath, allowing .tasks and .overridetasks to be found by tests
[Export (typeof (IMSBuildEnvironment))]
[method: ImportingConstructor]
class TestMSBuildEnvironment (MSBuildEnvironmentLogger environmentLogger) : CurrentProcessMSBuildEnvironment(environmentLogger.Logger)
{
	// However, suppress resolution of Microsoft.NET.SDK so tests don't use the SDK fallback.
	// The SDK fallback substantially slows down the tests and is not necessary for the tests,
	// and different versions of the SDK may introduce unexpected values from inference.
	public override SdkInfo ResolveSdk ((string name, string version, string minimumVersion) sdk, string projectFile, string solutionPath, ILogger logger = null)
		=> null;
}
