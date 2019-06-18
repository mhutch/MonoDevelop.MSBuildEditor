// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Schema
{
	public abstract class TaskMetadataBuilder : ITaskMetadataBuilder
	{
		public TaskInfo CreateTaskInfo (
			string typeName, string assemblyName, string assemblyFile,
			string declaredInFile, int declaredAtOffset,
			PropertyValueCollector propVals)
		{
			//ignore this, it's redundant
			if (assemblyName != null && assemblyName.StartsWith ("Microsoft.Build.Tasks.v", StringComparison.Ordinal)) {
				return null;
			}

			var tasks = GetTaskAssembly (assemblyName, assemblyFile, declaredInFile, propVals);
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

			var desc = new DisplayText (Ambience.GetSummaryMarkup (type), true);

			var ti = new TaskInfo (type.Name, desc, type.GetFullName (), assemblyName, assemblyFile, declaredInFile, declaredAtOffset);
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
			var propDesc = new DisplayText (Ambience.GetSummaryMarkup (prop), true);

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

		protected abstract (string path, IAssemblySymbol assembly)? GetTaskAssembly (string assemblyName, string assemblyFile, string declaredInFile, PropertyValueCollector propVals);
	}
}