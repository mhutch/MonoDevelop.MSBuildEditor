// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Typesystem;

using ISymbol = MonoDevelop.MSBuild.Language.ISymbol;

namespace MonoDevelop.MSBuild.Editor.Roslyn
{
	[Export (typeof (IFunctionTypeProvider))]
	class RoslynFunctionTypeProvider : IFunctionTypeProvider
	{
		[Import (AllowDefault=true)]
		public IRoslynCompilationProvider AssemblyLoader { get; set; }

		readonly object locker = new object ();
		Compilation compilation;
		Task compilationLoadTask;

		public RoslynFunctionTypeProvider ()
		{
		}

		public RoslynFunctionTypeProvider (Compilation compilation)
		{
			if (compilation == null) {
				this.compilation = compilation;
				compilationLoadTask = Task.CompletedTask;
			}
		}

		//we need the reference assembly to get docs
		static string GetMscorlibReferenceAssembly ()
		{
			if (Util.Platform.IsWindows) {
				//FIXME: enumerate installed frameworks
				var programFiles86 = Environment.GetFolderPath (Environment.SpecialFolder.ProgramFilesX86);
				var file = Path.Combine (programFiles86, @"Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\mscorlib.dll");
				if (File.Exists (file)) {
					return file;
				}
			}
			return typeof (string).Assembly.Location;
		}

		Task LoadCoreCompilation ()
		{
			if (AssemblyLoader == null) {
				return Task.CompletedTask;
			}
			return Task.Run (() => {
				try {
					var mscorlibPath = GetMscorlibReferenceAssembly ();
					compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create (
						"FunctionCompletion",
						references: new[] {
							AssemblyLoader.CreateReference (mscorlibPath)
						});
				} catch (Exception ex) {
					LoggingService.LogError ("Failed to load roslyn compilation for function completion", ex);
				}
			});
		}

		public Task EnsureInitialized (CancellationToken token)
		{
			if (compilationLoadTask == null) {
				lock (locker) {
					if (compilationLoadTask == null) {
						compilationLoadTask = LoadCoreCompilation ();
					}
				}
			}
			if (compilationLoadTask.IsCompleted || !token.CanBeCanceled) {
				return compilationLoadTask;
			}

			//allow aborting the await without cancelling the loader task
			return Task.WhenAny (compilationLoadTask, Task.Delay (-1, token));
		}

		public IEnumerable<FunctionInfo> GetPropertyFunctionNameCompletions (ExpressionNode triggerExpression)
		{
			if (triggerExpression is ConcatExpression expression) {
				triggerExpression = expression.Nodes.Last ();
			}

			var last = triggerExpression.Find (triggerExpression.End);
			if (last == null) {
				return null;
			}

			if (last is ExpressionFunctionName pn) {
				last = pn.Parent;
			}

			if (!(last is ExpressionPropertyFunctionInvocation node)) {
				return null;
			}

			if (node.Target is ExpressionClassReference classRef) {
				if (classRef.Name == "MSBuild") {
					return CollapseOverloads (GetIntrinsicPropertyFunctions ());
				}
				if (permittedFunctions.TryGetValue (classRef.Name, out HashSet<string> members)) {
					return CollapseOverloads (GetStaticFunctions (classRef.Name, members));
				}
				return null;
			}

			//function completion
			if (node.Target is ExpressionPropertyNode epn && (epn is ExpressionPropertyName || epn is ExpressionPropertyFunctionInvocation)) {
				var type = ResolveType (epn);
				return CollapseOverloads (GetInstanceFunctions (type, true, false));
			}

			return null;
		}

		public MSBuildValueKind ResolveType (ExpressionPropertyNode node)
		{
			if (node is ExpressionPropertyName) {
				return MSBuildValueKind.Unknown;
			}

			if (node is ExpressionPropertyFunctionInvocation inv && inv.Target is ExpressionPropertyNode epn) {
				if (epn is ExpressionClassReference classRef) {
					var info = GetStaticPropertyFunctionInfo (classRef.Name, inv.Function.Name);
					return info.ReturnType;
				}

				//FIXME: maybe this could pass the types along directly instead of constantly converting
				var targetType = ResolveType (epn);

				//FIXME: overload resolution
				var match = Find (GetInstanceFunctions (targetType, true, true), inv.Function?.Name);
				if (match != null) {
					return match.ReturnType;
				}
				return MSBuildValueKind.Unknown;
			}

			return MSBuildValueKind.Unknown;
		}

		public IEnumerable<FunctionInfo> GetItemFunctionNameCompletions ()
		{
			return CollapseOverloads (GetIntrinsicItemFunctions ().Concat (GetStringFunctions (false, false)));
		}

