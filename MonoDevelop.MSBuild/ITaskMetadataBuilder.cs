// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Language;

namespace MonoDevelop.MSBuild.Schema
{
	public interface ITaskMetadataBuilder
	{
		TaskInfo CreateTaskInfo (
			string typeName, string assemblyName, string assemblyFile,
			string declaredInFile, int declaredAtOffset,
			PropertyValueCollector propVals);
	}
}