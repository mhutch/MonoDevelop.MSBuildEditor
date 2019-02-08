// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Schema
{
	static class ValueKindExtensions
	{
		public static MSBuildValueKind GetScalarType (this MSBuildValueKind value)
		{
			return value & ~(MSBuildValueKind.List | MSBuildValueKind.Literal);
		}

		public static bool AllowExpressions (this MSBuildValueKind value)
		{
			return (value & MSBuildValueKind.Literal) == 0;
		}

		public static bool AllowLists (this MSBuildValueKind value)
		{
			return (value & MSBuildValueKind.List) != 0 || value == MSBuildValueKind.Unknown;
		}

		public static bool AllowListsOrCommaLists (this MSBuildValueKind value)
		{
			return (value & MSBuildValueKind.List) != 0
				|| (value & MSBuildValueKind.CommaList) != 0
				|| value == MSBuildValueKind.Unknown;
		}

		public static bool AllowCommaLists (this MSBuildValueKind value)
		{
			return (value & MSBuildValueKind.CommaList) != 0 || value == MSBuildValueKind.Unknown;
		}

		public static MSBuildValueKind List (this MSBuildValueKind value)
		{
			return value | MSBuildValueKind.List;
		}

		public static MSBuildValueKind Literal (this MSBuildValueKind value)
		{
			return value | MSBuildValueKind.Literal;
		}

		//FIXME: cache these somewhere?
		public static IReadOnlyList<ConstantInfo> GetSimpleValues (this MSBuildValueKind kind, bool includeParseableTypes)
		{
			switch (kind) {
			case MSBuildValueKind.Bool:
				if (!includeParseableTypes) {
					return null;
				}
				return new ConstantInfo [] {
					new ConstantInfo ("True", null),
					new ConstantInfo ("False", null),
				};
			case MSBuildValueKind.TaskArchitecture:
				return new ConstantInfo [] {
					new ConstantInfo ("*", "Any architecture"),
					new ConstantInfo ("CurrentArchitecture", "The architecture on which MSBuild is running"),
					new ConstantInfo ("x86", "The 32-bit x86 architecture"),
					new ConstantInfo ("x64", "The 64-bit x64 architecture"),
				};
			case MSBuildValueKind.TaskRuntime:
				return new ConstantInfo [] {
					new ConstantInfo ("*", "Any runtime"),
					new ConstantInfo ("CurrentRuntime", "The runtime on which MSBuild is running"),
					new ConstantInfo ("CLR2", "The .NET 2.0 runtime"),
					new ConstantInfo ("CLR4", "The .NET 4.0 runtime"),
				};
			case MSBuildValueKind.Importance:
				return new ConstantInfo [] {
					new ConstantInfo ("high", "High importance, only displayed for all log verbosity settings"),
					new ConstantInfo ("normal", "Normal importance"),
					new ConstantInfo ("low", "Low importance, only displayed for highly verbose log settings")
				};
			case MSBuildValueKind.HostOS:
				return new ConstantInfo [] {
					new ConstantInfo ("Windows_NT", "Running on Windows"),
					new ConstantInfo ("Unix", "Running on Unix")
					// deliberately ignoring Mac as it doesn't actually work
				};
			case MSBuildValueKind.HostRuntime:
				return new ConstantInfo [] {
					new ConstantInfo ("Mono", "Running on Mono"),
					new ConstantInfo ("Core", "Running on .NET Core"),
					new ConstantInfo ("Full", "Running on .NET Framework")
				};
			case MSBuildValueKind.ContinueOnError:
				return new ConstantInfo [] {
					new ConstantInfo ("WarnAndContinue", "When the task outputs errors, convert them to warnings, and continue executing other tasks and targets"),
					new ConstantInfo ("true", "Equivalent to `WarnAndContinue`"),
					new ConstantInfo ("ErrorAndContinue", "When the task outputs errors, continue executing other tasks and targets"),
					new ConstantInfo ("ErrorAndStop", "When the task outputs errors, do not execute further tasks and targets"),
					new ConstantInfo ("true", "Equivalent to `ErrorAndStop`"),

				};
			case MSBuildValueKind.ToolsVersion:
				return new ConstantInfo [] {
					new ConstantInfo ("2.0", "MSBuild 2.0, included in .NET Framework 2.0"),
					new ConstantInfo ("3.5", "MSBuild 3.5, included in .NET Framework 3.5"),
					new ConstantInfo ("4.0", "MSBuild 4.0, included in .NET Framework 4.0"),
					new ConstantInfo ("12.0", "MSBuild 12.0, included in Visual Studio 2013"),
					new ConstantInfo ("14.0", "MSBuild 14.0, included in Visual Studio 2015"),
					new ConstantInfo ("15.0", "MSBuild 15.0, included in Visual Studio 2017"),
				};
			}
			return null;
		}

		public static ExpressionOptions GetExpressionOptions (this MSBuildValueKind kind)
		{
			var options = ExpressionOptions.Items;

			if (kind.AllowLists ()) {
				options |= ExpressionOptions.Lists;
			}
			if (kind.AllowCommaLists ()) {
				options |= ExpressionOptions.CommaLists;
			}

			//FIXME: need more context to figure out whether to allow metadata. say yes for now.
			options |= ExpressionOptions.Metadata;

			return options;
		}
	}
}
