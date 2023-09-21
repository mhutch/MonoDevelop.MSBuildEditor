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

	// These must be manually updated as they are defined on the IntrinsicItemFunctions static class in https://github.com/dotnet/msbuild/blob/main/src/Build/Evaluation/Expander.cs
	static FunctionInfo[] CreateIntrinsicItemFunctions () => new[] {
		FInfo ("Combine", MSBuildValueKind.MatchItem.AsList (), "Returns a new set of items with the given path appended to each of the input items.",
			FArg ("path", MSBuildValueKind.String, "A relative path")),
		FInfo ("Count", MSBuildValueKind.Int, "Counts the number of items."),
		FInfo ("DirectoryName", MSBuildValueKind.String, "Transforms each item into its directory name."),
		FInfo ("Distinct", MSBuildValueKind.ItemName.AsList(),  "Returns the items with distinct ItemSpecs, ignoring case and metadata."),
		FInfo ("DistinctWithCase", MSBuildValueKind.ItemName.AsList(), "Returns the items with distinct ItemSpecs, respecting case but ignoring metadata."),
		FInfo ("Exists", MSBuildValueKind.Bool, "Returns a new set of items with the given path appended to each of the input items."),
		FInfo ("GetPathsOfAllDirectoriesAbove", MSBuildValueKind.MatchItem.AsList (), "Returns a set of items representing all the directories above the specified items, in no particular order."),
		FInfo ("Metadata", MSBuildValueKind.String, "Returns items where the ItemSpec is the value of the specified metadata on the source item, with the metadata from the source items preserved.",
			FArg ("name", MSBuildValueKind.MetadataName, "Name of the metadata")
		),
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

	// These must be manually updated as they are defined in https://github.com/dotnet/msbuild/blob/main/src/Build/Evaluation/IntrinsicFunctions.cs.
	// Note that some have special casing in how they are dispatched in https://github.com/dotnet/msbuild/blob/main/src/Build/Evaluation/Expander.cs
	static FunctionInfo[] CreateIntrinsicPropertyFunctions () => new FunctionInfo[] {
		//these are all really doubles and longs but MSBuildValueKind doesn't make a distinction
		// math functions
		FInfo ("Add", MSBuildValueKind.Float, "Add two doubles",
			FArg ("first", MSBuildValueKind.Float, "First operand"),
			FArg ("second", MSBuildValueKind.Float, "Second operand")
		),
		FInfo ("Add", MSBuildValueKind.Int, "Add two longs",
			FArg ("first", MSBuildValueKind.Int, "First operand"),
			FArg ("second", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("Subtract", MSBuildValueKind.Float, "Subtract two doubles",
			FArg ("first", MSBuildValueKind.Float, "First operand"),
			FArg ("second", MSBuildValueKind.Float, "Second operand")
		),
		FInfo ("Subtract", MSBuildValueKind.Int, "Subtract two longs",
			FArg ("first", MSBuildValueKind.Int, "First operand"),
			FArg ("second", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("Multiply", MSBuildValueKind.Float, "Multiply two doubles",
			FArg ("first", MSBuildValueKind.Float, "First operand"),
			FArg ("second", MSBuildValueKind.Float, "Second operand")
		),
		FInfo ("Multiply", MSBuildValueKind.Int, "Multiply two longs",
			FArg ("first", MSBuildValueKind.Int, "First operand"),
			FArg ("second", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("Divide", MSBuildValueKind.Float, "Divide two doubles",
			FArg ("first", MSBuildValueKind.Float, "First operand"),
			FArg ("second", MSBuildValueKind.Float, "Second operand")
		),
		FInfo ("Divide", MSBuildValueKind.Int, "Divide two longs",
			FArg ("first", MSBuildValueKind.Int, "First operand"),
			FArg ("second", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("Modulo", MSBuildValueKind.Float, "Modulo two doubles",
			FArg ("first", MSBuildValueKind.Float, "First operand"),
			FArg ("second", MSBuildValueKind.Float, "Second operand")
		),
		FInfo ("Modulo", MSBuildValueKind.Int, "Modulo two longs",
			FArg ("first", MSBuildValueKind.Int, "First operand"),
			FArg ("second", MSBuildValueKind.Int, "Second operand")
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
			FArg ("first", MSBuildValueKind.Int, "First operand"),
			FArg ("second", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("BitwiseAnd", MSBuildValueKind.Int, "Perform a bitwise AND on the first and second (first & second)",
			FArg ("first", MSBuildValueKind.Int, "First operand"),
			FArg ("second", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("BitwiseXor", MSBuildValueKind.Int, "Perform a bitwise XOR on the first and second (first ^ second)",
			FArg ("first", MSBuildValueKind.Int, "First operand"),
			FArg ("second", MSBuildValueKind.Int, "Second operand")
		),
		FInfo ("BitwiseNot", MSBuildValueKind.Int, "Perform a bitwise NOT on the first (~first)",
			FArg ("first", MSBuildValueKind.Int, "First operand")
		),
		FInfo ("LeftShift", MSBuildValueKind.Int, "Perform a left-shift on the operand (operand << count)",
			FArg ("operand", MSBuildValueKind.Int, "The operand to be left-shifted"),
			FArg ("count", MSBuildValueKind.Int, "The number of places to left-shift the operand")
		),
		FInfo ("RightShift", MSBuildValueKind.Int, "Perform a right-shift on the operand (operand << count)",
			FArg ("operand", MSBuildValueKind.Int, "The operand to be right-shifted"),
			FArg ("count", MSBuildValueKind.Int, "The number of places to right-shift the operand")
		),
		FInfo ("RightShiftUnsigned", MSBuildValueKind.Int, "Perform an unsigned right-shift on the operand (operand << count)",
			FArg ("operand", MSBuildValueKind.Int, "The operand to be right-shifted"),
			FArg ("count", MSBuildValueKind.Int, "The number of places to right-shift the operand")
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
			//TODO: params, registryView enum
			//TODO: what should we do about the overload that takes an ArraySegment<object>?
			FArg ("views", MSBuildValueKind.Object.AsList(), "Which registry view(s) to use")
		),

		// path manipulation
		FInfo ("MakeRelative", MSBuildValueKind.String, "Converts a file path to be relative to the specified base path.",
			FArg ("basePath", MSBuildValueKind.String, "The returned path is relative to this base path. " +
				"Must be an absolute path. " +
				"Do not include a filename as the last segment will always be treated as a directory."),
			FArg ("path", MSBuildValueKind.String, "The path to make relative to basePath. " +
				"If it is not absolute, it is interpreted relative to basePath. " +
				"If it cannot be made relative (e.g. it is on another drive) it is returned verbatim.")
		),
		FInfo ("GetDirectoryNameOfFileAbove", MSBuildValueKind.String, "Searches upward for a directory containing the specified file, beginning in the specified directory. If not found, returns an empty string.",
			FArg ("startingDirectory", MSBuildValueKind.String, "The directory in which to start the search. If empty, defaults to the directory containing the file in which the property function is used."),
			FArg ("fileName", MSBuildValueKind.String, "The filename for which to search")
		),
		FInfo ("GetPathOfFileAbove", MSBuildValueKind.String, "Searches upward for the specified file, beginning in the specified directory. If not found, returns an empty string.",
			//yes, GetPathOfFileAbove and GetDirectoryNameOfFileAbove have reversed args
			FArg ("file", MSBuildValueKind.String, "The filename for which to search"),
			FArg ("startingDirectory", MSBuildValueKind.String, "The directory in which to start the search. If empty, defaults to the directory containing the file in which the property function is used.")
		),

		// versions

		FInfo ("VersionEquals", MSBuildValueKind.Bool, "Check whether two versions are equal.",
			FArg ("first", MSBuildValueKind.String, "The first version"),
			FArg ("second", MSBuildValueKind.String, "The second version")
		),
		FInfo ("VersionNotEquals", MSBuildValueKind.Bool, "Check whether two versions are not equal.",
			FArg ("first", MSBuildValueKind.String, "The first version"),
			FArg ("second", MSBuildValueKind.String, "The second version")
		),
		FInfo ("VersionGreaterThan", MSBuildValueKind.Bool, "Check whether two versions are not equal.",
			FArg ("first", MSBuildValueKind.String, "The first version"),
			FArg ("second", MSBuildValueKind.String, "The second version")
		),
		FInfo ("VersionGreaterThanOrEquals", MSBuildValueKind.Bool, "Check whether the `first` version is greater than or equal to the `second` version.",
			FArg ("first", MSBuildValueKind.String, "The first version"),
			FArg ("second", MSBuildValueKind.String, "The second version")
		),
		FInfo ("VersionLessThan", MSBuildValueKind.Bool, "Check whether the `first` version is less than the `second` version.",
			FArg ("first", MSBuildValueKind.String, "The first version"),
			FArg ("second", MSBuildValueKind.String, "The second version")
		),
		FInfo ("VersionLessThanOrEquals", MSBuildValueKind.Bool, "Check whether the `first` version is less than or equal to the `second` version.",
			FArg ("first", MSBuildValueKind.String, "The first version"),
			FArg ("second", MSBuildValueKind.String, "The second version")
		),

		// target frameworks

		FInfo ("GetTargetFrameworkIdentifier", MSBuildValueKind.String, "Parse the `TargetFrameworkIdentifier` from a `TargetFramework` value.",
			FArg ("targetFramework", MSBuildValueKind.String, "The target framework")
		),
		FInfo ("GetTargetFrameworkVersion", MSBuildValueKind.String, "Parse the `TargetFrameworkVersion` from a `TargetFramework` value.",
			FArg ("targetFramework", MSBuildValueKind.String, "The target framework"),
			FArg ("minVersionPartCount", MSBuildValueKind.Int, "The minimum number of parts the returned version should have. Any zero-valued components beyond this minimum will be omitted.")
		),
		// as we don't support default values (minVersionPartCount = 2), add an overlead instead
		FInfo ("GetTargetFrameworkVersion", MSBuildValueKind.String, "Parse the `TargetFrameworkVersion` from a `TargetFramework` value, omitting any zero-valued components beyond the major and minor version",
			FArg ("targetFramework", MSBuildValueKind.String, "The target framework")
		),
		FInfo ("IsTargetFrameworkCompatible", MSBuildValueKind.Bool, "Check whether the first `TargetFramework` can reference the second `TargetFramework`.",
			FArg ("baseTargetFramework", MSBuildValueKind.String, "The base TargetFramework"),
			FArg ("candidateTargetFramework", MSBuildValueKind.String, "The TargetFramework candidate to be be referenced")
		),
		FInfo ("GetTargetPlatformIdentifier", MSBuildValueKind.String, "Parse the `TargetFrameworkPlatform` from a `TargetFramework` value.",
			FArg ("targetFramework", MSBuildValueKind.String, "The target framework")
		),
		FInfo ("GetTargetPlatformVersion", MSBuildValueKind.String, "Parse the `TargetPlatformVersion` from a `TargetFramework` value.",
			FArg ("targetFramework", MSBuildValueKind.String, "The target framework"),
			FArg ("minVersionPartCount", MSBuildValueKind.Int, "The minimum number of parts the returned version should have. Any zero-valued components beyond this minimum will be omitted.")
		),
		// as we don't support default values (minVersionPartCount = 2), add an overlead instead
		FInfo ("GetTargetPlatformVersion", MSBuildValueKind.String, "Parse the `TargetPlatformVersion` from a `TargetFramework` value, omitting any zero-valued components beyond the major and minor version.",
			FArg ("targetFramework", MSBuildValueKind.String, "The target framework")
		),
		// https://github.com/dotnet/msbuild/pull/8350
		FInfo ("FilterTargetFrameworks", MSBuildValueKind.TargetFramework.AsList (),
			"Filter the `incoming` list of TFMs, returning only those that match the `TargetFrameworkIdentifier` and " +
			"`TargetFrameworkVersion` of one or more of the `filter` TFMs. The version component of the filter TFMs is optional. " +
			"Note that `TargetPlatformIdentifier` and `TargetPlatformVersion` components are ignored entirely and stripped from the returned TFMs.",
			FArg ("incoming", MSBuildValueKind.TargetFramework.AsList (), "The list of TFMs to be returnd if they match a filter TFM"),
			FArg ("filter", MSBuildValueKind.TargetFramework.AsList (), "The list of TFM filters")
		),

		// other

		FInfo ("ValueOrDefault", MSBuildValueKind.String, "Return the string in parameter `defaultValue` only if parameter `conditionValue` is empty, else, return the value `conditionValue`",
			FArg ("conditionValue", MSBuildValueKind.String, "The condition"),
			FArg ("defaultValue", MSBuildValueKind.String, "The default value")
		),
		FInfo ("ConvertToBase64", MSBuildValueKind.String, "Returns the string after converting all bytes to base 64 (alphanumeric characters plus '+' and '/'), ending in one or two '='.",
			FArg ("toEncode", MSBuildValueKind.String, "String to encode in base 64")
		),
		FInfo ("ConvertFromBase64", MSBuildValueKind.String, "Returns the string after converting from base 64 (alphanumeric characters plus '+' and '/'), ending in one or two '='.",
			FArg ("toEncode", MSBuildValueKind.String, "The string to decode from base 64")
		),
		FInfo ("StableStringHash", MSBuildValueKind.String, "Hash the string independent of bitness and target framework",
			FArg ("toHash", MSBuildValueKind.String, "The string to hash")
		),
		FInfo ("DoesTaskHostExist", MSBuildValueKind.Bool, "Returns true if a task host exists that can service the requested runtime and architecture",
			//FIXME type these more strongly for intellisense
			FArg ("runtime", MSBuildValueKind.String, "The runtime"),
			FArg ("architecture", MSBuildValueKind.String, "The architecture")
		),
		FInfo ("EnsureTrailingSlash", MSBuildValueKind.String, "If the given path doesn't have a trailing slash then add one. If empty, leave it empty.",
			FArg ("path", MSBuildValueKind.String, "The path to check")
		),
		FInfo ("NormalizeDirectory", MSBuildValueKind.String, "Gets the canonical full path of the provided directory, with correct directory separators for the current OS and a trailing slash.",
			//FIXME add support for params - this annotation isn't correct, as the AsList() means semicolon separated string
			FArg ("path", MSBuildValueKind.String.AsList(), "One or more directory paths to combine and normalize")
		),
		FInfo ("NormalizePath", MSBuildValueKind.String, "Gets the canonical full path of the provided path, with correct directory separators for the current OS.",
			//FIXME add support for params - this annotation isn't correct, as the AsList() means semicolon separated string
			FArg ("path", MSBuildValueKind.String.AsList (), "One or more directory paths to combine and normalize")
		),
		FInfo ("IsOSPlatform", MSBuildValueKind.Bool, "Whether the current OS platform is the specified OSPlatform value. Case insensitive.",
			//FIXME stronger typing
			FArg ("platformString", MSBuildValueKind.String, "The OSPlatform value")
		),
		FInfo ("IsOsUnixLike", MSBuildValueKind.Bool, "True if current OS is a Unix system."),
		FInfo ("IsOsBsdLike", MSBuildValueKind.Bool, "True if current OS is a BSD system."),

		FInfo ("AreFeaturesEnabled", MSBuildValueKind.Bool, "True if the specified feature wave is enabled.",
			FArg ("featureWave", MSBuildValueKind.Version, "The version of the feature wave")
		),
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
