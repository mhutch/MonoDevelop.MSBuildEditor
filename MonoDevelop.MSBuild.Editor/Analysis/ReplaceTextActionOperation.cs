// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;

using Microsoft.VisualStudio.Text;

using MonoDevelop.Xml.Dom;

namespace MonoDevelop.MSBuild.Analysis
{
	internal class ReplaceTextActionOperation : MSBuildActionOperation
	{
		readonly (TextSpan span, string newText)[] replacements;

		public ReplaceTextActionOperation (params (TextSpan span, string newText)[] replacements)
		{
			this.replacements = replacements;
		}

		public override void Apply (ITextBuffer document, CancellationToken cancellationToken)
		{
			using (var edit = document.CreateEdit ()) {
				//edit.Replace 
				throw new System.NotImplementedException ();
			}
		}
	}
}