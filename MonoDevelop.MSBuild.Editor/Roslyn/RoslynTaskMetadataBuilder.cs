// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable annotations

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;

using MonoDevelop.MSBuild.Evaluation;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;

using MonoDevelop.Xml.Logging;

using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;

namespace MonoDevelop.MSBuild.Editor.Roslyn
{
	[Export (typeof (ITaskMetadataBuilder))]
	partial class TaskMetadataBuilder : ITaskMetadataBuilder
	{
		[ImportingConstructor]
		public TaskMetadataBuilder (IRoslynCompilationProvider compilationProvider)
		{
			CompilationProvider = compilationProvider;
		}

		public IRoslynCompilationProvider CompilationProvider { get; }

		public TaskInfo CreateTaskInfo (
			string typeName, string assemblyName, ExpressionNode assemblyFile, string assemblyFileStr,
			string declaredInFile, int declaredAtOffset,
			IMSBuildEvaluationContext evaluationContext,
			ILogger logger)
		{
			//ignore this, it's redundant
			if (assemblyName != null && assemblyName.StartsWith ("Microsoft.Build.Tasks.v", StringComparison.Ordinal)) {
				return null;
			}

			var tasks = GetTaskAssembly (assemblyName, assemblyFile, assemblyFileStr, declaredInFile, evaluationContext, logger);
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
				LogFailedToResolveTaskType(logger, typeName);
				return null;
			}

			var parameters = new Dictionary<string, TaskParameterInfo> (StringComparer.OrdinalIgnoreCase);
			GetTaskInfoFromTask (type, logger, parameters, out string? deprecationMessage);

			return new TaskInfo (
				type.Name, RoslynHelpers.GetDescription (type),
				TaskDeclarationKind.Assembly,
				type.GetFullName (),
				assemblyName, assemblyFileStr,
				declaredInFile, declaredAtOffset,
				deprecationMessage,
				parameters);
		}

		static void GetTaskInfoFromTask (INamedTypeSymbol type, ILogger logger, Dictionary<string, TaskParameterInfo> parameters, out string? deprecationMessage)
		{
			deprecationMessage = null;

			while (type != null) {
				foreach (var member in type.GetMembers ()) {
					//skip overrides as they will have incorrect accessibility. trust the base definition.
					if (member is not IPropertySymbol prop || member.IsOverride || !member.DeclaredAccessibility.HasFlag (Accessibility.Public)) {
						continue;
					}
					if (!parameters.ContainsKey (prop.Name) && !IsSpecialName (prop.Name)) {
						var pi = ConvertParameter (prop, type);
						if (pi != null) {
							parameters.Add (pi.Name, pi);
						}
					}
				}

				if (deprecationMessage is null) {
					foreach (var att in type.GetAttributes ()) {
						switch (att.AttributeClass.Name) {
						case "ObsoleteAttribute":
							if (att.AttributeClass.GetFullName () == "System.ObsoleteAttribute") {
								deprecationMessage = (att.ConstructorArguments.FirstOrDefault ().Value as string) ?? "Deprecated";
							}
							break;
						}
					}
				}

				if (type.BaseType is IErrorTypeSymbol) {
					LogErrorResolvingBaseType(logger, type.BaseType.GetFullName (), type.BaseType.ContainingAssembly.Name, type.GetFullName ());
					break;
				}

				type = type.BaseType;
			}

			bool IsSpecialName (string name) => name == "Log" || name == "HostObject" || name.StartsWith ("BuildEngine", StringComparison.Ordinal);
		}

