// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace MonoDevelop.MSBuild.Evaluation
{
	interface IMSBuildEvaluationContext
	{
		bool TryGetProperty (string name, [NotNullWhen (true)]  out MSBuildPropertyValue? value);
	}
}

#if !NETCOREAPP3_0_OR_GREATER

namespace System.Diagnostics.CodeAnalysis
{
	[AttributeUsage (AttributeTargets.Parameter, Inherited = false)]
	public sealed class NotNullWhenAttribute : Attribute
	{
		public NotNullWhenAttribute (bool returnValue)
		{
		}
	}
}

#endif