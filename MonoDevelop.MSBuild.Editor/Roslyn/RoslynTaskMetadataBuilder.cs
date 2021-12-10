// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Util;

namespace MonoDevelop.MSBuild.Editor.Roslyn
{
	[Export (typeof (ITaskMetadataBuilder))]
	class TaskMetadataBuilder : ITaskMetadataBuilder
	{
		[Import]
		public IRoslynCompilationProvider CompilationProvider { get; set; }

		public TaskInfo CreateTaskInfo (
			string typeName, string assemblyName, ExpressionNode assemblyFile, string assemblyFileStr,
			string declaredInFile, int declaredAtOffset,
			IMSBuildEvaluationContext evaluationContext)
		{
			//ignore this, it's redundant
			if (assemblyName != null && assemblyName.StartsWith ("Microsoft.Build.Tasks.v", StringComparison.Ordinal)) {
				return null;
			}

			var tasks = GetTaskAssembly (assemblyName, assemblyFile, assemblyFileStr, declaredInFile, evaluationContext);
			IAssemblySymbol assembly = tasks?.assembly;
			if (assembly == null) {
				//TODO log this?
				return null;
			}

			string asmShortName;
			if (string.IsNullOrEmpty (assemblyName)) {
				asmShortName = Path.GetFileNameWithoutExtension (tasks.Value.path);
			} else {
				asmShortName = new AssemblyName (assemblyName).Name;
			}

			INamedTypeSymbol FindType (INamespaceSymbol ns, string name)
			{
				foreach (var m in ns.GetMembers ()) {
					switch (m) {
					case INamedTypeSymbol ts:
						if (ts.Name == name) {
							return ts;
						}
						continue;
					case INamespaceSymbol childNs:
						var found = FindType (childNs, name);
						if (found != null) {
							return found;
						}
						continue;
					}
				}
				return null;
			}

			var type = assembly.GetTypeByMetadataName (typeName) ?? FindType (assembly.GlobalNamespace, typeName);

			if (type == null) {
				switch (typeName) {
				case "Microsoft.Build.Tasks.RequiresFramework35SP1Assembly":
				case "Microsoft.Build.Tasks.ResolveNativeReference":
					//we don't care about these, they're not present on Mac and they're just noise
					return null;
				}
				LoggingService.LogWarning ($"Did not resolve {typeName}");
				return null;
			}

			var ti = new TaskInfo (
				type.Name, RoslynHelpers.GetDescription (type), type.GetFullName (),
				assemblyName, assemblyFileStr, declaredInFile, declaredAtOffset);
			PopulateTaskInfoFromType (ti, type);
			return ti;
		}

		static void PopulateTaskInfoFromType (TaskInfo ti, INamedTypeSymbol type)
		{
			while (type != null) {
				foreach (var member in type.GetMembers ()) {
					//skip overrides as they will have incorrect accessibility. trust the base definition.
					if (!(member is IPropertySymbol prop) || member.IsOverride || !member.DeclaredAccessibility.HasFlag (Accessibility.Public)) {
						continue;
					}
					if (!ti.Parameters.ContainsKey (prop.Name) && !IsSpecialName (prop.Name)) {
						var pi = ConvertParameter (prop, type);
						if (pi != null) {
							ti.Parameters.Add (prop.Name, pi);
						}
					}
				}

				if (type.BaseType is IErrorTypeSymbol) {
					LoggingService.LogWarning (
						$"Error resolving '{type.BaseType.GetFullName ()}' [{type.BaseType.ContainingAssembly.Name}] (from '{type.GetFullName ()}')");
					break;
				}

				type = type.BaseType;
			}

			bool IsSpecialName (string name) => name == "Log" || name == "HostObject" || name.StartsWith ("BuildEngine", StringComparison.Ordinal);
		}

		static TaskParameterInfo ConvertParameter (IPropertySymbol prop, INamedTypeSymbol type)
		{
			bool isOutput = false, isRequired = false;
			foreach (var att in prop.GetAttributes ()) {
				switch (att.AttributeClass.GetFullName ()) {
				case "Microsoft.Build.Framework.OutputAttribute":
					isOutput = true;
					break;
				case "Microsoft.Build.Framework.RequiredAttribute":
					isRequired = true;
					break;
				}
			}

			var kind = MSBuildValueKind.Unknown;
			ITypeSymbol propType = prop.Type;
			bool isList = false;
			if (propType is IArrayTypeSymbol arr) {
				isList = true;
				propType = arr.ElementType;
			}

			string fullTypeName = propType.GetFullName ();

			switch (fullTypeName) {
			case "System.String":
				kind = MSBuildValueKind.String;
				break;
			case "System.Boolean":
				kind = MSBuildValueKind.Bool;
				break;
			case "System.Int32":
			case "System.UInt32":
			case "System.Int62":
			case "System.UInt64":
				kind = MSBuildValueKind.Int;
				break;
			case "Microsoft.Build.Framework.ITaskItem":
				kind = MSBuildValueKind.UnknownItem;
				break;
			}

			if (kind == MSBuildValueKind.Unknown) {
				//this usually happens because the type has public members with custom types for testing,
				//e.g. NuGetPack.Logger.
				//in general MSBuild does not support custom types on task parameters so they would not be
				//usable anyway.
				//LoggingService.LogDebug ($"Unknown type '{fullTypeName}' for parameter '{type.GetFullName ()}.{prop.Name}'");
				return null;
			}

			if (isList) {
				kind = kind.AsList ();
			}

			return new TaskParameterInfo (prop.Name, RoslynHelpers.GetDescription (prop), isRequired, isOutput, kind);
		}

