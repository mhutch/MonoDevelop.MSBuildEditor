// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.VisualStudio.LanguageServices.Implementation.LanguageService
{
	internal abstract partial class AbstractLanguageService<TPackage, TLanguageService>
	{
		internal class VsCodeWindowManager : IVsCodeWindowManager, IVsCodeWindowEvents
		{
			private readonly TLanguageService _languageService;
			private readonly IVsCodeWindow _codeWindow;
			private readonly ComEventSink _sink;

			public VsCodeWindowManager (TLanguageService languageService, IVsCodeWindow codeWindow)
			{
				_languageService = languageService;
				_codeWindow = codeWindow;

				_sink = ComEventSink.Advise<IVsCodeWindowEvents> (codeWindow, this);
			}

			private void SetupView (IVsTextView view)
				=> _languageService.SetupNewTextView (view);

			public int AddAdornments ()
			{
				int hr;
				if (ErrorHandler.Failed (hr = _codeWindow.GetPrimaryView (out var primaryView))) {
					Debug.Fail ("GetPrimaryView failed in IVsCodeWindowManager.AddAdornments");
					return hr;
				}

				SetupView (primaryView);
				if (ErrorHandler.Succeeded (_codeWindow.GetSecondaryView (out var secondaryView))) {
					SetupView (secondaryView);
				}

				return VSConstants.S_OK;
			}

			public int OnCloseView (IVsTextView view)
			{
				return VSConstants.S_OK;
			}

			public int OnNewView (IVsTextView view)
			{
				SetupView (view);

				return VSConstants.S_OK;
			}

			public int RemoveAdornments ()
			{
				Shell.ThreadHelper.ThrowIfNotOnUIThread ();

				_sink.Unadvise ();

				return VSConstants.S_OK;
			}
		}
	}
}