// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.MSBuild.Analysis;
using MonoDevelop.MSBuild.Language;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Analyzers;

[TestFixture]
class TaskDiagnosticTests : MSBuildDocumentTest
{

	[Test]
	public void ValidUsingTaskFactory ()
	{
		var source = @"<Project>
  <UsingTask TaskName=""ReplaceFileText"" TaskFactory=""RoslynCodeTaskFactory"" AssemblyFile=""$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll"">
    <ParameterGroup>
      <Filename ParameterType=""System.String"" Required=""true"" />
    </ParameterGroup>
    <Task>
      <Using Namespace=""System"" />
      <Using Namespace=""System.IO"" />
      <Code Type=""Fragment"" Language=""cs"">
        <![CDATA[
        ]]>
      </Code>
    </Task>
  </UsingTask>
</Project>";

		VerifyDiagnostics (source, out _,
			includeCoreDiagnostics: true,
			expectedDiagnostics: []
		);

	}

	[Test]
	public void TaskMissingAssembly ()
	{
		var source = @"<Project>
  <UsingTask TaskName=""ReplaceFileText"" TaskFactory=""CodeTaskFactory"">
    <Task></Task>
  </UsingTask>
</Project>";

		VerifyDiagnostics (source, out _,
			includeCoreDiagnostics: true,
			expectedDiagnostics: [
				new MSBuildDiagnostic (
					CoreDiagnostics.UsingTaskMustHaveAssembly,
					SpanFromLineColLength (source, 2, 4, 9)
				)
			]
		);
	}

	[Test]
	public void UnknownTaskFactory ()
	{
		var source = @"<Project>
  <UsingTask TaskName=""ReplaceFileText"" TaskFactory=""SomeFactory"" AssemblyFile=""SomeAssembly.dll"">
    <Task></Task>
  </UsingTask>
</Project>";

		VerifyDiagnostics (source, out _,
			includeCoreDiagnostics: true,
			expectedDiagnostics: [
				new MSBuildDiagnostic (
					CoreDiagnostics.UnknownTaskFactory,
					SpanFromLineColLength (source, 2, 54, 11),
					messageArgs: [ "SomeFactory" ]
				),
			]
		);
	}

	[Test]
	public void EmptyTaskFactory ()
	{
		var source = @"<Project>
  <UsingTask TaskName=""ReplaceFileText"" TaskFactory="""" AssemblyFile=""SomeAssembly.dll"">
    <Task></Task>
  </UsingTask>
</Project>";

		VerifyDiagnostics (source, out _,
			includeCoreDiagnostics: true,
			expectedDiagnostics: [
				new MSBuildDiagnostic (
					CoreDiagnostics.AttributeEmpty,
					SpanFromLineColLength (source, 2, 41, 11),
					"TaskFactory"
				),
			]
		);
	}

	[Test]
	public void TaskFactoryMissingBody ()
	{
		var source = @"<Project>
  <UsingTask TaskName=""ReplaceFileText"" TaskFactory=""RoslynCodeTaskFactory"" AssemblyFile=""$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll"">
    <ParameterGroup>
      <Filename ParameterType=""System.String"" Required=""true"" />
    </ParameterGroup>
  </UsingTask>
</Project>";

		VerifyDiagnostics (source, out _,
			includeCoreDiagnostics: true,
			expectedDiagnostics: [
				new MSBuildDiagnostic (
					CoreDiagnostics.TaskFactoryMustHaveBody,
					SpanFromLineColLength (source, 2, 4, 9)
				)
			]
		);

	}

	[Test]
	public void ParameterGroupMissingTaskFactory ()
	{
		var source = @"<Project>
  <UsingTask TaskName=""ReplaceFileText"" AssemblyFile=""$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll"">
    <ParameterGroup>
      <Filename ParameterType=""System.String"" Required=""true"" />
    </ParameterGroup>
  </UsingTask>
</Project>";

		VerifyDiagnostics (source, out _,
			includeCoreDiagnostics: true,
			expectedDiagnostics: [
				new MSBuildDiagnostic (
					CoreDiagnostics.ParameterGroupMustHaveFactory,
					SpanFromLineColLength (source, 3, 6, 14)
				)
			]
		);

	}

	[Test]
	public void TaskBodyMissingTaskFactory ()
	{
		var source = @"<Project>
  <UsingTask TaskName=""ReplaceFileText"" AssemblyFile=""$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll"">
    <Task>
      <Using Namespace=""System"" />
      <Using Namespace=""System.IO"" />
      <Code Type=""Fragment"" Language=""cs"">
        <![CDATA[
        ]]>
      </Code>
    </Task>
  </UsingTask>
</Project>";

		VerifyDiagnostics (source, out _,
			includeCoreDiagnostics: true,
			expectedDiagnostics: [
				new MSBuildDiagnostic (
					CoreDiagnostics.TaskBodyMustHaveFactory,
					SpanFromLineColLength (source, 3, 6, 4)
				)
			]
		);
	}
}