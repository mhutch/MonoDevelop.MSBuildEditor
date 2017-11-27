// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Ide.Editor.Extension;
using MonoDevelop.Ide.FindInFiles;
using MonoDevelop.MSBuildEditor.Language;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildHighlightUsagesExtension : AbstractUsagesExtension<MSBuildResolveResult>
	{
		//FIXME docs say this is called on background thread but not true
		protected override Task<IEnumerable<MemberReference>> GetReferencesAsync (MSBuildResolveResult resolveResult, CancellationToken token)
		{
			var ext = Editor.GetContent<MSBuildTextEditorExtension> ();
			var doc = ext.GetDocument ();
			var collector = MSBuildReferenceCollector.Create (resolveResult);

			//FIXME: it should be possible to run this async, all the args are immutable
			collector.Run (doc.XDocument, doc.FileName, doc.Text, doc.Context);

			//FIXME ReferenceUsageType = ReferenceUsageType
			return Task.FromResult (
				collector.Results.Select (r => new MemberReference (r, doc.FileName, r.Offset, r.Length))
			);
		}

		protected override Task<MSBuildResolveResult> ResolveAsync (CancellationToken token)
		{
			var ext = Editor.GetContent<MSBuildTextEditorExtension> ();

			//FIXME can we cache this? maybe make it async?
			var rr = ext.ResolveCurrentLocation ();

			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Metadata:
				if (rr.ReferenceItemName != null) {
					return Task.FromResult (rr);
				}
				break;
			case MSBuildReferenceKind.Item:
			case MSBuildReferenceKind.Property:
			case MSBuildReferenceKind.Target:
				return Task.FromResult (rr);
			}
			return null;
		}
	}
}
