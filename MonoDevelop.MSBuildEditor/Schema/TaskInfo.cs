// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using MonoDevelop.Ide.Editor;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class TaskInfo : BaseInfo
	{
		public Dictionary<string, TaskParameterInfo> Parameters { get; } = new Dictionary<string, TaskParameterInfo> ();

		public TaskInfo (string name, DisplayText description, string typeName, string assemblyName, string assemblyFile, string declaredInFile, DocumentLocation declaredAtLocation)
			: base (name, description)
		{
			TypeName = typeName;
			AssemblyName = assemblyName;
			AssemblyFile = assemblyFile;
			DeclaredInFile = declaredInFile;
			DeclaredAtLocation = declaredAtLocation;
		}

		public string TypeName { get; }
		public string AssemblyName { get; }
		public string AssemblyFile { get; }

		public string DeclaredInFile { get; }
		public DocumentLocation DeclaredAtLocation { get; }

		public bool IsInferred => DeclaredInFile == null;

		public bool ForceInferAttributes { get; set; }
	}

	class TaskParameterInfo : ValueInfo
	{
		public bool IsOutput { get; }
		public bool IsRequired { get; }

		public TaskParameterInfo (string name, DisplayText description, bool isRequired, bool isOutput, MSBuildValueKind kind) : base (name, description, kind)
		{
			IsOutput = isOutput;
			IsRequired = isRequired;
		}
	}
}
