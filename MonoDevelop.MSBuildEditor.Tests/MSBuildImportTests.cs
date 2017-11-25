// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using NUnit.Framework;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Core.Text;
using System.Linq;
using MonoDevelop.MSBuildEditor.Schema;
using System.Threading;

namespace MonoDevelop.MSBuildEditor.Tests
{
	[TestFixture]
	public class MSBuildSchemaTests
	{
		static MSBuildResolveContext BuildContext (string filename, string doc)
		{
			doc = doc.Trim ();

			var parsedDoc = (MSBuildParsedDocument) MSBuildParsedDocument.ParseInternal (new Ide.TypeSystem.ParseOptions {
				Content = new StringTextSource (doc),
				FileName = "filename"
			}, CancellationToken.None);

			return parsedDoc.Context;
		}

		bool HasImported (MSBuildResolveContext ctx, string filename)
		{
			return ctx
				.GetDescendentImports ()
				.FirstOrDefault (i => i.Filename.EndsWith (filename, StringComparison.OrdinalIgnoreCase))
				?.IsResolved ?? false;
		}

		[Test]
		public void TestSdkStyle ()
		{
			var ctx = BuildContext ("hello.csproj", @"
<Project Sdk=""Microsoft.Net.Sdk"">
	<PropertyGroup>
		<TargetFramework>netstandard2.0</TargetFramework>
	</PropertyGroup>
</Project>
			");

			Assert.IsTrue (HasImported (ctx, "Microsoft.Common.targets"));

			//these come via a multivalued import
			Assert.IsTrue (HasImported (ctx, "Microsoft.CSharp.CurrentVersion.targets"));
			Assert.IsTrue (HasImported (ctx, "Microsoft.CSharp.CrossTargeting.targets"));

			//this comes via a wildcard
			Assert.IsTrue (HasImported (ctx, "Microsoft.NuGet.targets"));

			var packageRefItem = ctx.GetSchemas ().GetItem ("PackageReference");
			Assert.NotNull (packageRefItem);
			Assert.NotNull (packageRefItem.Description);
		}
	}
}
