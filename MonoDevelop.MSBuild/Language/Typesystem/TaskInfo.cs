// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace MonoDevelop.MSBuild.Language.Typesystem
{
	public class TaskInfo : BaseSymbol, IDeprecatable, ITypedSymbol, IHasHelpUrl
	{
		Dictionary<string, TaskParameterInfo> parameters;

		public IReadOnlyDictionary<string, TaskParameterInfo> Parameters => parameters;

		/// <summary>
		/// Intrinsic task
		/// </summary>
		internal TaskInfo (string name, DisplayText description, TaskParameterInfo[] parameters, string? helpUrl, string? parametersHelpUrl)
			: this (name, description, TaskDeclarationKind.Intrinsic, null, null, null, null, 0, null, helpUrl: helpUrl)
		{
			this.parameters = new Dictionary<string, TaskParameterInfo> ();
			foreach (var p in parameters) {
				this.parameters.Add (p.Name, p);
				p.SetParent (this);
			}
		}

		/// <summary>
		/// All other kinds of task
		/// </summary>
		public TaskInfo (string name, DisplayText description, TaskDeclarationKind declarationKind, string? typeName, string? assemblyName, string? assemblyFile, string? declaredInFile, int declaredAtOffset,
			string? deprecationMessage, Dictionary<string, TaskParameterInfo>? parameters = null, string? helpUrl = null, string? parametersHelpUrl = null
			)
			: base (name, description)
		{
			DeclarationKind = declarationKind;
			TypeName = typeName;
			AssemblyName = assemblyName;
			AssemblyFile = assemblyFile;
			DeclaredInFile = declaredInFile;
			DeclaredAtOffset = declaredAtOffset;
			DeprecationMessage = deprecationMessage;
			this.parameters = parameters ?? new Dictionary<string, TaskParameterInfo> (StringComparer.OrdinalIgnoreCase);
			HelpUrl = helpUrl;
			ParametersHelpUrl = parametersHelpUrl;

			if (parameters is not null) {
				foreach (var p in parameters.Values) {
					p.SetParent (this);
				}
			}
		}

		// this is ONLY for use in schema inference as it collects parameters over the whole document
		// ideally it would use its own collection and later realize it into a readonly TaskInfo
		internal void SetParameter (TaskParameterInfo parameterInfo)
		{
			parameters.Add (parameterInfo.Name, parameterInfo);
			parameterInfo.SetParent (this);
		}

		public TaskDeclarationKind DeclarationKind { get; }
		public string? TypeName { get; }
		public string? AssemblyName { get; }
		public string? AssemblyFile { get; }

		public string? DeclaredInFile { get; }
		public int DeclaredAtOffset { get; }

		public string? DeprecationMessage { get; }

		public MSBuildValueKind ValueKind => MSBuildValueKind.Nothing;

		public CustomTypeInfo? CustomType => null;

		public string? HelpUrl { get; }

		/// <summary>
		/// If provided, used as a fallback for parameters that don't have an explicit help URL.
		/// This is likely a link into the attributes anchor of the task docs page.
		/// </summary>
		public string? ParametersHelpUrl { get; }

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

	public class TaskParameterInfo : VariableInfo, IHasHelpUrl
	{
		public bool IsOutput { get; }
		public bool IsRequired { get; }

		public TaskInfo? Task { get; private set; }

		string? helpUrl;
		public string? HelpUrl => helpUrl ?? Task.ParametersHelpUrl;

		public TaskParameterInfo (
			string name, DisplayText description, bool isRequired,
			bool isOutput, MSBuildValueKind kind, string deprecationMessage = null, string? helpUrl = null)
			: base (name, description, kind, null, null, deprecationMessage)
		{
			IsOutput = isOutput;
			IsRequired = isRequired;
			this.helpUrl = helpUrl;
		}


		internal void SetParent (TaskInfo task) => Task = task;
	}
}
