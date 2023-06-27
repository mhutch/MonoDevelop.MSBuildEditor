// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Community.VisualStudio.Toolkit;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Options;

public class MSBuildTelemetryOptions : BaseOptionModel<MSBuildTelemetryOptions>
{
	public bool IsEnabled { get; set; } = true;
}
