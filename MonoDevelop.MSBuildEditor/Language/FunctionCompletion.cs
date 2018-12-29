// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.Schema;

namespace MonoDevelop.MSBuildEditor.Language
{
	static class FunctionCompletion
	{
		public static IEnumerable<BaseInfo> GetPropertyFunctionNameCompletions (ExpressionNode triggerExpression)
		{
			if (triggerExpression is Expression expression) {
				triggerExpression = expression.Nodes.Last ();
			}

			var incomplete = (triggerExpression as IncompleteExpressionError)?.IncompleteNode;
			incomplete = incomplete?.Find (incomplete.Length);

			if (!(incomplete is ExpressionPropertyFunctionInvocation node)) {
				return null;
			}

			//string function completion
			if (node.Target is ExpressionPropertyName || node.Target is ExpressionPropertyFunctionInvocation) {
				return GetStringMethods ();
			}

			return null;
		}

		public static IEnumerable<BaseInfo> GetItemFunctionNameCompletions (ExpressionNode triggerExpression)
		{
			return GetIntrinsicItemFunctions ()
				.Concat (GetStringMethods ());
		}

		static IEnumerable<BaseInfo> GetStringMethods ()
		{
			var compilation = CreateCoreCompilation ();
			var type = compilation.GetTypeByMetadataName ("System.String");
			foreach (var member in type.GetMembers ()) {
				if (!(member is IMethodSymbol method)) {
					continue;
				}
				if (method.IsStatic || !method.DeclaredAccessibility.HasFlag (Accessibility.Public)) {
					continue;
				}
				yield return new RoslynFunctionInfo (method);
			}
		}

		static Compilation CreateCoreCompilation ()
		{
			return CSharpCompilation.Create (
				"FunctionCompletion",
				references: new [] {
					RoslynHelpers.GetReference (typeof(string).Assembly.Location),
				}
			);
		}

		static IEnumerable<BaseInfo> GetIntrinsicItemFunctions ()
		{
			yield return new FunctionInfo (
				"Count",
				"Counts the number of items.",
				MSBuildValueKind.Int);
			yield return new FunctionInfo (
				"DirectoryName",
				"Transforms each item into its directory name.",
				MSBuildValueKind.String);
			yield return new FunctionInfo (
				"Metadata",
				"Returns the values of the specified metadata.",
				MSBuildValueKind.String,
				new FunctionParameterInfo ("name", "Name of the metadata", MSBuildValueKind.MetadataName));
			yield return new FunctionInfo (
				"DistinctWithCase",
				"Returns the items with distinct ItemSpecs, respecting case but ignoring metadata.",
				MSBuildValueKind.MatchItem.List());
			yield return new FunctionInfo (
				"Distinct",
				"Returns the items with distinct ItemSpecs, ignoring case and metadata.",
				MSBuildValueKind.MatchItem.List());
			yield return new FunctionInfo (
				"Reverse",
				"Reverses the list.",
				MSBuildValueKind.MatchItem.List());
			yield return new FunctionInfo (
				"ClearMetadata",
				"Returns the items with their metadata cleared.",
				MSBuildValueKind.MatchItem.List());
			yield return new FunctionInfo (
				"HasMetadata",
				"Returns the items that have non-empty values for the specified metadata.",
				MSBuildValueKind.MatchItem.List(),
				new FunctionParameterInfo ("name", "Name of the metadata", MSBuildValueKind.MetadataName));
			yield return new FunctionInfo (
				"WithMetadataValue",
				"Returns items that have the specified metadata value, ignoring case.",
				MSBuildValueKind.MatchItem.List (),
				new FunctionParameterInfo ("name", "Name of the metadata", MSBuildValueKind.MetadataName),
				new FunctionParameterInfo ("value", "Value of the metadata", MSBuildValueKind.String));
			yield return new FunctionInfo (
				"AnyHaveMetadataValue",
				"Returns true if any item has the specified metadata name and value, ignoring case.",
				MSBuildValueKind.Bool,
				new FunctionParameterInfo ("name", "Name of the metadata", MSBuildValueKind.MetadataName),
				new FunctionParameterInfo ("value", "Value of the metadata", MSBuildValueKind.String));
		}

