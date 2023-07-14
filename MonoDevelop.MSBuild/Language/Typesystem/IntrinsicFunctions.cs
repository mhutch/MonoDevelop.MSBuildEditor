// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language.Typesystem;

static class IntrinsicFunctions
{
	static FunctionInfo FInfo (string name, MSBuildValueKind returnType, string documentation, params FunctionParameterInfo[] args)
		=> new (name, documentation, returnType, args);
	static FunctionParameterInfo FArg (string name, MSBuildValueKind kind, string documentation)
		=> new (name, documentation, kind);

	/// <summary>
	/// Returns the set of intrinsic item functions, with any overloads already collapsed.
	/// </summary>
	public static ICollection<FunctionInfo> GetIntrinsicItemFunctions () => intrinsicItemFunctions ??= CreateIntrinsicItemFunctions ();

	static ICollection<FunctionInfo> intrinsicItemFunctions;

	static FunctionInfo[] CreateIntrinsicItemFunctions () => new[] {
		FInfo ("Count", MSBuildValueKind.Int, "Counts the number of items."),
		FInfo ("DirectoryName", MSBuildValueKind.String, "Transforms each item into its directory name."),
		FInfo ("Metadata", MSBuildValueKind.String, "Returns the values of the specified metadata.",
			FArg ("name", MSBuildValueKind.MetadataName, "Name of the metadata")
		),
		FInfo ("DistinctWithCase", MSBuildValueKind.ItemName.AsList(), "Returns the items with distinct ItemSpecs, respecting case but ignoring metadata."),
		FInfo ("Distinct", MSBuildValueKind.ItemName.AsList(),  "Returns the items with distinct ItemSpecs, ignoring case and metadata."),
		FInfo ("Reverse", MSBuildValueKind.MatchItem.AsList(), "Reverses the list."),
		FInfo ("ClearMetadata", MSBuildValueKind.MatchItem.AsList(), "Returns the items with their metadata cleared."),
		FInfo ("HasMetadata", MSBuildValueKind.MatchItem.AsList(),  "Returns the items that have non-empty values for the specified metadata.",
			FArg ("name", MSBuildValueKind.MetadataName, "Name of the metadata")
		),
		FInfo ("WithMetadataValue", MSBuildValueKind.MatchItem.AsList (), "Returns items that have the specified metadata value, ignoring case.",
			FArg ("name", MSBuildValueKind.MetadataName, "Name of the metadata"),
			FArg ("value", MSBuildValueKind.String, "Value of the metadata")
		),
		FInfo ("AnyHaveMetadataValue", MSBuildValueKind.Bool, "Returns true if any item has the specified metadata name and value, ignoring case.",
			FArg ("name", MSBuildValueKind.MetadataName, "Name of the metadata"),
			FArg ("value", MSBuildValueKind.String, "Value of the metadata")
		),
	};

	/// <summary>
	/// Returns the set of intrinsic property functions, with any overloads already collapsed.
	/// </summary>
	public static ICollection<FunctionInfo> GetIntrinsicPropertyFunctions () => intrinsicPropertyFunctions ??= CreateIntrinsicPropertyFunctions ().CollapseOverloads ();

	static ICollection<FunctionInfo> intrinsicPropertyFunctions;

