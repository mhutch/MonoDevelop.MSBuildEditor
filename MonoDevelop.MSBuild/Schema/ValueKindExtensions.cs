// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using MonoDevelop.MSBuild.Language.Expressions;

namespace MonoDevelop.MSBuild.Schema
{
	static class ValueKindExtensions
	{
		public static MSBuildValueKind GetScalarType (this MSBuildValueKind value)
		{
			return value & ~(MSBuildValueKind.List | MSBuildValueKind.Literal | MSBuildValueKind.CommaList);
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

		public static bool IsCustomType (this MSBuildValueKind value) => value.GetScalarType() == MSBuildValueKind.CustomType;

		public static MSBuildValueKind List (this MSBuildValueKind value)
		{
			return value | MSBuildValueKind.List;
		}

		public static MSBuildValueKind Literal (this MSBuildValueKind value)
		{
			return value | MSBuildValueKind.Literal;
		}

		//FIXME: cache these somewhere?
		public static IReadOnlyList<ValueKindValue> GetSimpleValues (this MSBuildValueKind kind, bool includeParseableTypes)
		{
			switch (kind) {
			case MSBuildValueKind.Bool:
				if (!includeParseableTypes) {
					return null;
				}
				return new ValueKindValue [] {
					new ValueKindValue ("True", null, MSBuildValueKind.Bool),
					new ValueKindValue ("False", null, MSBuildValueKind.Bool),
				};
			case MSBuildValueKind.TaskArchitecture:
				return new ValueKindValue [] {
					new ValueKindValue ("*", "Any architecture", MSBuildValueKind.TaskArchitecture),
					new ValueKindValue ("CurrentArchitecture", "The architecture on which MSBuild is running", MSBuildValueKind.TaskArchitecture),
					new ValueKindValue ("x86", "The 32-bit x86 architecture", MSBuildValueKind.TaskArchitecture),
					new ValueKindValue ("x64", "The 64-bit x64 architecture", MSBuildValueKind.TaskArchitecture),
				};
			case MSBuildValueKind.TaskRuntime:
				return new ValueKindValue [] {
					new ValueKindValue ("*", "Any runtime", MSBuildValueKind.TaskRuntime),
					new ValueKindValue ("CurrentRuntime", "The runtime on which MSBuild is running", MSBuildValueKind.TaskRuntime),
					new ValueKindValue ("CLR2", "The .NET 2.0 runtime", MSBuildValueKind.TaskRuntime),
					new ValueKindValue ("CLR4", "The .NET 4.0 runtime", MSBuildValueKind.TaskRuntime),
				};
			case MSBuildValueKind.Importance:
				return new ValueKindValue [] {
					new ValueKindValue ("high", "High importance, only displayed for all log verbosity settings", MSBuildValueKind.Importance),
					new ValueKindValue ("normal", "Normal importance", MSBuildValueKind.Importance),
					new ValueKindValue ("low", "Low importance, only displayed for highly verbose log settings", MSBuildValueKind.Importance)
				};
			case MSBuildValueKind.HostOS:
				return new ValueKindValue [] {
					new ValueKindValue ("Windows_NT", "Running on Windows", MSBuildValueKind.HostOS),
					new ValueKindValue ("Unix", "Running on Unix", MSBuildValueKind.HostOS)
					// deliberately ignoring Mac as this value doesn't work for legacy compat reasons
				};
			case MSBuildValueKind.HostRuntime:
				return new ValueKindValue [] {
					new ValueKindValue ("Mono", "Running on Mono", MSBuildValueKind.HostRuntime),
					new ValueKindValue ("Core", "Running on .NET Core", MSBuildValueKind.HostRuntime),
					new ValueKindValue ("Full", "Running on .NET Framework", MSBuildValueKind.HostRuntime)
				};
			case MSBuildValueKind.ContinueOnError:
				return new ValueKindValue [] {
					new ValueKindValue (
						"WarnAndContinue",
						"When the task outputs errors, convert them to warnings, and continue executing other tasks and targets",
						MSBuildValueKind.ContinueOnError),
					new ValueKindValue (
						"true",
						"Equivalent to `WarnAndContinue`",
						MSBuildValueKind.ContinueOnError),
					new ValueKindValue (
						"ErrorAndContinue",
						"When the task outputs errors, continue executing other tasks and targets",
						MSBuildValueKind.ContinueOnError),
					new ValueKindValue (
						"ErrorAndStop",
						"When the task outputs errors, do not execute further tasks and targets",
						MSBuildValueKind.ContinueOnError),
					new ValueKindValue (
						"true",
						"Equivalent to `ErrorAndStop`",
						MSBuildValueKind.ContinueOnError),

				};
			case MSBuildValueKind.SkipNonexistentProjectsBehavior:
				return new ValueKindValue[] {
					new ValueKindValue ("True", "Skip the project if the project file does not exist", MSBuildValueKind.SkipNonexistentProjectsBehavior),
					new ValueKindValue ("False", "Output an error if the project file does not exist", MSBuildValueKind.SkipNonexistentProjectsBehavior),
					new ValueKindValue ("Build", "Build the project even if the project file does not exist", MSBuildValueKind.SkipNonexistentProjectsBehavior)

				};
			case MSBuildValueKind.ToolsVersion:
				return new ValueKindValue [] {
					new ValueKindValue ("2.0", "MSBuild 2.0, included in .NET Framework 2.0", MSBuildValueKind.ToolsVersion),
					new ValueKindValue ("3.5", "MSBuild 3.5, included in .NET Framework 3.5", MSBuildValueKind.ToolsVersion),
					new ValueKindValue ("4.0", "MSBuild 4.0, included in .NET Framework 4.0", MSBuildValueKind.ToolsVersion),
					new ValueKindValue ("12.0", "MSBuild 12.0, included in Visual Studio 2013", MSBuildValueKind.ToolsVersion),
					new ValueKindValue ("14.0", "MSBuild 14.0, included in Visual Studio 2015", MSBuildValueKind.ToolsVersion),
					new ValueKindValue ("15.0", "MSBuild 15.0, included in Visual Studio 2017", MSBuildValueKind.ToolsVersion),
					new ValueKindValue ("Current", "MSBuild 16.0, included in Visual Studio 2019", MSBuildValueKind.ToolsVersion),
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
