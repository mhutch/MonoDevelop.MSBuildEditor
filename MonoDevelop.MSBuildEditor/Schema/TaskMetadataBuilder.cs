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
using MonoDevelop.MSBuildEditor.Evaluation;
using MonoDevelop.MSBuildEditor.Language;
using BF = System.Reflection.BindingFlags;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class TaskMetadataBuilder
	{
		object metadataService;
		MethodInfo getReferenceMethod;
		string binPath;
		MSBuildRootDocument rootDoc;

		public TaskMetadataBuilder (MSBuildRootDocument rootDoc)
		{
			this.rootDoc = rootDoc;
			binPath = rootDoc.RuntimeInformation.GetBinPath ();

			var metadataServiceType = typeof (Workspace).Assembly.GetType ("Microsoft.CodeAnalysis.Host.IMetadataService");
			getReferenceMethod = metadataServiceType.GetMethod ("GetReference", BF.Instance | BF.NonPublic | BF.Public);
			var services = Ide.TypeSystem.TypeSystemService.Workspace.Services;
			var getServiceMeth = services.GetType ().GetMethod ("GetService", BF.Instance | BF.NonPublic | BF.Public);
			getServiceMeth = getServiceMeth.MakeGenericMethod (metadataServiceType);
			metadataService = getServiceMeth.Invoke (services, null);
		}

		PortableExecutableReference GetReference (string resolvedPath, MetadataReferenceProperties properties = default)
		{
			//HACK: for some reason the cache fails on 7.3 but works on 7.4
			if (BuildInfo.Version.StartsWith ("7.3", StringComparison.Ordinal)) {
				return MetadataReference.CreateFromFile (resolvedPath);
			}
			return (PortableExecutableReference)getReferenceMethod.Invoke (metadataService, new object [] { resolvedPath, properties });
		}

		Dictionary<(string fileExpr, string asmName, string declaredInFile), (string, Compilation)?> resolvedAssemblies
			= new Dictionary<(string, string, string), (string, Compilation)?> ();

		public TaskInfo CreateTaskInfo (
			string typeName, string assemblyName, string assemblyFile,
			string declaredInFile, Ide.Editor.DocumentLocation declaredAtLocation,
			PropertyValueCollector propVals)
		{
			//blacklist this, it's redundant
			if (assemblyName != null && assemblyName.StartsWith ("Microsoft.Build.Tasks.v", StringComparison.Ordinal)) {
				return null;
			}

			var file = GetTaskFile (assemblyName, assemblyFile, declaredInFile, propVals);
			if (file == null) {
				return null;
			}


			var type = file.Value.compilation.GetTypeByMetadataName (typeName);

			//FIXME: do a full namespace-ignoring name lookup. this just special cases common targets
			if (type == null) {
				if (typeName.IndexOf ('.') < 0 && assemblyName != null && assemblyName.StartsWith ("Microsoft.Build.Tasks", StringComparison.Ordinal)) {
					type = file.Value.compilation.GetTypeByMetadataName ("Microsoft.Build.Tasks." + typeName);
				}
			}

			if (type == null) {
				LoggingService.LogWarning ($"Did not resolve {typeName}");
				return null;
			}

			var desc = type.GetDocumentationCommentXml ();

			var ti = new TaskInfo (type.Name, desc, GetFullName (type), assemblyName, assemblyFile, declaredInFile, declaredAtLocation);
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
						ti.Parameters.Add (prop.Name, pi);
					}
				}

				if (type.BaseType is IErrorTypeSymbol) {
					LoggingService.LogWarning (
						$"Error resolving '{GetFullName (type.BaseType)}' [{type.BaseType.ContainingAssembly.Name}] (from '{GetFullName (type)}')");
					break;
				}

				type = type.BaseType;
			}

			bool IsSpecialName (string name) => name == "Log" || name == "HostObject" || name.StartsWith ("BuildEngine", StringComparison.Ordinal);
		}

		static TaskParameterInfo ConvertParameter (IPropertySymbol prop, INamedTypeSymbol type)
		{
			var propDesc = prop.GetDocumentationCommentXml ();
			bool isOutput = false, isRequired = false;
			foreach (var att in prop.GetAttributes ()) {
				switch (GetFullName (att.AttributeClass)) {
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

			string fullTypeName = GetFullName (propType);

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
				LoggingService.LogWarning ($"Unknown type '{fullTypeName}' for parameter '{GetFullName (type)}.{prop.Name}'");
			}

			if (isList) {
				kind = kind.List ();
			}

			return new TaskParameterInfo (prop.Name, propDesc, isRequired, isOutput, kind);
		}

		static string GetFullName (ITypeSymbol symbol)
		{
			var sb = new System.Text.StringBuilder ();
			var ns = symbol.ContainingNamespace;
			while (ns != null && !string.IsNullOrEmpty (ns.Name)) {
				sb.Insert (0, '.');
				sb.Insert (0, ns.Name);
				ns = ns.ContainingNamespace;
			}
			sb.Append (symbol.Name);
			return sb.ToString ();
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
			resolvedAssemblies [key] = taskFile;
			return taskFile;
		}


		(string path, Compilation compilation)? ResolveTaskFile (string assemblyName, string assemblyFile, string declaredInFile, PropertyValueCollector propVals)
		{
			if (!string.IsNullOrEmpty (assemblyName)) {
				var name = new AssemblyName (assemblyName);
				string path = Path.Combine (binPath, $"{name.Name}.dll");
				if (!File.Exists (path)) {
					LoggingService.LogWarning ($"Did not find tasks assembly '{assemblyName}'");
					return null;
				}
				return CreateResult (path);
			}

			if (!string.IsNullOrEmpty (assemblyFile)) {
				string path;
				if (assemblyFile.IndexOf ('$') < 0) {
					path = Projects.MSBuild.MSBuildProjectService.FromMSBuildPath (Path.GetDirectoryName (declaredInFile), assemblyFile);
					if (!File.Exists (path)) {
						path = null;
					}
				} else {
					var evalCtx = MSBuildEvaluationContext.Create (rootDoc.RuntimeInformation, rootDoc.Filename, declaredInFile);
					path = evalCtx
						.EvaluatePathWithPermutation (assemblyFile, Path.GetDirectoryName (declaredInFile), propVals)
						.FirstOrDefault (File.Exists);
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
					"TaskResolver",
					references: new [] {
						GetReference (path),
						GetReference (Path.Combine (binPath, "Microsoft.Build.Framework.dll")),
						GetReference (Path.Combine (binPath, "Microsoft.Build.Utilities.Core.dll")),
						GetReference (Path.Combine (binPath, "Microsoft.Build.Utilities.v4.0.dll")),
						GetReference (Path.Combine (binPath, "Microsoft.Build.Utilities.v12.0.dll")),
						GetReference (typeof (object).Assembly.Location)
					}
				);
				return (path, compilation);
			}
		}
	}
}
