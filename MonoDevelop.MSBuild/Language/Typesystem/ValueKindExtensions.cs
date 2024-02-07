// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

using MonoDevelop.MSBuild.Language.Expressions;

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	static class ValueKindExtensions
	{
		/// <summary>
		/// Returns the type without any modifier flags (i.e. list, literal)
		/// </summary>
		public static MSBuildValueKind WithoutModifiers (this MSBuildValueKind kind) => kind & ~MSBuildValueKind.AllModifiers;

		/// <summary>
		/// Determine whether the type has any modifier flags (i.e. list, literal)
		/// </summary>
		public static bool HasModifiers (this MSBuildValueKind kind) => (kind | MSBuildValueKind.AllModifiers) != 0;

		/// <summary>
		/// Checks whether the value kind is a specific value kind or list of that value kind.
		/// </summary>
		public static bool IsKindOrListOfKind (this MSBuildValueKind kind, MSBuildValueKind compareTo) => kind.WithoutModifiers () == compareTo.WithoutModifiers ();

		/// <summary>
		/// Check whether the value allows expressions, i.e. the absence of the Literal modifier
		/// </summary>
		public static bool AllowsExpressions (this MSBuildValueKind kind) => (kind & MSBuildValueKind.Literal) == 0;

		/// <summary>
		/// Whether the type permits lists, i.e. whether it has a list modifier flag or is an unknown type. By default
		/// it only respects <see cref="MSBuildValueKind.ListSemicolon"/> but this can be overridden with <paramref name="listKind"/>.
		/// </summary>
		/// <param name="listKind">
		/// Which list modifiers to respect. Ignores bits other than <see cref="MSBuildValueKind.ListSemicolon"/>,
		/// <see cref="MSBuildValueKind.ListComma"/> or <see cref="MSBuildValueKind.ListSemicolonOrComma"/>.
		/// </param>
		public static bool AllowsLists (this MSBuildValueKind kind, MSBuildValueKind listKind = MSBuildValueKind.ListSemicolon) => (kind & listKind & MSBuildValueKind.ListSemicolonOrComma) != 0 || kind.IsUnknown ();

		public static bool IsUnknown (this MSBuildValueKind kind) => kind.WithoutModifiers () == MSBuildValueKind.Unknown;

		/// <summary>
		/// Whether the type is a custom type, regardless of modifiers
		/// </summary>
		public static bool IsCustomType (this MSBuildValueKind kind) => kind.WithoutModifiers () == MSBuildValueKind.CustomType;

		/// <summary>
		/// Return the type with a list modifier(s) applied. By default it adds only <see cref="MSBuildValueKind.ListSemicolon"/> but this can be overridden with <paramref name="listKind"/>.
		/// </summary>
		/// <param name="listKind">
		/// Which list modifiers to add. Ignores bits other than <see cref="MSBuildValueKind.ListSemicolon"/>,
		/// <see cref="MSBuildValueKind.ListComma"/> or <see cref="MSBuildValueKind.ListSemicolonOrComma"/>.
		/// </param>
		public static MSBuildValueKind AsList (this MSBuildValueKind kind, MSBuildValueKind listKind = MSBuildValueKind.ListSemicolon) => kind | (listKind & MSBuildValueKind.ListSemicolonOrComma);

		/// <summary>
		/// Return the type with the <see cref="MSBuildValueKind.Literal"/> modifier applied.
		/// </summary>
		public static MSBuildValueKind AsLiteral (this MSBuildValueKind value) => value | MSBuildValueKind.Literal;

		//FIXME: cache these somewhere?
		public static IReadOnlyList<ConstantSymbol> GetSimpleValues (this MSBuildValueKind kind)
		{
			switch (kind) {
			case MSBuildValueKind.Bool:
				return [
					new ("True", null, MSBuildValueKind.Bool),
					new ("False", null, MSBuildValueKind.Bool),
				];
			case MSBuildValueKind.TaskArchitecture:
				return [
					new ("*", "Any architecture", MSBuildValueKind.TaskArchitecture),
					new ("CurrentArchitecture", "The architecture on which MSBuild is running", MSBuildValueKind.TaskArchitecture),
					new ("x86", "The 32-bit x86 architecture", MSBuildValueKind.TaskArchitecture),
					new ("x64", "The 64-bit x64 architecture", MSBuildValueKind.TaskArchitecture),
				];
			case MSBuildValueKind.TaskRuntime:
				return [
					new ("*", "Any runtime", MSBuildValueKind.TaskRuntime),
					new ("CurrentRuntime", "The runtime on which MSBuild is running", MSBuildValueKind.TaskRuntime),
					new ("CLR2", "The .NET 2.0 runtime", MSBuildValueKind.TaskRuntime),
					new ("CLR4", "The .NET 4.0 runtime", MSBuildValueKind.TaskRuntime),
				];
			case MSBuildValueKind.Importance:
				return [
					new ("high", "High importance, only displayed for all log verbosity settings", MSBuildValueKind.Importance),
					new ("normal", "Normal importance", MSBuildValueKind.Importance),
					new ("low", "Low importance, only displayed for highly verbose log settings", MSBuildValueKind.Importance)
				];
			case MSBuildValueKind.HostOS:
				return [
					new ("Windows_NT", "Running on Windows", MSBuildValueKind.HostOS),
					new ("Unix", "Running on Unix", MSBuildValueKind.HostOS)
					// deliberately ignoring Mac as this value doesn't work for legacy compat reasons
				];
			case MSBuildValueKind.HostRuntime:
				return [
					new ("Mono", "Running on Mono", MSBuildValueKind.HostRuntime),
					new ("Core", "Running on .NET Core", MSBuildValueKind.HostRuntime),
					new ("Full", "Running on .NET Framework", MSBuildValueKind.HostRuntime)
				];
			case MSBuildValueKind.ContinueOnError:
				return [
					new (
						"WarnAndContinue",
						"When the task outputs errors, convert them to warnings, and continue executing other tasks and targets",
						MSBuildValueKind.ContinueOnError),
					new (
						"true",
						"Equivalent to `WarnAndContinue`",
						MSBuildValueKind.ContinueOnError),
					new (
						"ErrorAndContinue",
						"When the task outputs errors, continue executing other tasks and targets",
						MSBuildValueKind.ContinueOnError),
					new (
						"ErrorAndStop",
						"When the task outputs errors, do not execute further tasks and targets",
						MSBuildValueKind.ContinueOnError),
					new (
						"false",
						"Equivalent to `ErrorAndStop`",
						MSBuildValueKind.ContinueOnError),

				];
			case MSBuildValueKind.SkipNonexistentProjectsBehavior:
				return [
					new ("True", "Skip the project if the project file does not exist", MSBuildValueKind.SkipNonexistentProjectsBehavior),
					new ("False", "Output an error if the project file does not exist", MSBuildValueKind.SkipNonexistentProjectsBehavior),
					new ("Build", "Build the project even if the project file does not exist", MSBuildValueKind.SkipNonexistentProjectsBehavior)

				];
			case MSBuildValueKind.ToolsVersion:
				return [
					new ("2.0", "MSBuild 2.0, included in .NET Framework 2.0", MSBuildValueKind.ToolsVersion),
					new ("3.5", "MSBuild 3.5, included in .NET Framework 3.5", MSBuildValueKind.ToolsVersion),
					new ("4.0", "MSBuild 4.0, included in .NET Framework 4.0", MSBuildValueKind.ToolsVersion),
					new ("12.0", "MSBuild 12.0, included in Visual Studio 2013", MSBuildValueKind.ToolsVersion),
					new ("14.0", "MSBuild 14.0, included in Visual Studio 2015", MSBuildValueKind.ToolsVersion),
					new ("15.0", "MSBuild 15.0, included in Visual Studio 2017", MSBuildValueKind.ToolsVersion),
					new ("Current", "MSBuild 16.0 and later, included in Visual Studio 2019 and later", MSBuildValueKind.ToolsVersion),
				];
			}
			return [];
		}

		public static ExpressionOptions GetExpressionOptions (this MSBuildValueKind kind)
		{
			var options = ExpressionOptions.Items;

			if (kind.AllowsLists (MSBuildValueKind.ListSemicolon)) {
				options |= ExpressionOptions.Lists;
			}

			if (kind.AllowsLists (MSBuildValueKind.ListComma)) {
				options |= ExpressionOptions.CommaLists;
			}

			//FIXME: need more context to figure out whether to allow metadata. say yes for now.
			options |= ExpressionOptions.Metadata;

			return options;
		}

		public static MSBuildValueKind FromFullTypeName (string fullTypeName)
		{
			switch (fullTypeName) {
			case "System.String":
				return MSBuildValueKind.String;
			case "System.Boolean":
				return MSBuildValueKind.Bool;
			case "System.Int32":
			case "System.UInt32":
			case "System.Int62":
			case "System.UInt64":
				return MSBuildValueKind.Int;
			case "System.Single":
			case "System.Double":
				return MSBuildValueKind.Float;
			case "Microsoft.Build.Framework.ITaskItem":
				return MSBuildValueKind.UnknownItem;
			}


			return MSBuildValueKind.Unknown;
		}
	}
}
