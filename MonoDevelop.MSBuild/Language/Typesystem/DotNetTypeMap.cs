// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MonoDevelop.MSBuild.Language.Typesystem;

/// <summary>
/// Helpers for mapping MSBuild types to and from .NET Types
/// </summary>
static class DotNetTypeMap
{
	public static string? FromValueKind (MSBuildValueKind kind)
	{
		if (kind.AllowsLists ()) {
			throw new ArgumentException ("Cannot convert a list value kind to a .NET type name");
		}

		kind = kind.WithoutModifiers ();

		switch (kind) {
		case MSBuildValueKind.String:
			return "System.String";
		case MSBuildValueKind.Bool:
			return "System.Boolean";
		case MSBuildValueKind.Int:
			return "System.Int32";
		case MSBuildValueKind.Char:
			return "System.Char";
		case MSBuildValueKind.Float:
			return "System.Float";
		case MSBuildValueKind.Object:
			return "System.Object";
		case MSBuildValueKind.DateTime:
			return "System.DateTime";
		}

		return null;
	}

	public static MSBuildValueKind ToValueKind (string fullTypeName)
		=> fullTypeName switch {
			"System.String" => MSBuildValueKind.String,
			"System.Boolean" => MSBuildValueKind.Bool,
			"System.Int32" => MSBuildValueKind.Int,
			"System.UInt32" => MSBuildValueKind.Int,
			"System.Int64" => MSBuildValueKind.Int,
			"System.UInt64" => MSBuildValueKind.Int,
			"System.Char" => MSBuildValueKind.Char,
			"System.Float" => MSBuildValueKind.Float,
			"System.Double" => MSBuildValueKind.Float,
			"Microsoft.Build.Framework.ITaskItem" => MSBuildValueKind.UnknownItem,
			"System.Object" => MSBuildValueKind.Object,
			"System.DateTime" => MSBuildValueKind.DateTime,
			_ => MSBuildValueKind.Unknown
		};
}