		public IEnumerable<ClassInfo> GetClassNameCompletions ()
		{
			foreach (var kv in permittedFunctions) {
				var type = compilation?.GetTypeByMetadataName (kv.Key);
				if (type != null) {
					yield return new RoslynClassInfo (kv.Key, type);
				} else {
					yield return new ClassInfo (kv.Key, null);
				}
			}
			yield return new ClassInfo ("MSBuild", "Intrinsic MSBuild functions");
		}

		public ICollection<FunctionInfo> CollapseOverloads (IEnumerable<FunctionInfo> infos)
		{
			var functions = new Dictionary<string, FunctionInfo> ();
			foreach (var info in infos) {
				if (functions.TryGetValue (info.Name, out FunctionInfo existing)) {
					existing.Overloads.Add (info);
				} else {
					functions.Add (info.Name, info);
				}
			}
			return functions.Values.ToArray ();
		}

		//FIXME: make this lookup cheaper
		public FunctionInfo GetStaticPropertyFunctionInfo (string className, string name)
		{
			if (className == null) {
				return null;
			}
			if (className == "MSBuild") {
				return GetIntrinsicPropertyFunctions ().FirstOrDefault (n => n.Name == name);
			}
			if (permittedFunctions.TryGetValue (className, out HashSet<string> members)) {
				return GetStaticFunctions (className, members).FirstOrDefault (n => n.Name == name);
			}
			return null;
		}

		//FIXME: make this lookup cheaper
		public FunctionInfo GetPropertyFunctionInfo (MSBuildValueKind valueKind, string name)
		{
			return Find (GetInstanceFunctions (valueKind, true, true), name);
		}

		static FunctionInfo Find (IEnumerable<FunctionInfo> functions, string name)
		{
			if (name == null) {
				return functions.FirstOrDefault (f => f is RoslynPropertyInfo rf && rf.Symbol.IsIndexer);
			}
			return functions.FirstOrDefault (f => f.Name == name);
		}

		//FIXME: make this lookup cheaper
		public FunctionInfo GetItemFunctionInfo (string name)
		{
			return GetItemFunctionNameCompletions ().FirstOrDefault (n => n.Name == name);
		}

		//FIXME: make this lookup cheaper
		public ClassInfo GetClassInfo (string name)
		{
			return GetClassNameCompletions ().FirstOrDefault (n => n.Name == name);
		}

		public ISymbol GetEnumInfo (string reference)
		{
			//FIXME: resolve enum values
			return new ConstantSymbol (reference, null, MSBuildValueKind.Unknown);
		}

		IEnumerable<FunctionInfo> GetStringFunctions (bool includeProperties, bool includeIndexers)
		{
			var type = compilation?.GetTypeByMetadataName ("System.String");
			if (type != null) {
				return GetInstanceFunctions (type, includeProperties, includeIndexers);
			}
			return Array.Empty<FunctionInfo> ();
		}

		IEnumerable<FunctionInfo> GetInstanceFunctions (MSBuildValueKind kind, bool includeProperties, bool includeIndexers)
		{
			var dotNetType = GetDotNetTypeName (kind);

			INamedTypeSymbol type = null;
			if (dotNetType != null) {
				type = compilation?.GetTypeByMetadataName (dotNetType);
			}
			if (type == null) {
				type = compilation?.GetTypeByMetadataName ("System.String");
			}
			if (type != null) {
				return GetInstanceFunctions (type, includeProperties, includeIndexers);
			}
			return Array.Empty<FunctionInfo> ();
		}

		static IEnumerable<FunctionInfo> GetInstanceFunctions (INamedTypeSymbol type, bool includeProperties, bool includeIndexers)
		{
			foreach (var member in type.GetMembers ()) {
				if (member.IsStatic || !member.DeclaredAccessibility.HasFlag (Accessibility.Public)) {
					continue;
				}
				if (member is IMethodSymbol method) {
					switch (method.MethodKind) {
					case MethodKind.Ordinary:
					case MethodKind.PropertyGet:
					case MethodKind.BuiltinOperator:
					case MethodKind.UserDefinedOperator:
						break;
					default:
						continue;
					}
					if (ConvertType (method.ReturnType).IsUnknown ()) {
						continue;
					}
					bool unknownType = false;
					foreach (var p in method.Parameters) {
						if (ConvertType (p.Type).IsUnknown ()) {
							unknownType = true;
							break;
						}
					}
					if (unknownType) {
						continue;
					}
					yield return new RoslynFunctionInfo (method);
				} else if (includeProperties && member is IPropertySymbol prop) {
					if (ConvertType (prop.Type).IsUnknown ()) {
						continue;
					}
					if (!includeIndexers && prop.IsIndexer) {
						continue;
					}
					yield return new RoslynPropertyInfo (prop);
				}
			}
		}

