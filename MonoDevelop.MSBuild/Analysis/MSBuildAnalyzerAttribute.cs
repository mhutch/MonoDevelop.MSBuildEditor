// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuild.Analysis
{
	[AttributeUsage (AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class MSBuildAnalyzerAttribute : Attribute
	{
	}
}