		Dictionary<(string fileExpr, string asmName, string declaredInFile), (string, IAssemblySymbol)?> resolvedAssemblies
			= new Dictionary<(string, string, string), (string, IAssemblySymbol)?> ();

		protected (string path, IAssemblySymbol assembly)? GetTaskAssembly (
			string assemblyName, ExpressionNode assemblyFile, string assemblyFileStr,
			string declaredInFile, IMSBuildEvaluationContext evaluationContext)
		{
			var key = (assemblyName?.ToLowerInvariant (), assemblyFileStr?.ToLowerInvariant (), declaredInFile.ToLowerInvariant ());
			if (resolvedAssemblies.TryGetValue (key, out (string, IAssemblySymbol)? r)) {
				return r;
			}
			(string, IAssemblySymbol)? taskFile = null;
			try {
				taskFile = ResolveTaskFile (assemblyName, assemblyFile, assemblyFileStr, declaredInFile, evaluationContext);
			} catch (Exception ex) {
				LoggingService.LogError ($"Error loading tasks assembly name='{assemblyName}' file='{taskFile}' in '{declaredInFile}'", ex);
			}
			resolvedAssemblies[key] = taskFile;
			return taskFile;
		}


		(string path, IAssemblySymbol compilation)? ResolveTaskFile (
			string assemblyName, ExpressionNode assemblyFile, string assemblyFileStr,
			string declaredInFile, IMSBuildEvaluationContext evaluationContext)
		{
			if (!(evaluationContext.TryGetProperty (ReservedProperties.BinPath, out var v) && v is MSBuildPropertyValue binPathProp)) {
				LoggingService.LogError ("Task resolver could not get MSBuildBinPath value from evaluationContext");
				return null;
			}
			string binPath = MSBuildEscaping.FromMSBuildPath (((ExpressionText)binPathProp.Value).Value, null);

			if (!string.IsNullOrEmpty (assemblyName)) {
				var name = new AssemblyName (assemblyName);
				string path = Path.Combine (binPath, $"{name.Name}.dll");
				if (!File.Exists (path)) {
					LoggingService.LogWarning ($"Did not find tasks assembly '{assemblyName}'");
					return null;
				}
				return CreateResult (path);
			}

			if (assemblyFile != null) {
				string path = null;
				if (assemblyFile is ExpressionText t) {
					path = MSBuildEscaping.FromMSBuildPath (t.Value, Path.GetDirectoryName (declaredInFile));
					if (!File.Exists (path)) {
						path = null;
					}
				} else {
					var permutations = evaluationContext.EvaluatePathWithPermutation (assemblyFile, Path.GetDirectoryName (declaredInFile));
					foreach (var p in permutations) {
						if (path == null && File.Exists (p)) {
							path = p;
						}
					}
				}
				if (path == null) {
					LoggingService.LogWarning ($"Did not find tasks assembly '{assemblyFileStr}' from file '{declaredInFile}'");
					return null;
				}
				return CreateResult (path);
			}

			return null;

			(string, IAssemblySymbol) CreateResult (string path)
			{
				var name = Path.GetFileNameWithoutExtension (path);

				//FIXME: we need to bundle the xml docs files for these as they are not shipped beside the assemblies in VS
				var paths = new List<string> {
					path,
					Path.Combine (binPath, "Microsoft.Build.Framework.dll"),
					Path.Combine (binPath, "Microsoft.Build.Utilities.Core.dll"),
					Path.Combine (binPath, "Microsoft.Build.Utilities.v4.0.dll"),
					Path.Combine (binPath, "Microsoft.Build.Utilities.v12.0.dll"),
					typeof (object).Assembly.Location
				};

				if (name != "Microsoft.Build.Tasks.Core") {
					paths.Add (Path.Combine (binPath, "Microsoft.Build.Tasks.Core.dll"));
					paths.Add (Path.Combine (binPath, "Microsoft.Build.Tasks.v4.0.dll"));
					paths.Add (Path.Combine (binPath, "Microsoft.Build.Tasks.v12.0.dll"));
				}

				var compilation = CSharpCompilation.Create (
					"__MSBuildEditorTaskResolver",
					references: paths.Where (File.Exists).Select (dll => CompilationProvider.CreateReference (dll)).ToArray ()
				);

				IAssemblySymbol asm = compilation
					.SourceModule
					.ReferencedAssemblySymbols
					.FirstOrDefault (a => string.Equals (a.Name, name, StringComparison.OrdinalIgnoreCase));

				return (path, asm);
			}
		}
	}
}