		static TaskParameterInfo ConvertParameter (IPropertySymbol prop, INamedTypeSymbol type)
		{
			bool isOutput = false, isRequired = false;
			string deprecationMessage = null;

			foreach (var att in prop.GetAttributes ()) {
				switch (att.AttributeClass.Name) {
				case "OutputAttribute":
					if (att.AttributeClass.GetFullName () == "Microsoft.Build.Framework.OutputAttribute") {
						isOutput = true;
					}
					break;
				case "RequiredAttribute":
					if (att.AttributeClass.GetFullName () == "Microsoft.Build.Framework.RequiredAttribute") {
						isRequired = true;
					}
					break;
				case "ObsoleteAttribute":
					if (att.AttributeClass.GetFullName () == "System.ObsoleteAttribute") {
						deprecationMessage = (att.ConstructorArguments.FirstOrDefault ().Value as string) ?? "Deprecated";
					}
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

			// strongly type Message.Importance - although public type is string, it errors if it can't cast to MessageImportance
			if (prop.Name == "Importance" && type.GetFullName () == "Microsoft.Build.Tasks.Message") {
				kind = MSBuildValueKind.Importance;
			}
			else {
				kind = ValueKindExtensions.FromFullTypeName (fullTypeName);
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

			return new TaskParameterInfo (prop.Name, RoslynHelpers.GetDescription (prop), isRequired, isOutput, kind, deprecationMessage);
		}

		Dictionary<(string fileExpr, string asmName, string declaredInFile), (string, IAssemblySymbol)?> resolvedAssemblies
			= new Dictionary<(string, string, string), (string, IAssemblySymbol)?> ();

		protected (string path, IAssemblySymbol assembly)? GetTaskAssembly (
			string assemblyName, ExpressionNode assemblyFile, string assemblyFileStr,
			string declaredInFile, IMSBuildEvaluationContext evaluationContext, ILogger logger)
		{
			var key = (assemblyName?.ToLowerInvariant (), assemblyFileStr?.ToLowerInvariant (), declaredInFile.ToLowerInvariant ());
			if (resolvedAssemblies.TryGetValue (key, out (string, IAssemblySymbol)? r)) {
				return r;
			}
			(string, IAssemblySymbol)? taskFile = null;
			try {
				taskFile = ResolveTaskFile (assemblyName, assemblyFile, assemblyFileStr, declaredInFile, evaluationContext, logger);
			} catch (Exception ex) {
				LogErrorLoadingTasksAssembly (logger, ex, assemblyName, assemblyFileStr, declaredInFile);
			}
			resolvedAssemblies[key] = taskFile;
			return taskFile;
		}


		(string path, IAssemblySymbol compilation)? ResolveTaskFile (
			string assemblyName, ExpressionNode assemblyFile, string assemblyFileStr,
			string declaredInFile, IMSBuildEvaluationContext evaluationContext, ILogger logger)
		{
			if (!evaluationContext.TryGetProperty (ReservedPropertyNames.binPath, out var binPathProp)) {
				LogCouldNotGetBinPath (logger);
				return null;
			}
			string binPath = binPathProp.Value.ToNativePath ();

			if (!string.IsNullOrEmpty (assemblyName)) {
				var name = new AssemblyName (assemblyName);
				string path = Path.Combine (binPath, $"{name.Name}.dll");
				if (!File.Exists (path)) {
					LogDidNotFindTasksAssembly (logger, assemblyName, declaredInFile);
					return null;
				}
				return CreateResult (path);
			}

			if (assemblyFile is not null) {
				string path = null;
				string dllName = null;

				var permutations = evaluationContext.EvaluatePathWithPermutation (assemblyFile, Path.GetDirectoryName (declaredInFile));
				foreach (var p in permutations) {
					if (File.Exists (p)) {
						path = p;
						break;
					}
					if (dllName is null && p.EndsWith (".dll", StringComparison.OrdinalIgnoreCase)) {
						dllName = Path.GetFileName (path);
					}
				}

				if (path is null && dllName is not null) {
					path = FindLocalTasksAssembly (dllName, declaredInFile);
				}

				if (path == null) {
					LogDidNotFindTasksAssemblyFile (logger, assemblyFileStr, declaredInFile);
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

		// HACK to find tasks assembly when editing targets file in a project that builds the task assembly
		// this doesn't update when the assembly changes, assumes that the project name matches the assembly, and assumes that the output dir is `bin`
		static string FindLocalTasksAssembly (string assemblyName, string declaredInFile)
		{
			var directory = Path.GetDirectoryName (declaredInFile);
			if (ExistsInDirectoryAbove (assemblyName + ".csproj", ref directory, 2)) {
				var binDir = Path.Combine (directory, "bin");
				if (Directory.Exists (binDir)) {
					return Directory.EnumerateFiles (binDir, assemblyName + ".dll", SearchOption.AllDirectories).FirstOrDefault ();
				}
			}
			return null;
		}

		static bool ExistsInDirectoryAbove (string filename, ref string directory, int searchLevelsUp = 0)
		{
			do {
				if (directory is null) {
					return false;
				}
				if (File.Exists (Path.Combine (directory, filename))) {
					return true;
				}
				directory = Path.GetDirectoryName (directory);
			} while (searchLevelsUp-- > 0);

			return false;
		}

		[LoggerMessage (EventId = 0, Level = LogLevel.Warning, Message = "Error loading tasks assembly name='{assemblyName}' file='{assemblyFile}' in '{declaredInFile}'")]
		static partial void LogErrorLoadingTasksAssembly (ILogger logger, Exception ex, UserIdentifiable<string> assemblyName, UserIdentifiableFileName assemblyFile, UserIdentifiableFileName declaredInFile);

		[LoggerMessage (EventId = 1, Level = LogLevel.Warning, Message = "Did not find tasks assembly file '{assemblyFile}' from file '{declaredInFile}'")]
		static partial void LogDidNotFindTasksAssemblyFile (ILogger logger, UserIdentifiableFileName assemblyFile, UserIdentifiableFileName declaredInFile);

		[LoggerMessage (EventId = 2, Level = LogLevel.Warning, Message = "Did not find tasks assembly '{assemblyName}' from file '{declaredInFile}'")]
		static partial void LogDidNotFindTasksAssembly (ILogger logger, UserIdentifiable<string> assemblyName, UserIdentifiableFileName declaredInFile);

		[LoggerMessage (EventId = 3, Level = LogLevel.Warning, Message = "Task resolver could not get MSBuildBinPath value from evaluation context'")]
		static partial void LogCouldNotGetBinPath (ILogger logger);

		[LoggerMessage (EventId = 4, Level = LogLevel.Warning, Message = "Error resolving base type {baseTypeName} [{baseTypeAssemblyName}]' for type {typeName}")]
		static partial void LogErrorResolvingBaseType (ILogger logger, string baseTypeName, string baseTypeAssemblyName, string typeName);

		[LoggerMessage (EventId = 5, Level = LogLevel.Warning, Message = "Did not resolve task type '{taskTypeName}'")]
		static partial void LogFailedToResolveTaskType (ILogger logger, string taskTypeName);
	}
}