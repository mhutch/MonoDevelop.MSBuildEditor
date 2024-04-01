// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Composition;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Tests.Editor.Completion;

[Export (typeof (MSBuildSchemaProvider))]
class TestSchemaProvider : MSBuildSchemaProvider
{
	public override MSBuildSchema GetSchema (string path, string sdk, out IList<MSBuildSchemaLoadError> loadErrors)
	{
		loadErrors = Array.Empty<MSBuildSchemaLoadError> ();

		switch (path) {
		case "EagerElementTrigger.csproj":
			return new MSBuildSchema {
				new PropertyInfo ("Foo", null, valueKind: MSBuildValueKind.Bool)
			};
		}

		return base.GetSchema (path, sdk, out loadErrors);
	}
}