		IEnumerable<FunctionInfo> GetStaticFunctions (string className, HashSet<string> members)
		{
			var type = compilation?.GetTypeByMetadataName (className);
			if (type == null) {
				yield break;
			}
			foreach (var member in type.GetMembers ()) {
				if (!member.DeclaredAccessibility.HasFlag (Accessibility.Public)) {
					continue;
				}
				if (!member.IsStatic && (member as IMethodSymbol)?.MethodKind != MethodKind.Constructor) {
					continue;
				}
				if (members != null && !members.Contains (member.Name)) {
					continue;
				}
				if (member is IMethodSymbol method) {
					switch (method.MethodKind) {
					case MethodKind.Ordinary:
					case MethodKind.PropertyGet:
					case MethodKind.BuiltinOperator:
					case MethodKind.UserDefinedOperator:
						if(!method.IsStatic) {
							continue;
						}
						if (ConvertType (method.ReturnType).IsUnknown ()) {
							continue;
						}
						break;
					case MethodKind.Constructor:
						break;
					default:
						continue;
					}

					bool unknownType = false;
					foreach (var p in method.Parameters) {
						if (ConvertType (p.Type).IsUnknown ()) {
							unknownType = true;
							break;
						}
					}
					//FIXME relax this
					if (unknownType) {
						continue;
					}
					yield return new RoslynFunctionInfo (method);
				} else if (member is IPropertySymbol prop) {
					if (ConvertType (prop.Type).IsUnknown ()) {
						continue;
					}
					yield return new RoslynPropertyInfo (prop);
				}
			}
		}

		public static MSBuildValueKind ConvertType (ITypeSymbol type)
		{
			if (type is IArrayTypeSymbol arr) {
				return ConvertType (arr.ElementType) | MSBuildValueKind.ListSemicolon;
			}

			string fullTypeName = RoslynHelpers.GetFullName (type);

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
			case "System.Char":
				return MSBuildValueKind.Char;
			case "System.Float":
			case "System.Double":
				return MSBuildValueKind.Float;
			case "Microsoft.Build.Framework.ITaskItem":
				return MSBuildValueKind.UnknownItem;
			case "System.Object":
				return MSBuildValueKind.Object;
			case "System.DateTime":
				return MSBuildValueKind.DateTime;
			}

			return MSBuildValueKind.Unknown;
		}

