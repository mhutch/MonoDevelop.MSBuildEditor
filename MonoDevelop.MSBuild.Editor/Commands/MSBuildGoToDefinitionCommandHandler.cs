// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Editor.Host;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.PackageSearch;
using MonoDevelop.Xml.Editor.Completion;

using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;

namespace MonoDevelop.MSBuild.Editor.Commands
{
	[Export (typeof (ICommandHandler))]
	[ContentType (MSBuildContentType.Name)]
	[Name ("MSBuild Go to Definition")]
	class MSBuildGoToDefinitionCommandHandler : ICommandHandler<GoToDefinitionCommandArgs>
	{
		[Import (typeof (IFunctionTypeProvider))]
		internal IFunctionTypeProvider FunctionTypeProvider { get; set; }

		[Import (typeof (IPackageSearchManager))]
		public IPackageSearchManager PackageSearchManager { get; set; }

		[Import (typeof (IMSBuildEditorHost))]
		public IMSBuildEditorHost EditorHost { get; set; }

		[Import]
		public JoinableTaskContext JoinableTaskContext { get; set; }

		public string DisplayName { get; } = "Go to Definition";

		public CommandState GetCommandState (GoToDefinitionCommandArgs args)
		{
			var pos = args.TextView.Caret.Position;
			var parser = BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> ((ITextBuffer2)args.SubjectBuffer);

			MSBuildRootDocument doc = parser.LastParseResult.MSBuildDocument;
			var rr = MSBuildResolver.Resolve (
				parser.GetSpineParser (pos.BufferPosition),
				pos.BufferPosition.Snapshot.GetTextSource (),
				doc, FunctionTypeProvider);

			if (MSBuildNavigation.CanNavigate (doc, pos.BufferPosition, rr)) {
				if (rr.ReferenceKind == MSBuildReferenceKind.NuGetID) {
					return new CommandState (true, displayText: "Open on NuGet.org");
				}
				return CommandState.Available;
			}

			// visible but disabled
			return new CommandState (true, false, false, true);
		}

		public bool ExecuteCommand (GoToDefinitionCommandArgs args, CommandExecutionContext executionContext)
		{
			var pos = args.TextView.Caret.Position;
			var parser = BackgroundParser<MSBuildParseResult>.GetParser<MSBuildBackgroundParser> ((ITextBuffer2)args.SubjectBuffer);

			MSBuildRootDocument doc = parser.LastParseResult.MSBuildDocument;
			var rr = MSBuildResolver.Resolve (
				parser.GetSpineParser (pos.BufferPosition),
				pos.BufferPosition.Snapshot.GetTextSource (),
				doc, FunctionTypeProvider,
				executionContext.OperationContext.UserCancellationToken);

			var nav = MSBuildNavigation.GetNavigation (doc, pos.BufferPosition, rr);

			if (nav.Kind == MSBuildReferenceKind.Target) {
				//FindReferences (() => new MSBuildTargetDefinitionCollector (result.Name), doc);
				return false;
			}

			if (nav.Paths != null) {
				EditorHost.ShowGoToDefinitionResults (nav.Paths);
				return true;
			}

			if (nav.DestFile != null) {
				EditorHost.OpenFile (nav.DestFile, nav.DestOffset);
				return true;
			}

			if (nav.NuGetID != null) {
				OpenNuGetUrl (nav.NuGetID, EditorHost);
				return true;
			}

			return false;
		}

		void OpenNuGetUrl (string nuGetId, IMSBuildEditorHost host)
		{
			Task.Run (async () => {
				var results = await PackageSearchManager.SearchPackageInfo (nuGetId, null, null).ToTask ();

				if (results.Any (r => r.SourceKind == FeedKind.NuGet)) {
					var url = $"https://www.nuget.org/packages/{Uri.EscapeUriString (nuGetId)}";
					Process.Start (url);
				} else {
					await JoinableTaskContext.Factory.SwitchToMainThreadAsync ();
					host.ShowStatusBarMessage ("Package is not from NuGet.org");
				}
			});
		}
	}
}
