// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MonoDevelop.Core;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.MSBuildEditor.Evaluation;
using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class TaskMetadataBuilder
	{
		readonly string binPath;
		readonly MSBuildRootDocument rootDoc;

		Dictionary<string, Projects.Project> projectMap
			= new Dictionary<string, Projects.Project> (StringComparer.OrdinalIgnoreCase);

		public TaskMetadataBuilder (MSBuildRootDocument rootDoc)
		{
			this.rootDoc = rootDoc;
			binPath = rootDoc.RuntimeInformation.GetBinPath ();

			foreach (var project in Ide.IdeApp.Workspace.GetAllProjects()) {
				var outName = project.GetOutputFileName (project.ParentSolution.DefaultConfigurationSelector);
				projectMap[Path.GetFileNameWithoutExtension (outName)] = project;
			}
		}

		Dictionary<(string fileExpr, string asmName, string declaredInFile), (string, Compilation)?> resolvedAssemblies
			= new Dictionary<(string, string, string), (string, Compilation)?> ();

		public TaskInfo CreateTaskInfo (
			string typeName, string assemblyName, string assemblyFile,
			string declaredInFile, Ide.Editor.DocumentLocation declaredAtLocation,
			PropertyValueCollector propVals)
		{
			//ignore this, it's redundant
			if (assemblyName != null && assemblyName.StartsWith ("Microsoft.Build.Tasks.v", StringComparison.Ordinal)) {
				return null;
			}

			Compilation compilation = GetTaskFile (assemblyName, assemblyFile, declaredInFile, propVals)?.compilation;
			if (compilation == null) {
				return null;
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

			// Figure out what assembly to search in.
			// If it's a project from the IDE we're done. If it's not, then we've
			// actually synthesized a container compilation, and the assembly we're
			// really interested in is the first reference.
			IAssemblySymbol asmToSearch = compilation.Assembly;
			if(asmToSearch.Name == "__MSBuildEditorTaskResolver") {
				asmToSearch = compilation.SourceModule.ReferencedAssemblySymbols [0];
			}

			var type = asmToSearch.GetTypeByMetadataName (typeName) ?? FindType (asmToSearch.GlobalNamespace, typeName);

			if (type == null) {
				LoggingService.LogWarning ($"Did not resolve {typeName}");
				return null;
			}

			DisplayText desc = Ambience.GetSummaryMarkup (type);

			var ti = new TaskInfo (type.Name, desc, type.GetFullName (), assemblyName, assemblyFile, declaredInFile, declaredAtLocation);
			PopulateTaskInfoFromType (ti, type);
			return ti;
		}

		static void PopulateTaskInfoFromType (TaskInfo ti, INamedTypeSymbol type)
		{
			while (type != null) {
				foreach (var member in type.GetMembers ()) {
					if (!(member is IPropertySymbol prop) || !member.DeclaredAccessibility.HasFlag (Accessibility.Public)) {
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
			DisplayText propDesc = Ambience.GetSummaryMarkup (prop);

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
				kind = kind.List ();
			}

			return new TaskParameterInfo (prop.Name, propDesc, isRequired, isOutput, kind);
		}

		(string path, Compilation compilation)? GetTaskFile (string assemblyName, string assemblyFile, string declaredInFile, PropertyValueCollector propVals)
		{
			var key = (assemblyName?.ToLowerInvariant (), assemblyFile?.ToLowerInvariant (), declaredInFile.ToLowerInvariant ());
			if (resolvedAssemblies.TryGetValue (key, out (string, Compilation)? r)) {
				return r;
			}
			(string, Compilation)? taskFile = null;
			try {
				taskFile = ResolveTaskFile (assemblyName, assemblyFile, declaredInFile, propVals);
			} catch (Exception ex) {
				LoggingService.LogError ($"Error loading tasks assembly name='{assemblyName}' file='{taskFile}' in '{declaredInFile}'", ex);
			}
			resolvedAssemblies[key] = taskFile;
			return taskFile;
		}


		(string path, Compilation compilation)? ResolveTaskFile (string assemblyName, string assemblyFile, string declaredInFile, PropertyValueCollector propVals)
		{
			if (!string.IsNullOrEmpty (assemblyName)) {
				if (projectMap.TryGetValue(assemblyName, out var project)) {
					return CreateProjectResult (project);
				}
				var name = new AssemblyName (assemblyName);
				string path = Path.Combine (binPath, $"{name.Name}.dll");
				if (!File.Exists (path)) {
					LoggingService.LogWarning ($"Did not find tasks assembly '{assemblyName}'");
					return null;
				}
				return CreateResult (path);
			}

			if (!string.IsNullOrEmpty (assemblyFile)) {
				string path = null;
				if (assemblyFile.IndexOf ('$') < 0) {
					path = Projects.MSBuild.MSBuildProjectService.FromMSBuildPath (Path.GetDirectoryName (declaredInFile), assemblyFile);
					if (!File.Exists (path)) {
						if (projectMap.TryGetValue (Path.GetFileNameWithoutExtension (path), out var project)) {
							return CreateProjectResult (project);
						}
						path = null;
					}
				} else {
					var evalCtx = MSBuildEvaluationContext.Create (rootDoc.RuntimeInformation, rootDoc.Filename, declaredInFile);
					var permutations = evalCtx
						.EvaluatePathWithPermutation (assemblyFile, Path.GetDirectoryName (declaredInFile), propVals);
					foreach (var p in permutations) {
						if (projectMap.TryGetValue (Path.GetFileNameWithoutExtension (p), out var project)) {
							return CreateProjectResult (project);
						}
						if (path == null && File.Exists(p)) {
							path = p;
						}
					}
				}
				if (path == null) {
					LoggingService.LogWarning ($"Did not find tasks assembly '{assemblyFile}' from file '{declaredInFile}'");
					return null;
				}
				return CreateResult (path);
			}

			return null;

			(string, Compilation) CreateResult (string path)
			{
				var compilation = CSharpCompilation.Create (
					"__MSBuildEditorTaskResolver",
					references: new[] {
						RoslynHelpers.GetReference (path),
						RoslynHelpers.GetReference (Path.Combine (binPath, "Microsoft.Build.Framework.dll")),
						RoslynHelpers.GetReference (Path.Combine (binPath, "Microsoft.Build.Utilities.Core.dll")),
						RoslynHelpers.GetReference (Path.Combine (binPath, "Microsoft.Build.Utilities.v4.0.dll")),
						RoslynHelpers.GetReference (Path.Combine (binPath, "Microsoft.Build.Utilities.v12.0.dll")),
						RoslynHelpers.GetReference (typeof (object).Assembly.Location)
					}
				);
				return (path, compilation);
			}

			//FIXME: propagate the async
			(string path, Compilation compilation) CreateProjectResult (Projects.Project project)
			{
				return (
					project.GetOutputFileName (project.ParentSolution.DefaultConfigurationSelector),
					Ide.TypeSystem.TypeSystemService.GetCompilationAsync (project).Result
				);
			}
		}
	}
}
