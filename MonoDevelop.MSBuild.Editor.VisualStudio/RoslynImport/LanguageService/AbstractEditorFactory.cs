// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable annotations

using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

using Roslyn.Utilities;

namespace Microsoft.VisualStudio.LanguageServices.Implementation
{
	/// <summary>
	/// The base class of both the Roslyn editor factories.
	/// </summary>
	internal abstract class AbstractEditorFactory : IVsEditorFactory, IVsEditorFactory4
	{
		private readonly IComponentModel _componentModel;
		private Microsoft.VisualStudio.OLE.Interop.IServiceProvider? _oleServiceProvider;
		private bool _encoding;

		protected AbstractEditorFactory (IComponentModel componentModel)
			=> _componentModel = componentModel;

		protected abstract string ContentTypeName { get; }
		protected abstract string LanguageName { get; }

		public void SetEncoding (bool value)
			=> _encoding = value;

		int IVsEditorFactory.Close ()
			=> VSConstants.S_OK;

		public int CreateEditorInstance (
			uint grfCreateDoc,
			string pszMkDocument,
			string? pszPhysicalView,
			IVsHierarchy vsHierarchy,
			uint itemid,
			IntPtr punkDocDataExisting,
			out IntPtr ppunkDocView,
			out IntPtr ppunkDocData,
			out string pbstrEditorCaption,
			out Guid pguidCmdUI,
			out int pgrfCDW)
		{
			Shell.ThreadHelper.ThrowIfNotOnUIThread ();
			Contract.ThrowIfNull (_oleServiceProvider);

			ppunkDocView = IntPtr.Zero;
			ppunkDocData = IntPtr.Zero;
			pbstrEditorCaption = string.Empty;
			pguidCmdUI = Guid.Empty;
			pgrfCDW = 0;

			var physicalView = pszPhysicalView ?? "Code";
			IVsTextLines? textLines = null;

			// Is this document already open? If so, let's see if it's a IVsTextLines we should re-use. This allows us
			// to properly handle multiple windows open for the same document.
			if (punkDocDataExisting != IntPtr.Zero) {
				var docDataExisting = Marshal.GetObjectForIUnknown (punkDocDataExisting);

				textLines = docDataExisting as IVsTextLines;

				if (textLines is null && docDataExisting is IVsTextBufferProvider textBufferProvider) {
					textBufferProvider.GetTextBuffer (out textLines);
				}

				if (textLines == null) {
					// We are incompatible with the existing doc data
					return VSConstants.VS_E_INCOMPATIBLEDOCDATA;
				}
			}

			var editorAdaptersFactoryService = _componentModel.GetService<IVsEditorAdaptersFactoryService> ();

			// Do we need to create a text buffer?
			if (textLines == null) {
				textLines = GetDocumentData (grfCreateDoc, pszMkDocument, vsHierarchy, itemid) as IVsTextLines;
				Contract.ThrowIfNull (textLines, $"Failed to get document data for {pszMkDocument}");
			}

			// If the text buffer is marked as read-only, ensure that the padlock icon is displayed
			// next the new window's title and that [Read Only] is appended to title.
			var readOnlyStatus = READONLYSTATUS.ROSTATUS_NotReadOnly;
			if (ErrorHandler.Succeeded (textLines.GetStateFlags (out var textBufferFlags)) &&
				0 != (textBufferFlags & ((uint)BUFFERSTATEFLAGS.BSF_FILESYS_READONLY | (uint)BUFFERSTATEFLAGS.BSF_USER_READONLY))) {
				readOnlyStatus = READONLYSTATUS.ROSTATUS_ReadOnly;
			}

			switch (physicalView) {

			case "Code":

				var codeWindow = editorAdaptersFactoryService.CreateVsCodeWindowAdapter (_oleServiceProvider);
				codeWindow.SetBuffer (textLines);

				codeWindow.GetEditorCaption (readOnlyStatus, out pbstrEditorCaption);

				ppunkDocView = Marshal.GetIUnknownForObject (codeWindow);
				pguidCmdUI = VSConstants.GUID_TextEditorFactory;

				break;

			default:

				return VSConstants.E_INVALIDARG;
			}

			ppunkDocData = Marshal.GetIUnknownForObject (textLines);

			return VSConstants.S_OK;
		}

		public object GetDocumentData (uint grfCreate, string pszMkDocument, IVsHierarchy pHier, uint itemid)
		{
			Contract.ThrowIfNull (_oleServiceProvider);
			var editorAdaptersFactoryService = _componentModel.GetService<IVsEditorAdaptersFactoryService> ();
			var contentTypeRegistryService = _componentModel.GetService<IContentTypeRegistryService> ();
			var contentType = contentTypeRegistryService.GetContentType (ContentTypeName);
			var textBuffer = editorAdaptersFactoryService.CreateVsTextBufferAdapter (_oleServiceProvider, contentType);

			if (_encoding) {
				if (textBuffer is IVsUserData userData) {
					// The editor shims require that the boxed value when setting the PromptOnLoad flag is a uint
					var hresult = userData.SetData (
						VSConstants.VsTextBufferUserDataGuid.VsBufferEncodingPromptOnLoad_guid,
						(uint)__PROMPTONLOADFLAGS.codepagePrompt);

					Marshal.ThrowExceptionForHR (hresult);
				}
			}

			return textBuffer;
		}

		public object GetDocumentView (uint grfCreate, string pszPhysicalView, IVsHierarchy pHier, IntPtr punkDocData, uint itemid)
		{
			// There is no scenario need currently to implement this method.
			throw new NotImplementedException ();
		}

		public string GetEditorCaption (string pszMkDocument, string pszPhysicalView, IVsHierarchy pHier, IntPtr punkDocData, out Guid pguidCmdUI)
		{
			// It is not possible to get this information without initializing the designer.
			// There is no other scenario need currently to implement this method.
			throw new NotImplementedException ();
		}

		public bool ShouldDeferUntilIntellisenseIsReady (uint grfCreate, string pszMkDocument, string pszPhysicalView) => false;

		public int MapLogicalView (ref Guid rguidLogicalView, out string? pbstrPhysicalView)
		{
			pbstrPhysicalView = null;

			if (rguidLogicalView == VSConstants.LOGVIEWID.Primary_guid ||
				rguidLogicalView == VSConstants.LOGVIEWID.Debugging_guid ||
				rguidLogicalView == VSConstants.LOGVIEWID.Code_guid ||
				rguidLogicalView == VSConstants.LOGVIEWID.TextView_guid) {
				return VSConstants.S_OK;
			} else {
				return VSConstants.E_NOTIMPL;
			}
		}

		int IVsEditorFactory.SetSite (Microsoft.VisualStudio.OLE.Interop.IServiceProvider psp)
		{
			_oleServiceProvider = psp;
			return VSConstants.S_OK;
		}
	}
}
