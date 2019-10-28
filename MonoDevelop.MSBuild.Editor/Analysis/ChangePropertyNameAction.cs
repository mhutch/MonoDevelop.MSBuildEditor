// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	class ChangePropertyNameAction : MSBuildAction
	{
		readonly XElement prop;
		readonly string newName;

		public ChangePropertyNameAction (XElement prop, string newName)
		{
			this.prop = prop;
			this.newName = newName;
		}

		public override string Title => $"Change to {newName}";

		public override Task<IEnumerable<MSBuildActionOperation>> ComputeOperationsAsync (CancellationToken cancellationToken)
		{
			return Task.FromResult<IEnumerable<MSBuildActionOperation>> (
				new MSBuildActionOperation[] {
					new ReplaceTextActionOperation (
						(prop.NameSpan, newName),
						(new TextSpan (prop.ClosingTag.Span.Start+2, prop.Name.Length), newName)
					)
				}
			);
		}
	}
}