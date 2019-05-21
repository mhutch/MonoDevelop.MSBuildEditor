//
// Copyright (c) 2019 Microsoft Corp
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using MonoDevelop.MSBuild.Schema;
using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests
{
	[TestFixture]
	public class SchemaTests
	{
		MSBuildSchemaProvider provider = new MSBuildSchemaProvider ();

		MSBuildSchema LoadCommonTargets () => provider.GetSchema ("microsoft.codeanalysis.targets", null);
		MSBuildSchema LoadCodeAnalysisTargets () => provider.GetSchema ("microsoft.common.targets", null);
		MSBuildSchema LoadVisualBasicTargets () => provider.GetSchema ("microsoft.visualbasic.currentversion.targets", null);
		MSBuildSchema LoadCSharpTargets () => provider.GetSchema ("microsoft.csharp.currentversion.targets", null);
		MSBuildSchema LoadCppTargets () => provider.GetSchema ("microsoft.cpp.targets", null);
		MSBuildSchema LoadNuGetPackTargets () => provider.GetSchema ("nuget.build.tasks.pack.targets", null);
		MSBuildSchema LoadNetSdkTargets () => provider.GetSchema ("sdk.targets", "microsoft.net.sdk");

		[Test]
		public void LoadCoreSchemas ()
		{
			Assert.NotNull (LoadCommonTargets ());
			Assert.NotNull (LoadCodeAnalysisTargets ());
			Assert.NotNull (LoadVisualBasicTargets ());
			Assert.NotNull (LoadCSharpTargets ());
			Assert.NotNull (LoadCppTargets ());
			Assert.NotNull (LoadNuGetPackTargets ());
			Assert.NotNull (LoadNetSdkTargets ());
		}
	}
}
