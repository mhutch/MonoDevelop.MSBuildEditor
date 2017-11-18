// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MonoDevelop.Core;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.Editor.Extension;

namespace MonoDevelop.MSBuildEditor
{
	public class CompletionDataWithSkipCharAndRetrigger : CompletionData
	{
		char skipchar;

		public CompletionDataWithSkipCharAndRetrigger (string displayText, IconId icon, string description, string completionText, char skipchar)
			: base (displayText, icon, description, completionText)
		{
			this.skipchar = skipchar;
		}

		public override void InsertCompletionText (CompletionListWindow window, ref KeyActions ka, KeyDescriptor descriptor)
		{
			var ext = window.Extension;
			base.InsertCompletionText (window, ref ka, descriptor);

			//FIXME: why is SkipSession private?! it's not even possible to recreate it, as editor.EndSession is private
			var skipSessionType = typeof (EditSession).Assembly.GetType ("MonoDevelop.Ide.Editor.SkipCharSession");
			var skipSession = (EditSession)Activator.CreateInstance (skipSessionType, skipchar);
			ext.Editor.StartSession (skipSession);

			//retrigger completion as soon as the item is committed
			Gtk.Application.Invoke ((s, e) => ext.TriggerCompletion (CompletionTriggerReason.CharTyped));
		}

		public override bool IsCommitCharacter (char keyChar, string partialWord)
		{
			//if it's an exact match for the full display text, it commits
			//if it exactly matches the start of the display text, it doesn't commit
			if (DisplayText.StartsWith (partialWord, StringComparison.Ordinal) && DisplayText.Length > partialWord.Length) {
				if (keyChar == DisplayText [partialWord.Length]) {
					return DisplayText.Length == partialWord.Length + 1;
				}
			}

			return base.IsCommitCharacter (keyChar, partialWord);
		}
	}

}
