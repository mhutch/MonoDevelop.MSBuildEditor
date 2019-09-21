// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Roslyn.Utilities;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.FindReferences
{
	internal class NameMetadata
	{
		public string Name { get; }

		public NameMetadata (IDictionary<string, object> data)
		{
			if (!data.TryGetValue (nameof (Name), out var val)) {
				Name = null;
			}
			Name = (string)val;
		}
	}
}