	static FunctionInfo[] CreateIntrinsicPropertyFunctions () => new[] {
		//these are all really doubles and longs but MSBuildValueKind doesn't make a distinction
		// math functions
		FInfo ("Add", MSBuildValueKind.Float, "Add two doubles",
			FArg ("a", MSBuildValueKind.Float, "First operand"),
			FArg ("b", MSBuildValueKind.Float, "Second operand")
		),
		FInfo ("Add", MSBuildValueKind.Int, "Add two longs",
			FArg ("a", MSBuildValueKind.Int, "First operand"),
			FArg ("b", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("Subtract", MSBuildValueKind.Float, "Subtract two doubles",
			FArg ("a", MSBuildValueKind.Float, "First operand"),
			FArg ("b", MSBuildValueKind.Float, "Second operand")
		),
		FInfo ("Subtract", MSBuildValueKind.Int, "Subtract two longs",
			FArg ("a", MSBuildValueKind.Int, "First operand"),
			FArg ("b", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("Multiply", MSBuildValueKind.Float, "Multiply two doubles",
			FArg ("a", MSBuildValueKind.Float, "First operand"),
			FArg ("b", MSBuildValueKind.Float, "Second operand")
		),
		FInfo ("Multiply", MSBuildValueKind.Int, "Multiply two longs",
			FArg ("a", MSBuildValueKind.Int, "First operand"),
			FArg ("b", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("Divide", MSBuildValueKind.Float, "Divide two doubles",
			FArg ("a", MSBuildValueKind.Float, "First operand"),
			FArg ("b", MSBuildValueKind.Float, "Second operand")
		),
		FInfo ("Divide", MSBuildValueKind.Int, "Divide two longs",
			FArg ("a", MSBuildValueKind.Int, "First operand"),
			FArg ("b", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("Modulo", MSBuildValueKind.Float, "Modulo two doubles",
			FArg ("a", MSBuildValueKind.Float, "First operand"),
			FArg ("b", MSBuildValueKind.Float, "Second operand")
		),
		FInfo ("Modulo", MSBuildValueKind.Int, "Modulo two longs",
			FArg ("a", MSBuildValueKind.Int, "First operand"),
			FArg ("b", MSBuildValueKind.Int, "Second operand")
		),

		//escaping
		FInfo ("Escape", MSBuildValueKind.String, "Escape the string according to MSBuild's escaping rules",
			FArg ("unescaped", MSBuildValueKind.String, "The unescaped string")
		),
		FInfo ("Unescape", MSBuildValueKind.String, "Unescape the string according to MSBuild's escaping rules",
			FArg ("escaped", MSBuildValueKind.String, "The escaped string")
		),

		// bitwise ops
		FInfo ("BitwiseOr", MSBuildValueKind.Int, "Perform a bitwise OR on the first and second (first | second)",
			FArg ("a", MSBuildValueKind.Int, "First operand"),
			FArg ("b", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("BitwiseAnd", MSBuildValueKind.Int, "Perform a bitwise AND on the first and second (first & second)",
			FArg ("a", MSBuildValueKind.Int, "First operand"),
			FArg ("b", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("BitwiseXor", MSBuildValueKind.Int, "Perform a bitwise XOR on the first and second (first ^ second)",
			FArg ("a", MSBuildValueKind.Int, "First operand"),
			FArg ("b", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("BitwiseNot", MSBuildValueKind.Int, "Perform a bitwise NOT on the first (~first)",
			FArg ("a", MSBuildValueKind.Int, "First operand")
		),

		//registry
		FInfo ("GetRegistryValue", MSBuildValueKind.Object, "Get the value of the registry key and value, default value is null",
			FArg ("keyName", MSBuildValueKind.String, "The key name"),
			FArg ("valueName", MSBuildValueKind.String, "The value name")
		),
		FInfo ("GetRegistryValue", MSBuildValueKind.Object, "Get the value of the registry key and value",
			FArg ("keyName", MSBuildValueKind.String, "The key name"),
			FArg ("valueName", MSBuildValueKind.String, "The value name"),
			FArg ("defaultValue", MSBuildValueKind.Object, "The default value")
		),
		FInfo ("GetRegistryValueFromView", MSBuildValueKind.Object, "Get the value of the registry key from one of the RegistryViews specified",
			FArg ("keyName", MSBuildValueKind.String, "The key name"),
			FArg ("valueName", MSBuildValueKind.String, "The value name"),
			FArg ("defaultValue", MSBuildValueKind.Object, "The default value"),
			//todo params, registryView enum
			FArg ("views", MSBuildValueKind.Object.AsList(), "Which registry view(s) to use")
		),

		// path manipulation
		FInfo ("MakeRelative", MSBuildValueKind.String, "Converts a file path to be relative to the specified base path.",
			FArg ("basePath", MSBuildValueKind.String, "The base path"),
			FArg ("path", MSBuildValueKind.String, "The path to convert")
		),
		FInfo ("GetDirectoryNameOfFileAbove", MSBuildValueKind.String, "Searches upward for a directory containing the specified file, beginning in the specified directory.",
			FArg ("startingDirectory", MSBuildValueKind.String, "The starting directory"),
			FArg ("fileName", MSBuildValueKind.String, "The filename for which to search")
		),
		FInfo ("GetPathOfFileAbove", MSBuildValueKind.String, "Searches upward for the specified file, beginning in the specified directory.",
			//yes, GetPathOfFileAbove and GetDirectoryNameOfFileAbove have reversed args
			FArg ("file", MSBuildValueKind.String, "The filename for which to search"),
			FArg ("startingDirectory", MSBuildValueKind.String, "The starting directory")
		),

		// other

		FInfo ("ValueOrDefault", MSBuildValueKind.String, "Return the string in parameter 'defaultValue' only if parameter 'conditionValue' is empty, else, return the value conditionValue",
			FArg ("conditionValue", MSBuildValueKind.String, "The condition"),
			FArg ("defaultValue", MSBuildValueKind.String, "The default value")
		),
		FInfo ("DoesTaskHostExist", MSBuildValueKind.Bool, "Returns true if a task host exists that can service the requested runtime and architecture",
			//FIXME type these more strongly for intellisense
			FArg ("runtime", MSBuildValueKind.String, "The runtime"),
			FArg ("architecture", MSBuildValueKind.String, "The architecture")
		),
		FInfo ("EnsureTrailingSlash", MSBuildValueKind.String, "If the given path doesn't have a trailing slash then add one. If empty, leave it empty.",
			FArg ("path", MSBuildValueKind.String, "The path")
		),

		FInfo ("NormalizeDirectory", MSBuildValueKind.String, "Gets the canonical full path of the provided directory, with correct directory separators for the current OS and a trailing slash.",
			//FIXME params
			FArg ("path", MSBuildValueKind.String.AsList(), "The path components")
		),
		FInfo ("NormalizePath", MSBuildValueKind.String, "Gets the canonical full path of the provided path, with correct directory separators for the current OS.",
			FArg ("path", MSBuildValueKind.String.AsList (), "The path components")
		),
		FInfo ("IsOSPlatform", MSBuildValueKind.Bool, "Whether the current OS platform is the specified OSPlatform value. Case insensitive.",
			//FIXME stronger typing
			FArg ("platformString", MSBuildValueKind.String, "The OSPlatform value")
		),
		FInfo ("IsOsUnixLike", MSBuildValueKind.Bool, "True if current OS is a Unix system."),
		FInfo ("IsOsBsdLike", MSBuildValueKind.Bool, "True if current OS is a BSD system."),
		FInfo ("GetCurrentToolsDirectory", MSBuildValueKind.String, "Gets the path of the current tools directory"),
		FInfo ("GetToolsDirectory32", MSBuildValueKind.String, "Gets the path of the 32-bit tools directory"),
		FInfo ("GetToolsDirectory64", MSBuildValueKind.String, "Gets the path of the 64-bit tools directory"),
		FInfo ("GetMSBuildSDKsPath", MSBuildValueKind.String,  "Gets the path of the MSBuild SDKs directory"),
		FInfo ("GetVsInstallRoot", MSBuildValueKind.String, "Gets the root directory of the Visual Studio installation"),
		FInfo ("GetProgramFiles32", MSBuildValueKind.String, "Gets the path of the 32-bit Program Files directory"),
		FInfo ("GetMSBuildExtensionsPath", MSBuildValueKind.String, "Gets the value of MSBuildExtensionsPath"),
		FInfo ("IsRunningFromVisualStudio", MSBuildValueKind.Bool, "Whether MSBuild is running from Visual Studio")
	};

	/// <summary>
	/// Returns the full names of all .NET types on which it is permitted to invoke static functions.
	/// </summary>
	public static IReadOnlyCollection<string> GetPermittedStaticFunctionTypes () => permittedFunctions.Keys;

	/// <summary>
	/// Determines which static functions are permitted to be invoked on the specified .NET type.
	/// </summary>
	/// <param name="fullTypeName">The full name of the .NET type.</param>
	/// <param name="isPermitted">A predicate to determine whether a function is permitted to be invoked.</param>
	/// <returns></returns>
	public static bool TryGetPermittedStaticFunctions (string fullTypeName, out Predicate<string> isPermitted)
	{
		if (permittedFunctions.TryGetValue (fullTypeName, out var permittedNames)) {
			isPermitted = permittedNames is not null? permittedNames.Contains : ((string _) => true);
			return true;
		} else {
			isPermitted = (_) => false;
			return false;
		}
	}

	// TODO: use MSBuild's src/Build/Resources/Constants.cs
	static readonly Dictionary<string, HashSet<string>> permittedFunctions = new () {
			{ "System.Byte", null },
			{ "System.Char", null },
			{ "System.Convert", null },
			{ "System.DateTime", null },
			{ "System.DateTimeOffset", null },
			{ "System.Decimal", null },
			{ "System.Double", null },
			{ "System.Enum", null },
			{ "System.Guid", null },
			{ "System.Int16", null },
			{ "System.Int32", null },
			{ "System.Int64", null },
			{ "System.IO.Path", null },
			{ "System.Math", null },
			{ "System.UInt16", null },
			{ "System.UInt32", null },
			{ "System.UInt64", null },
			{ "System.SByte", null },
			{ "System.Single", null },
			{ "System.String", null },
			{ "System.StringComparer", null },
			{ "System.TimeSpan", null },
			{ "System.Text.RegularExpressions.Regex", null },
			{ "System.UriBuilder", null },
			{ "System.Version", null },
			{ "Microsoft.Build.Utilities.ToolLocationHelper", null },
			{ "System.Runtime.InteropServices.RuntimeInformation", null },
			{ "System.Runtime.InteropServices.OSPlatform", null },
			{ "System.OperatingSystem", null }, // NET 5.0 +
			{ "System.Environment", new HashSet<string> {
				"ExpandEnvironmentVariables",
				"GetEnvironmentVariable",
				"GetEnvironmentVariables",
				"GetFolderPath",
				"GetLogicalDrives",
				"CommandLine",
				"Is64BitOperatingSystem",
				"Is64BitProcess",
				"MachineName",
				"NewLine",
				"OSVersion",
				"ProcessorCount",
				"StackTrace",
				"SystemDirectory",
				"SystemPageSize",
				"TickCount",
				"UserDomainName",
				"UserInteractive",
				"UserName",
				"Version",
				"WorkingSet"
			} },
			{ "System.IO.Directory", new HashSet<string> {
				"GetDirectories",
				"GetFiles",
				"GetLastAccessTime",
				"GetLastWriteTime",
				"GetParent"
			} },
			{ "System.IO.File", new HashSet<string> {
				"Exists",
				"GetCreationTime",
				"GetAttributes",
				"GetLastAccessTime",
				"GetLastWriteTime",
				"ReadAllText"
			} },
			{ "System.Globalization.CultureInfo", new HashSet<string> {
				"GetCultureInfo",
				".ctor", // FIXME: alias this as "new"?
				"CurrentUICulture"
			} }
		};
}
