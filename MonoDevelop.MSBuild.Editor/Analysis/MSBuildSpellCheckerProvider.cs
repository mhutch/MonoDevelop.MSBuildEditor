// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.Composition;
using System.Diagnostics;

using Microsoft.VisualStudio.Text;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	// this adds a level of indirection so we can swap out the method by which a spell checker is obtained
	// once we have some kind of document scoping story. currently it's just bound to the buffer.
	[Export]
	class MSBuildSpellCheckerProvider
	{
		public MSBuildSpellChecker GetSpellChecker (ITextBuffer buffer)
		{
			Debug.Assert (buffer.ContentType.IsOfType (MSBuildContentType.Name));

			return buffer.Properties.GetOrCreateSingletonProperty (typeof(MSBuildSpellChecker), () => {
				buffer.ContentTypeChanged += ContentTypeChanged;
				return new MSBuildSpellChecker ();
			});
		}

		void ContentTypeChanged (object sender, ContentTypeChangedEventArgs e)
		{
			if (!e.AfterContentType.IsOfType (MSBuildContentType.Name)) {
				var buffer = (ITextBuffer)sender;
				buffer.ContentTypeChanged -= ContentTypeChanged;
				buffer.Properties.RemoveProperty (typeof (MSBuildSpellChecker));
			}
		}
	}
}
