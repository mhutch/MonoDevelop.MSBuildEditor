// Copyright (c) Microsoft. All rights reserved.
// Copyright (c) 2008 Novell, Inc (http://www.novell.com)
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using MonoDevelop.Components;

namespace MonoDevelop.MSBuildEditor
{
	//for some reason MD doesn't have a public concrete editor tooltip class that just displays a label
	//this is derived from MonoDevelop.SourceEditor.LanguageItemWindow
	class LabelTooltipWindow : TooltipWindow
	{
		public LabelTooltipWindow (DisplayText messageMarkup)
		{
			var label = new FixedWidthWrapLabel {
				Wrap = Pango.WrapMode.WordChar,
				Indent = -20,
				BreakOnCamelCasing = true,
				BreakOnPunctuation = true,
				Markup = messageMarkup.AsMarkup (),
			};

			BorderWidth = 3;
			Add (label);
			UpdateFont (label);

			EnableTransparencyControl = true;
		}

		public int SetMaxWidth (int maxWidth)
		{
			var label = ((FixedWidthWrapLabel)Child);
			label.MaxWidth = maxWidth;
			return label.RealWidth;
		}

		protected override void OnStyleSet (Gtk.Style previous_style)
		{
			base.OnStyleSet (previous_style);
			UpdateFont ((FixedWidthWrapLabel)Child);
		}

		void UpdateFont (FixedWidthWrapLabel label) => label.FontDescription = Ide.IdeServices.FontService.GetFontDescription ("Pad");
	}
}