		static IEnumerable<BaseInfo> GetIntrinsicPropertyFunctions ()
		{
			yield return new ConstantInfo (
				"Add",
				"double Add (double a, double b)\n" +
				"Add two doubles");
			yield return new ConstantInfo (
				"Add",
				"long Add (long a, long b)\n" +
				"Add two longs");
			yield return new ConstantInfo (
				"Subtract",
				"double Subtract (double a, double b)\n" +
				"Subtract two doubles");
			yield return new ConstantInfo (
				"Subtract",
				"long Subtract (long a, long b)\n" +
				"Subtract two longs");
			yield return new ConstantInfo (
				"Multiply",
				"double Multiply (double a, double b)\n" +
				"Multiply two doubles");
			yield return new ConstantInfo (
				"Multiply",
				"long Multiply (long a, long b)\n" +
				"Multiply two longs");
			yield return new ConstantInfo (
				"Divide",
				"double Divide (double a, double b)\n" +
				"Divide two doubles");
			yield return new ConstantInfo (
				"Divide",
				"long Divide (long a, long b)\n" +
				"Divide two longs");
			yield return new ConstantInfo (
				"Modulo",
				"double Modulo (double a, double b)\n" +
				"Modulo two doubles");
			yield return new ConstantInfo (
				"Modulo",
				"long Modulo (long a, long b)\n" +
				"Modulo two longs");

			yield return new ConstantInfo (
				"Escape",
				"string Escape (string unescaped)\n" +
				"Escape the string according to MSBuild's escaping rules");
			yield return new ConstantInfo (
				"Unescape",
				"string Unescape (string escaped)\n" +
				"Unescape the string according to MSBuild's escaping rules");


			yield return new ConstantInfo (
				"BitwiseOr",
				"int BitwiseOr (int first, int second)\n" +
				"Perform a bitwise OR on the first and second (first | second)");
			yield return new ConstantInfo (
				"BitwiseAnd",
				"int BitwiseAnd (int first, int second)\n" +
				"Perform a bitwise AND on the first and second (first & second)");
			yield return new ConstantInfo (
				"BitwiseXor",
				"int BitwiseXor (int first, int second)\n" +
				"Perform a bitwise XOR on the first and second (first ^ second)");
			yield return new ConstantInfo (
				"BitwiseNot",
				"int BitwiseNot (int first)\n" +
				"Perform a bitwise NOT on the first and second (~first)");


			yield return new ConstantInfo (
				"GetRegistryValue",
				"object GetRegistryValue (string keyName, string valueName)\n" +
				"Get the value of the registry key and value, default value is null");
			yield return new ConstantInfo (
				"GetRegistryValue",
				"object GetRegistryValue (string keyName, string valueName, object defaultValue)\n" +
				"Get the value of the registry key and value");
			yield return new ConstantInfo (
				"GetRegistryValueFromView",
				"object GetRegistryValueFromView (string keyName, string valueName, object defaultValue, params object [] views)\n" +
				"Get the value of the registry key from one of the RegistryView's specified");

			yield return new ConstantInfo (
				"MakeRelative",
				"string MakeRelative (string basePath, string path)\n" +
				"Converts a file path to be relative to the specified base path.");
			yield return new ConstantInfo (
				"GetDirectoryNameOfFileAbove",
				"string GetDirectoryNameOfFileAbove (string startingDirectory, string fileName)\n" +
				"Searches upward for a directory containing the specified file, beginning in the specified directory.");
			yield return new ConstantInfo (
				"GetPathOfFileAbove",
				"string GetPathOfFileAbove (string file, string startingDirectory)\n" +
				"Searches upward for the specified file, beginning in the specified directory.");

			yield return new ConstantInfo (
				"ValueOrDefault",
				"string ValueOrDefault (string conditionValue, string defaultValue)\n" +
				"Return the string in parameter 'defaultValue' only if parameter 'conditionValue' is empty, else, return the value conditionValue");
			yield return new ConstantInfo (
				"DoesTaskHostExist",
				"bool DoesTaskHostExist (string runtime, string architecture)\n" +
				"Returns true if a task host exists that can service the requested runtime and architecture");
			yield return new ConstantInfo (
				"EnsureTrailingSlash",
				"string EnsureTrailingSlash (string path)\n" +
				"If the given path doesn't have a trailing slash then add one. If empty, leave it empty.");
			yield return new ConstantInfo (
				"NormalizeDirectory",
				"string NormalizeDirectory (params string [] path)\n" +
				"Gets the canonical full path of the provided directory, with correct directory separators for the current OS and a trailing slash.");
			yield return new ConstantInfo (
				"NormalizePath",
				"string NormalizePath (params string [] path)\n" +
				"Gets the canonical full path of the provided path, with correct directory separators for the current OS.");
			yield return new ConstantInfo (
				"IsOSPlatform",
				"bool IsOSPlatform (string platformString)\n" +
				"Whether the current OS platform is the specified OSPlatform value. Case insensitive.");
			yield return new ConstantInfo (
				"IsOsUnixLike",
				"bool IsOsUnixLike ()\n" +
				"True if current OS is a Unix system.");
			yield return new ConstantInfo (
				"IsOsBsdLike",
				"bool IsOsBsdLike ()\n" +
				"True if current OS is a BSD system.");
			yield return new ConstantInfo (
				"GetCurrentToolsDirectory",
				"string GetCurrentToolsDirectory ()\n" +
				"Gets the path of the current tools directory");
			yield return new ConstantInfo (
				"GetToolsDirectory32",
				"string GetToolsDirectory32 ()\n" +
				"Gets the path of the 32-bit tools directory");
			yield return new ConstantInfo (
				"GetToolsDirectory64",
				"string GetToolsDirectory64 ()\n" +
				"Gets the path of the 64-bit tools directory");
			yield return new ConstantInfo (
				"GetMSBuildSDKsPath",
				"string GetMSBuildSDKsPath ()\n" +
				"Gets the path of the MSBuild SDKs directory");
			yield return new ConstantInfo (
				"GetVsInstallRoot",
				"string GetVsInstallRoot ()\n" +
				"Gets the root directory of the Visual Studio installation");
			yield return new ConstantInfo (
				"GetProgramFiles32",
				"string GetProgramFiles32 ()\n" +
				"Gets the path of the 32-bit Program Files directory");
			yield return new ConstantInfo (
				"GetMSBuildExtensionsPath",
				"string GetMSBuildExtensionsPath ()\n" +
				"Gets the value of MSBuildExtensionsPath");
			yield return new ConstantInfo (
				"IsRunningFromVisualStudio",
				"bool IsRunningFromVisualStudio ()\n" +
				"Whether MSBuild is running from Visual Studio");
		}
	}
}
