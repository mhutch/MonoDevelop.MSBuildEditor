// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace MonoDevelop.MSBuild.Editor.VisualStudio.Options;

partial class MSBuildTelemetryOptionsUIElement : UserControl
{
	readonly MSBuildTelemetryOptionsPage optionsPage;
	bool initialized = false;

	public MSBuildTelemetryOptionsUIElement (MSBuildTelemetryOptionsPage optionsPage)
	{
		InitializeComponent ();
		AddHandler (Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler (Hyperlink_RequestNavigate));
		this.optionsPage = optionsPage;
	}

	public void Initialize ()
	{
		enableTelemetry.IsChecked = MSBuildTelemetryOptions.Instance.IsEnabled;
		initialized = true;
	}

	void EnableTelemetry_Toggled (object sender, RoutedEventArgs e)
	{
		if (!initialized) {
			return;
		}

		MSBuildTelemetryOptions.Instance.IsEnabled = (bool)enableTelemetry.IsChecked;
		MSBuildTelemetryOptions.Instance.Save ();
	}

	void Hyperlink_RequestNavigate (object sender, RequestNavigateEventArgs e)
	{
		string uriString;

		if (e.Uri.Scheme == "link") {
			switch (e.Uri.Host) {
			case "privacy-statement":
				uriString = "https://github.com/mhutch/MonoDevelop.MSBuildEditor/blob/main/docs/PrivacyStatement.md";
				break;
			default:
				// TODO: log this
				return;
			}
		} else {
			uriString = e.Uri.AbsoluteUri;
		}

		System.Diagnostics.Process.Start (new System.Diagnostics.ProcessStartInfo (uriString) { UseShellExecute = true });
		e.Handled = true;
	}
}
