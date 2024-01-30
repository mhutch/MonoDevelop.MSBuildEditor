// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	public class TaskInfo : BaseSymbol, IDeprecatable
	{
		public Dictionary<string, TaskParameterInfo> Parameters { get; }


		/// <summary>
		/// Intrinsic task
		/// </summary>
		internal TaskInfo (string name, DisplayText description, params TaskParameterInfo[] parameters) : this (name, description, TaskDeclarationKind.Intrinsic, null, null, null, null, 0, null)
		{
			foreach (var p in parameters) {
				Parameters.Add (p.Name, p);
			}
		}

		/// <summary>
		/// All other kinds of task
		/// </summary>
		public TaskInfo (string name, DisplayText description, TaskDeclarationKind declarationKind, string? typeName, string? assemblyName, string? assemblyFile, string? declaredInFile, int declaredAtOffset, string? deprecationMessage, Dictionary<string, TaskParameterInfo>? parameters = null)
			: base (name, description)
		{
			DeclarationKind = declarationKind;
			TypeName = typeName;
			AssemblyName = assemblyName;
			AssemblyFile = assemblyFile;
			DeclaredInFile = declaredInFile;
			DeclaredAtOffset = declaredAtOffset;
			DeprecationMessage = deprecationMessage;
			Parameters = parameters ?? new Dictionary<string, TaskParameterInfo> (StringComparer.OrdinalIgnoreCase);
		}

		public TaskDeclarationKind DeclarationKind { get; }
		public string? TypeName { get; }
		public string? AssemblyName { get; }
		public string? AssemblyFile { get; }

		public string? DeclaredInFile { get; }
		public int DeclaredAtOffset  { get; }

		public string? DeprecationMessage { get; }

		// TODO: check for invalid chars in name and namespace
		internal static bool ValidateTaskName (string fullTaskName, out string taskName, out string taskNamespace)
		{
			if (string.IsNullOrWhiteSpace (fullTaskName)) {
				taskNamespace = null;
				taskName = null;
				return false;
			}

			fullTaskName = fullTaskName.Trim ();

			int nameIdx = fullTaskName.LastIndexOf ('.');
			if (nameIdx < 0) {
				taskName = fullTaskName;
				taskNamespace = null;
				return true;
			}

			if (nameIdx == 0 || nameIdx == fullTaskName.Length - 1) {
				taskName = null;
				taskNamespace = null;
				return false;
			}

			taskNamespace = fullTaskName.Substring (0, nameIdx);
			taskName = fullTaskName.Substring (nameIdx + 1);
			return true;
		}
	}

	public enum TaskDeclarationKind
	{
		Intrinsic,
		Assembly,
		AssemblyUnresolved,
		TaskFactoryExplicitParameters,
		TaskFactoryImplicitParameters,
		Inferred
	}

	public class TaskParameterInfo : VariableInfo
	{
		public bool IsOutput { get; }
		public bool IsRequired { get; }

		public TaskParameterInfo (
			string name, DisplayText description, bool isRequired,
			bool isOutput, MSBuildValueKind kind, string deprecationMessage = null)
			: base (name, description, kind, null, null, deprecationMessage)
		{
			IsOutput = isOutput;
			IsRequired = isRequired;
		}
	}
}
