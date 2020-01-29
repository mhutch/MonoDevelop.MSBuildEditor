// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.ProjectSystem.VS;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace MonoDevelop.MSBuild.Editor.VisualStudio
{
	[Guid (FactoryGuid)]
	[ComVisible (true)]
	internal sealed class MSBuildEditorFactory : IVsEditorFactory
	{
		public const string FactoryGuid = "351a6d4d-a558-4547-b825-e381869f5c92";

		private System.IServiceProvider serviceProvider;
		private readonly Package package;

		public MSBuildEditorFactory (Package package)
		{
			this.package = package;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage ("Usage", "VSTHRD010:Invoke single-threaded types on Main thread", Justification = "Can't import IProjectThreadingService here")]
		public int CreateEditorInstance (uint grfCreateDoc, string pszMkDocument, string pszPhysicalView, IVsHierarchy pvHier, uint itemid, IntPtr punkDocDataExisting, out IntPtr ppunkDocView, out IntPtr ppunkDocData, out string pbstrEditorCaption, out Guid pguidCmdUI, out int pgrfCDW)
		{
			int result = 0;

			ppunkDocView = IntPtr.Zero;
			ppunkDocData = IntPtr.Zero;
			pbstrEditorCaption = null;
			pguidCmdUI = Guid.Empty;
			pgrfCDW = 0;

			IVsTextLines lines;

			if (punkDocDataExisting != IntPtr.Zero) {
				object rcw = Marshal.GetObjectForIUnknown (punkDocDataExisting);
				lines = rcw as IVsTextLines;

				if (lines == null) {
					if (rcw is IVsTextBufferProvider provider) {
						ThrowOnError (provider.GetTextBuffer (out lines));
					}
				}

				if (lines == null) {
					result = unchecked((int)0x80041FEA);
					goto cleanup;
				}
			} else {
				Type linesType = typeof (IVsTextLines);
				Guid linesIID = linesType.GUID;
				Guid linesCLSID = typeof (VsTextBufferClass).GUID;

				lines = (IVsTextLines)package.CreateInstance (ref linesCLSID, ref linesIID, linesType);
				if (!string.IsNullOrEmpty (pszMkDocument)) {
					if (lines is IVsUserData userData) {
						Guid userDataGuid = typeof (IVsUserData).GUID;
						ThrowOnError (userData.SetData (ref userDataGuid, pszMkDocument));
					}
				}

				if (lines is IObjectWithSite objectWithSite) {
					objectWithSite.SetSite (serviceProvider.GetService (typeof (Microsoft.VisualStudio.OLE.Interop.IServiceProvider)));
				}
			}

			Guid serviceId = new Guid (MSBuildLanguageService.Guid);
			lines.SetLanguageServiceID (ref serviceId);

			if (lines is IVsUserData linesData) {
				Guid detectLanguageSid = new Guid (401831340u, 51220, 4561, 136, 173, 0, 0, 248, 117, 121, 210);
				ThrowOnError (linesData.SetData (ref detectLanguageSid, false));
			}

			if (punkDocDataExisting != IntPtr.Zero) {
				ppunkDocData = punkDocDataExisting;
				Marshal.AddRef (punkDocDataExisting);
			} else {
				ppunkDocData = Marshal.GetIUnknownForObject (lines);
			}

			Type codeWindowType = typeof (IVsCodeWindow);
			Guid codeWindowIID = codeWindowType.GUID;
			Guid codeWindowCLSID = typeof (VsCodeWindowClass).GUID;
			IVsCodeWindow codeWindow = (IVsCodeWindow)package.CreateInstance (ref codeWindowCLSID, ref codeWindowIID, codeWindowType);
			ThrowOnError (codeWindow.SetBuffer (lines));
			ThrowOnError (codeWindow.SetBaseEditorCaption (null));
			ThrowOnError (codeWindow.GetEditorCaption (READONLYSTATUS.ROSTATUS_Unknown, out pbstrEditorCaption));
			pguidCmdUI = new Guid (2335713320u, 25090, 4561, 136, 112, 0, 0, 248, 117, 121, 210);

			ppunkDocView = Marshal.GetIUnknownForObject (codeWindow);
			if (ppunkDocView == IntPtr.Zero) result = unchecked((int)0x80041FEB);

			cleanup:
			if (ppunkDocView == IntPtr.Zero && ppunkDocData != punkDocDataExisting && ppunkDocData != IntPtr.Zero) {
				Marshal.Release (ppunkDocData);
				ppunkDocData = IntPtr.Zero;
			}

			return result;
		}

		public int SetSite (Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
		{
			serviceProvider = new ServiceProvider (psp);
			return HResult.OK;
		}

		public int Close () => HResult.OK;

		public int MapLogicalView (ref Guid rguidLogicalView, out string pbstrPhysicalView)
		{
			pbstrPhysicalView = null;

			Guid LOGVIEWID_Code = new Guid ("7651a701-06e5-11d1-8ebd-00a0c90f26ea");
			Guid LOGVIEWID_Debugging = new Guid ("7651a700-06e5-11d1-8ebd-00a0c90f26ea");
			Guid LOGVIEWID_TextView = new Guid ("7651a703-06e5-11d1-8ebd-00a0c90f26ea");
			Guid LOGVIEWID_Primary = Guid.Empty;

			if (rguidLogicalView == LOGVIEWID_Code || rguidLogicalView == LOGVIEWID_Debugging || rguidLogicalView == LOGVIEWID_TextView || rguidLogicalView == LOGVIEWID_Primary) {
				pbstrPhysicalView = null;
				return 0;
			}

			return unchecked((int)0x80004001);
		}

		private static void ThrowOnError (int hr)
		{
			if (hr < 0) Marshal.ThrowExceptionForHR (hr);
		}
	}
}
