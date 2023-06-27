// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Windows;

using Microsoft.VisualStudio.Shell;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Options;

[ComVisible (true)]
[Guid (PackageConsts.TelemetryOptionsPageGuid)]
public class MSBuildTelemetryOptionsPage : UIElementDialogPage
{
	protected override UIElement Child {
		get {
			MSBuildTelemetryOptionsUIElement page = new (this);
			page.Initialize ();
			return page;
		}
	}
}