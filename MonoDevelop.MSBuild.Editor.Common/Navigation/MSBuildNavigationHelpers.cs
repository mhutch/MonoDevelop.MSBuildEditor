// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable annotations

using System;

using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.Xml.Logging;
using MonoDevelop.Xml.Parser;

namespace MonoDevelop.MSBuild.Editor.Navigation;

static partial class MSBuildNavigationHelpers
{
	public static string GetFindReferencesSearchTitle (MSBuildResolveResult reference, ILogger logger)
	{
		string referenceName = reference.GetReferenceDisplayName ();

		return reference.ReferenceKind switch {
			MSBuildReferenceKind.Item => $"Item '{referenceName}' references",
			MSBuildReferenceKind.Property => $"Property '{referenceName}' references",
			MSBuildReferenceKind.Metadata => $"Metadata '{referenceName}' references",
			MSBuildReferenceKind.Task => $"Task '{referenceName}' references",
			MSBuildReferenceKind.TaskParameter => $"Task parameter '{referenceName}' references",
			MSBuildReferenceKind.Keyword => $"Keyword '{referenceName}' references",
			MSBuildReferenceKind.Target => $"Target '{referenceName}' references",
			MSBuildReferenceKind.KnownValue => $"Value '{referenceName}' references",
			MSBuildReferenceKind.NuGetID => $"NuGet package '{referenceName}' references",
			MSBuildReferenceKind.TargetFramework => $"Target framework '{referenceName}' references",
			MSBuildReferenceKind.ItemFunction => $"Item function '{referenceName}' references",
			MSBuildReferenceKind.PropertyFunction => $"Property function '{referenceName}' references",
			MSBuildReferenceKind.StaticPropertyFunction => $"Static '{referenceName}' references",
			MSBuildReferenceKind.ClassName => $"Class '{referenceName}' references",
			MSBuildReferenceKind.Enum => $"Enum '{referenceName}' references",
			MSBuildReferenceKind.ConditionFunction => $"Condition function '{referenceName}' references",
			MSBuildReferenceKind.FileOrFolder => $"Path '{referenceName}' references",
			MSBuildReferenceKind.TargetFrameworkIdentifier => $"TargetFrameworkIdentifier '{referenceName}' references",
			MSBuildReferenceKind.TargetFrameworkVersion => $"TargetFrameworkVersion '{referenceName}' references",
			MSBuildReferenceKind.TargetFrameworkProfile => $"TargetFrameworkProfile '{referenceName}' references",
			_ => logger.LogUnhandledCaseAndReturnDefaultValue ($"'{referenceName}' references", reference.ReferenceKind)
		};
	}

	public static string GetFindTargetDefinitionsSearchTitle (string targetName) => $"Target '{targetName}' definitions";

	public static string GetFindPropertyWritesSearchTitle (string propertyName) => $"Property '{propertyName}' writes";

	public static string GetFindItemWritesSearchTitle (string itemName) => $"Item '{itemName}' writes";

	public static bool FilterUsageWrites (FindReferencesResult result) => result.Usage switch {
		ReferenceUsage.Declaration or ReferenceUsage.Write => true,
		_ => false
	};

	[LoggerMessage (EventId = 0, Level = LogLevel.Warning, Message = "Error searching for references in MSBuild file '{filename}'")]
	public static partial void LogErrorSearchingFile (ILogger logger, Exception ex, UserIdentifiableFileName filename);

	[LoggerMessage (EventId = 1, Level = LogLevel.Error, Message = "Error getting text for file '{filename}'")]
	public static partial void LogErrorGettingFileText (ILogger logger, Exception ex, UserIdentifiableFileName filename);
}

delegate MSBuildReferenceCollector MSBuildReferenceCollectorFactory (MSBuildDocument doc, ITextSource textSource, ILogger logger, FindReferencesReporter reportResult);