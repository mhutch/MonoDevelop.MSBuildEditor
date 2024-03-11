// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System;

namespace MonoDevelop.MSBuild.Analysis
{
	[Flags]
	public enum MSBuildDiagnosticSeverity
	{
		None = 0,

		Suggestion = 1 << 0,
		Warning = 1 << 1,
		Error = 1 << 2,

		All = Suggestion | Warning | Error
	}
}