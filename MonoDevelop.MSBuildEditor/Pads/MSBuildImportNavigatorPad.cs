// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MonoDevelop.Components;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.MSBuildEditor.Pads
{
	class MSBuildImportNavigatorPad : PadContent
	{
		Control control;

		public override Control Control => control;

		protected override void Initialize (IPadWindow window)
		{
			base.Initialize (window);
			control = new XwtControl (new MSBuildImportNavigator ());
		}

		public override void Dispose ()
		{
			base.Dispose ();
			control.Dispose ();
			control = null;
		}
	}
}