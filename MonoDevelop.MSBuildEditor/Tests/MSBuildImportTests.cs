// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using MonoDevelop.Core.Text;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.MSBuildEditor.Schema;
using NUnit.Framework;

namespace MonoDevelop.MSBuildEditor.Tests
{
	[TestFixture]
	public class MSBuildSchemaTests
	{
		static MSBuildDocument BuildDocument (string filename, string doc)
		{
			doc = doc.Trim ();

			var parsedDoc = (MSBuildParsedDocument) MSBuildParsedDocument.ParseInternal (new Ide.TypeSystem.ParseOptions {
				Content = new StringTextSource (doc),
				FileName = filename
			}, CancellationToken.None);

			return parsedDoc.Document;
		}

		bool HasImported (MSBuildDocument doc, string filename)
		{
			return doc
				.GetDescendentImports ()
				.FirstOrDefault (i => i.IsResolved && i.Filename.EndsWith (filename, StringComparison.OrdinalIgnoreCase))
				?.IsResolved ?? false;
		}

		[Test]
		public void TestSdkStyle ()
		{
			var doc = BuildDocument ("hello.csproj", @"
<Project Sdk=""Microsoft.Net.Sdk"">
	<PropertyGroup>
		<TargetFramework>netstandard2.0</TargetFramework>
	</PropertyGroup>
</Project>
			");

			Assert.IsTrue (HasImported (doc, "Microsoft.Common.targets"));

			//these come via a multivalued import
			Assert.IsTrue (HasImported (doc, "Microsoft.CSharp.CurrentVersion.targets"));
			Assert.IsTrue (HasImported (doc, "Microsoft.CSharp.CrossTargeting.targets"));

			//this comes via a wildcard
			Assert.IsTrue (HasImported (doc, "Microsoft.NuGet.targets"));

			var packageRefItem = doc.GetSchemas ().GetItem ("PackageReference");
			Assert.NotNull (packageRefItem);
			Assert.NotNull (packageRefItem.Description);
		}
	}
}