		static string GetDotNetTypeName (MSBuildValueKind kind)
		{
			if (kind.AllowsLists ()) {
				return null;
			}

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

		static IEnumerable<FunctionInfo> GetIntrinsicItemFunctions ()
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
				MSBuildValueKind.MatchItem.AsList());
			yield return new FunctionInfo (
				"Distinct",
				"Returns the items with distinct ItemSpecs, ignoring case and metadata.",
				MSBuildValueKind.MatchItem.AsList());
			yield return new FunctionInfo (
				"Reverse",
				"Reverses the list.",
				MSBuildValueKind.MatchItem.AsList());
			yield return new FunctionInfo (
				"ClearMetadata",
				"Returns the items with their metadata cleared.",
				MSBuildValueKind.MatchItem.AsList());
			yield return new FunctionInfo (
				"HasMetadata",
				"Returns the items that have non-empty values for the specified metadata.",
				MSBuildValueKind.MatchItem.AsList(),
				new FunctionParameterInfo ("name", "Name of the metadata", MSBuildValueKind.MetadataName));
			yield return new FunctionInfo (
				"WithMetadataValue",
				"Returns items that have the specified metadata value, ignoring case.",
				MSBuildValueKind.MatchItem.AsList (),
				new FunctionParameterInfo ("name", "Name of the metadata", MSBuildValueKind.MetadataName),
				new FunctionParameterInfo ("value", "Value of the metadata", MSBuildValueKind.String));
			yield return new FunctionInfo (
				"AnyHaveMetadataValue",
				"Returns true if any item has the specified metadata name and value, ignoring case.",
				MSBuildValueKind.Bool,
				new FunctionParameterInfo ("name", "Name of the metadata", MSBuildValueKind.MetadataName),
				new FunctionParameterInfo ("value", "Value of the metadata", MSBuildValueKind.String));
		}

		static IEnumerable<FunctionInfo> GetIntrinsicPropertyFunctions ()
		{
			//these are all really doubles and longs but MSBuildValueKind doesn't make a distinction
			yield return new FunctionInfo (
				"Add",
				"Add two doubles",
				MSBuildValueKind.Float,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Float),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Float));
			yield return new FunctionInfo (
				"Add",
				"Add two longs",
				MSBuildValueKind.Int,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Int),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Int));
			yield return new FunctionInfo (
				"Subtract",
				"Subtract two doubles",
				MSBuildValueKind.Float,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Float),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Float));
			yield return new FunctionInfo (
				"Subtract",
				"Subtract two longs",
				MSBuildValueKind.Int,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Int),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Int));
			yield return new FunctionInfo (
				"Multiply",
				"Multiply two doubles",
				MSBuildValueKind.Float,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Float),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Float));
			yield return new FunctionInfo (
				"Multiply",
				"Multiply two longs",
				MSBuildValueKind.Int,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Int),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Int));
			yield return new FunctionInfo (
				"Divide",
				"Divide two doubles",
				MSBuildValueKind.Float,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Float),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Float));
			yield return new FunctionInfo (
				"Divide",
				"Divide two longs",
				MSBuildValueKind.Int,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Int),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Int));
			yield return new FunctionInfo (
				"Modulo",
				"Modulo two doubles",
				MSBuildValueKind.Float,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Float),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Float));
			yield return new FunctionInfo (
				"Modulo",
				"Modulo two longs",
				MSBuildValueKind.Int,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Int),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Int));

			yield return new FunctionInfo (
				"Escape",
				"Escape the string according to MSBuild's escaping rules",
				MSBuildValueKind.String,
				new FunctionParameterInfo ("unescaped", "The unescaped string", MSBuildValueKind.String));
			yield return new FunctionInfo (
				"Unescape",
				"Unescape the string according to MSBuild's escaping rules",
				MSBuildValueKind.String,
				new FunctionParameterInfo ("escaped", "The escaped string", MSBuildValueKind.String));


			yield return new FunctionInfo (
				"BitwiseOr",
				"Perform a bitwise OR on the first and second (first | second)",
				MSBuildValueKind.Int,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Int),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Int));
			yield return new FunctionInfo (
				"BitwiseAnd",
				"Perform a bitwise AND on the first and second (first & second)",
				MSBuildValueKind.Int,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Int),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Int));
			yield return new FunctionInfo (
				"BitwiseXor",
				"Perform a bitwise XOR on the first and second (first ^ second)",
				MSBuildValueKind.Int,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Int),
				new FunctionParameterInfo ("b", "Second operand", MSBuildValueKind.Int));
			yield return new FunctionInfo (
				"BitwiseNot",
				"Perform a bitwise NOT on the first (~first)",
				MSBuildValueKind.Int,
				new FunctionParameterInfo ("a", "First operand", MSBuildValueKind.Int));

			yield return new FunctionInfo (
				"GetRegistryValue",
				"Get the value of the registry key and value, default value is null",
				MSBuildValueKind.Object,
				new FunctionParameterInfo ("keyName", "The key name", MSBuildValueKind.String),
				new FunctionParameterInfo ("valueName", "The value name", MSBuildValueKind.String));
			yield return new FunctionInfo (
				"GetRegistryValue",
				"Get the value of the registry key and value",
				MSBuildValueKind.Object,
				new FunctionParameterInfo ("keyName", "The key name", MSBuildValueKind.String),
				new FunctionParameterInfo ("valueName", "The value name", MSBuildValueKind.String),
				new FunctionParameterInfo ("defaultValue", "The default value", MSBuildValueKind.Object));
			yield return new FunctionInfo (
				"GetRegistryValueFromView",
				"Get the value of the registry key from one of the RegistryViews specified",
				MSBuildValueKind.Object,
				new FunctionParameterInfo ("keyName", "The key name", MSBuildValueKind.String),
				new FunctionParameterInfo ("valueName", "The value name", MSBuildValueKind.String),
				new FunctionParameterInfo ("defaultValue", "The default value", MSBuildValueKind.Object),
				//todo params, registryView enum
				new FunctionParameterInfo ("views", "Which registry view(s) to use", MSBuildValueKind.Object.AsList()));

			yield return new FunctionInfo (
				"MakeRelative",
				"Converts a file path to be relative to the specified base path.",
				MSBuildValueKind.String,
				new FunctionParameterInfo ("basePath", "The base path", MSBuildValueKind.String),
				new FunctionParameterInfo ("path", "The path to convert", MSBuildValueKind.String));
			yield return new FunctionInfo (
				"GetDirectoryNameOfFileAbove",
				"Searches upward for a directory containing the specified file, beginning in the specified directory.",
				MSBuildValueKind.String,
				new FunctionParameterInfo ("startingDirectory", "The starting directory", MSBuildValueKind.String),
				new FunctionParameterInfo ("fileName", "The filename for which to search", MSBuildValueKind.String));
			yield return new FunctionInfo (
				"GetPathOfFileAbove",
				"Searches upward for the specified file, beginning in the specified directory.",
				MSBuildValueKind.String,
				//yes, GetPathOfFileAbove and GetDirectoryNameOfFileAbove have reversed args
				new FunctionParameterInfo ("file", "The filename for which to search", MSBuildValueKind.String),
				new FunctionParameterInfo ("startingDirectory", "The starting directory", MSBuildValueKind.String));

			yield return new FunctionInfo (
				"ValueOrDefault",
				"Return the string in parameter 'defaultValue' only if parameter 'conditionValue' is empty, else, return the value conditionValue",
				MSBuildValueKind.String,
				new FunctionParameterInfo ("conditionValue", "The condition", MSBuildValueKind.String),
				new FunctionParameterInfo ("defaultValue", "The default value", MSBuildValueKind.String));
			yield return new FunctionInfo (
				"DoesTaskHostExist",
				"Returns true if a task host exists that can service the requested runtime and architecture",
				MSBuildValueKind.Bool,
				//FIXME type these more strongly for intellisense
				new FunctionParameterInfo ("runtime", "The runtime", MSBuildValueKind.String),
				new FunctionParameterInfo ("architecture", "The architecture", MSBuildValueKind.String));
			yield return new FunctionInfo (
				"EnsureTrailingSlash",
				"If the given path doesn't have a trailing slash then add one. If empty, leave it empty.",
				MSBuildValueKind.String,
				new FunctionParameterInfo ("path", "The path", MSBuildValueKind.String));
			yield return new FunctionInfo (
				"NormalizeDirectory",
				"Gets the canonical full path of the provided directory, with correct directory separators for the current OS and a trailing slash.",
				MSBuildValueKind.String,
				//FIXME params
				new FunctionParameterInfo ("path", "The path components", MSBuildValueKind.String.AsList()));
			yield return new FunctionInfo (
				"NormalizePath",
				"Gets the canonical full path of the provided path, with correct directory separators for the current OS.",
				MSBuildValueKind.String,
				new FunctionParameterInfo ("path", "The path components", MSBuildValueKind.String.AsList ()));
			yield return new FunctionInfo (
				"IsOSPlatform",
				"Whether the current OS platform is the specified OSPlatform value. Case insensitive.",
				MSBuildValueKind.Bool,
				//FIXME stronger typing
				new FunctionParameterInfo ("platformString", "The OSPlatform value", MSBuildValueKind.String));
			yield return new FunctionInfo (
				"IsOsUnixLike",
				"True if current OS is a Unix system.",
				MSBuildValueKind.Bool);
			yield return new FunctionInfo (
				"IsOsBsdLike",
				"True if current OS is a BSD system.",
				MSBuildValueKind.Bool);
			yield return new FunctionInfo (
				"GetCurrentToolsDirectory",
				"Gets the path of the current tools directory",
				MSBuildValueKind.String);
			yield return new FunctionInfo (
				"GetToolsDirectory32",
				"Gets the path of the 32-bit tools directory",
				MSBuildValueKind.String);
			yield return new FunctionInfo (
				"GetToolsDirectory64",
				"Gets the path of the 64-bit tools directory",
				MSBuildValueKind.String);
			yield return new FunctionInfo (
				"GetMSBuildSDKsPath",
				"Gets the path of the MSBuild SDKs directory",
				MSBuildValueKind.String);
			yield return new FunctionInfo (
				"GetVsInstallRoot",
				"Gets the root directory of the Visual Studio installation",
				MSBuildValueKind.String);
			yield return new FunctionInfo (
				"GetProgramFiles32",
				"Gets the path of the 32-bit Program Files directory",
				MSBuildValueKind.String);
			yield return new FunctionInfo (
				"GetMSBuildExtensionsPath",
				"Gets the value of MSBuildExtensionsPath",
				MSBuildValueKind.String);
			yield return new FunctionInfo (
				"IsRunningFromVisualStudio",
				"Whether MSBuild is running from Visual Studio",
				MSBuildValueKind.Bool);
		}

		//FIXME put this on some context class instead of static
		static readonly Dictionary<string, HashSet<string>> permittedFunctions = new Dictionary<string, HashSet<string>> {
			{ "System.Byte", null },
			{ "System.Char", null },
			{ "System.Convert", null },
			{ "System.DateTime", null },
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
				".ctor",
				"CurrentUICulture"
			} }
		};
	}
}
