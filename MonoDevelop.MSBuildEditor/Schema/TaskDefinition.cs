// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Ide.Editor;

namespace MonoDevelop.MSBuildEditor.Schema
{
	class TaskDefinition
	{
		public TaskDefinition (string taskName, TaskInfo info, string typeName, string assemblyName, string assemblyFile, string declaredInFile, DocumentLocation declaredAtLocation)
		{
			TaskName = taskName;
			TypeName = typeName;
			AssemblyName = assemblyName;
			AssemblyFile = assemblyFile;
			DeclaredInFile = declaredInFile;
			DeclaredAtLocation = declaredAtLocation;
			Info = info;
		}

		public string TaskName { get; }
		public string TypeName { get; }
		public string AssemblyName { get; }
		public string AssemblyFile { get; }
		
		public string DeclaredInFile { get; }
		public DocumentLocation DeclaredAtLocation { get; }

		public TaskInfo Info { get; }
	}
}
