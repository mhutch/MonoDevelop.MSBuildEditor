// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoDevelop.Components.Commands;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Editor;
using MonoDevelop.Ide.FindInFiles;
using MonoDevelop.MSBuildEditor.Language;
using MonoDevelop.MSBuildEditor.PackageSearch;
using MonoDevelop.MSBuildEditor.Schema;
using MonoDevelop.Xml.Completion;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Parser;
using ProjectFileTools.NuGetSearch.Contracts;
using ProjectFileTools.NuGetSearch.Feeds;
using ProjectFileTools.NuGetSearch.Feeds.Disk;
using ProjectFileTools.NuGetSearch.Feeds.Web;
using ProjectFileTools.NuGetSearch.IO;
using ProjectFileTools.NuGetSearch.Search;

namespace MonoDevelop.MSBuildEditor
{
	class MSBuildTextEditorExtension : BaseXmlEditorExtension
	{
		public static readonly string MSBuildMimeType = "application/x-msbuild";

		public IPackageSearchManager PackageSearchManager { get; set; }

		protected override void Initialize ()
		{
			base.Initialize ();

			//we don't have a MEF composition here, set it up manually
			PackageSearchManager = new PackageSearchManager (
				new MonoDevelopPackageFeedRegistry (),
				new PackageFeedFactorySelector (new IPackageFeedFactory [] {
					new NuGetDiskFeedFactory (new FileSystem()),
					new NuGetV3ServiceFeedFactory (new WebRequestFactory()),
				})
			);
		}

		public MSBuildParsedDocument GetDocument ()
		{
			return (MSBuildParsedDocument)DocumentContext.ParsedDocument;
		}

		public override Task<ICompletionDataList> HandleCodeCompletionAsync (CodeCompletionContext completionContext, CompletionTriggerInfo triggerInfo, CancellationToken token = default (CancellationToken))
		{
			var doc = GetDocument ();
			if (doc != null) {
				var rr = ResolveCurrentLocation ();
				if (rr?.LanguageElement != null) {
					var expressionCompletion = HandleExpressionCompletion (rr, token);
					if (expressionCompletion != null) {
						return expressionCompletion;
					}
				}
			}

			return base.HandleCodeCompletionAsync (completionContext, triggerInfo, token);
		}

		internal MSBuildResolveResult ResolveAt (int offset)
		{
			if (Tracker == null || GetDocument () == null) {
				return null;
			}
			Tracker.UpdateEngine (offset);
			return MSBuildResolver.Resolve (Tracker.Engine, Editor.CreateDocumentSnapshot ());
		}

		MSBuildResolveResult ResolveCurrentLocation ()
		{
			if (Tracker == null || GetDocument () == null) {
				return null;
			}
			Tracker.UpdateEngine ();
			return MSBuildResolver.Resolve(Tracker.Engine, Editor.CreateDocumentSnapshot ());
		}

		protected override Task<CompletionDataList> GetElementCompletions (CancellationToken token)
		{
			var list = new CompletionDataList ();
			AddMiscBeginTags (list);

			var rr = ResolveCurrentLocation ();
			if (rr != null) {
				var doc = GetDocument ();
				foreach (var el in rr.GetElementCompletions (doc.Context.GetSchemas ())) {
					list.Add (new MSBuildCompletionData (el, doc.Context, rr, XmlCompletionData.DataType.XmlElement));
				}
			}

			return Task.FromResult (list);
		}

		static Task<CompletionDataList> ToCompletionList (IEnumerable<BaseInfo> infos, MSBuildResolveContext ctx, MSBuildResolveResult rr, XmlCompletionData.DataType type)
		{
			var data = infos.Select (i => new MSBuildCompletionData (i, ctx, rr, type));
			return Task.FromResult (new CompletionDataList (data));
		}

		protected override Task<CompletionDataList> GetAttributeCompletions (IAttributedXObject attributedOb,
			Dictionary<string, string> existingAtts, CancellationToken token)
		{
			var rr = ResolveCurrentLocation ();
			if (rr?.LanguageElement == null)
				return null;

			var doc = GetDocument ();
			var list = new CompletionDataList ();
			foreach (var att in rr.GetAttributeCompletions (doc.Context.GetSchemas (), doc.ToolsVersion)) {
				list.Add (new MSBuildCompletionData (att, doc.Context, rr, XmlCompletionData.DataType.XmlAttribute));
			}

			return Task.FromResult (list);
		}

		string GetTargetFramework (MSBuildParsedDocument doc)
		{
			return doc.Frameworks.FirstOrDefault ()?.ToString () ?? ".NETStandard,Version=v2.0";
		}

		Task<ICompletionDataList> GetPackageNameCompletions (MSBuildParsedDocument doc, int startIdx, int triggerLength)
		{
			string name = ((IXmlParserContext)Tracker.Engine).KeywordBuilder.ToString ();
			if (string.IsNullOrWhiteSpace (name)) {
				return null;
			}
			return Task.FromResult<ICompletionDataList> (
				new PackageSearchCompletionDataList (
					name,
					(n) => PackageSearchManager.SearchPackageNames (n.ToLower (), GetTargetFramework (doc))
				) {
					TriggerWordStart = startIdx,
					TriggerWordLength = triggerLength
				}
			);
		}

		Task<ICompletionDataList> GetPackageVersionCompletions (MSBuildParsedDocument doc, MSBuildResolveResult rr, int startIdx, int triggerLength)
		{
			var name = rr.XElement.Attributes.FirstOrDefault (a => a.Name.FullName == "Include")?.Value;
			if (string.IsNullOrEmpty (name)) {
				return null;
			}
			return Task.FromResult<ICompletionDataList> (
				new PackageSearchCompletionDataList (
					PackageSearchManager.SearchPackageVersions (name, GetTargetFramework (doc))
				) {
					TriggerWordStart = startIdx,
					TriggerWordLength = triggerLength
				}
			);
		}

		Task<ICompletionDataList> GetSdkCompletions (int triggerLength, CancellationToken token)
		{
			var list = new CompletionDataList { TriggerWordLength = triggerLength };
			var doc = GetDocument ();
			if (doc == null) {
				return null;
			}

			var sdks = new HashSet<string> ();

			var resolver = doc.SdkResolver;
			foreach (var sdk in resolver.GetRegisteredSdks ()) {
				if (sdks.Add (sdk.Name)) {
					list.Add (Path.GetFileName (sdk.Name));
				}
			}

			//TODO: how can we find SDKs in the non-default locations?
			return Task.Run<ICompletionDataList> (() => {
				foreach (var d in Directory.GetDirectories (resolver.DefaultSdkPath)) {
					string name = Path.GetFileName (d);
					if (sdks.Add (name)) {
						list.Add (name);
					}
				}
				return list;
			}, token);
		}

		Task<ICompletionDataList> HandleExpressionCompletion (MSBuildResolveResult rr, CancellationToken token)
		{
			var doc = GetDocument ();

			if (!ExpressionCompletion.IsPossibleExpressionCompletionContext (Tracker.Engine)) {
				return null;
			}

			string expression = GetAttributeOrElementTextToCaret ();

			var triggerState = ExpressionCompletion.GetTriggerState (expression, out int triggerLength);
			if (triggerState == ExpressionCompletion.TriggerState.None) {
				return null;
			}

			var info = rr.GetElementOrAttributeValueInfo (doc.Context.GetSchemas ());
			if (info == null) {
				return null;
			}

			var kind = MSBuildCompletionExtensions.InferValueKindIfUnknown (info);

			if (!ExpressionCompletion.ValidateListPermitted (ref triggerState, info.ValueSeparators, kind)) {
				return null;
			}

			bool allowExpressions = kind.AllowExpressions ();

			kind = kind.GetScalarType ();

			switch (kind) {
			case MSBuildValueKind.NuGetID:
				return GetPackageNameCompletions (doc, Editor.CaretOffset - triggerLength, triggerLength);
			case MSBuildValueKind.NuGetVersion:
				return GetPackageVersionCompletions (doc, rr, Editor.CaretOffset - triggerLength, triggerLength);
			case MSBuildValueKind.Sdk:
				return GetSdkCompletions (triggerLength, token);
			}

			var list = new CompletionDataList { TriggerWordLength = triggerLength };
			list.AutoSelect = false;

			//TODO: better metadata support

			var cinfos = ExpressionCompletion.GetCompletionInfos (triggerState, kind, doc.Context.GetSchemas ());
			foreach (var ci in cinfos) {
				list.Add (new MSBuildCompletionData (ci, doc.Context, rr, XmlCompletionData.DataType.XmlAttributeValue));
			}

			if (allowExpressions && triggerState == ExpressionCompletion.TriggerState.Value) {
				list.Add (new CompletionDataWithSkipCharAndRetrigger ("$(", "md-variable", "Property value reference", "$(|)", ')'));
				list.Add (new CompletionDataWithSkipCharAndRetrigger ("@(", "md-variable", "Item list reference", "$(|)", ')'));
			}

			if (list.Count > 0) {
				return Task.FromResult<ICompletionDataList> (list);
			}
			return null;
		}

		//FIXME: move this down to XML layer
		string GetAttributeOrElementTextToCaret ()
		{
			int currentPosition = Editor.CaretOffset;
			int lineStart = Editor.GetLine (Editor.CaretLine).Offset;
			int expressionStart = currentPosition - Tracker.Engine.CurrentStateLength;
			if (Tracker.Engine.CurrentState is XmlAttributeValueState && Tracker.Engine.GetAttributeValueDelimiter () != 0) {
				expressionStart += 1;
			}
			int start = Math.Max (expressionStart, lineStart);
			var expression = Editor.GetTextAt (start, currentPosition - start);
			return expression;
		}

		public IEnumerable<T> GetAnnotationsAtLocation<T> (DocumentLocation location)
		{
			var doc = GetDocument ();
			if (doc == null) {
				return null;
			}

			var xobj = FindNodeAtLocation (doc.XDocument, location);
			if (xobj == null) {
				return null;
			}

			return doc.Context.Annotations
				.GetMany<T> (xobj)
				.Where (a => !(a is IRegionAnnotation ra) || ra.Region.Contains (location));
		}

		[CommandHandler (Refactoring.RefactoryCommands.GotoDeclaration)]
		void GotoDefinition()
		{
			var annotations = GetAnnotationsAtLocation<NavigationAnnotation> (Editor.CaretLocation);

			var files = new List<string> ();
			foreach (var nav in annotations) {
				if (Directory.Exists (nav.Path)) {
					foreach (var f in Directory.EnumerateFiles (nav.Path, "*", SearchOption.AllDirectories)) {
						if (f.EndsWith (".targets", StringComparison.OrdinalIgnoreCase) || f.EndsWith (".props", StringComparison.OrdinalIgnoreCase))
							files.Add (f);
					}
				}
				if (File.Exists (nav.Path)) {
					files.Add (nav.Path);
				}
			}


			if (files.Count == 1) {
				//FIXME: can we open the doc with the same context i.e. as a child of this?
				// That would improve drilldown and find refs accuracy but would run into issues
				// when drilling down into the same child from multiple parents.
				// We'd probably need something like the shared projects context dropdown.
				IdeApp.Workbench.OpenDocument (files[0], DocumentContext.Project, true);
				return;
			}

			using (var monitor = IdeApp.Workbench.ProgressMonitors.GetSearchProgressMonitor (true, true)) {
				foreach (var file in files) {
					var fp = new FileProvider (file);
					monitor.ReportResult (new SearchResult (fp, 0, 0));
				}
			}
		}

		[CommandUpdateHandler (Refactoring.RefactoryCommands.GotoDeclaration)]
		void UpdateGotoDefinition (CommandInfo info)
		{
			info.Enabled = GetAnnotationsAtLocation<NavigationAnnotation> (Editor.CaretLocation).Any ();
		}

		//FIXME: binary search
		XObject FindNodeAtLocation (XContainer container, DocumentLocation location)
		{
			var node = container.AllDescendentNodes.FirstOrDefault (n => n.Region.Contains (location));
			if (node != null) {
				if (node is IAttributedXObject attContainer) {
					var att = attContainer.Attributes.FirstOrDefault (n => n.Region.Contains (location));
					if (att != null) {
						return att;
					}
				}
			}
			return node;
		}

		[CommandHandler (Refactoring.RefactoryCommands.FindReferences)]
		void FindReferences ()
		{
			var rr = ResolveCurrentLocation ();
			var doc = GetDocument ();

			var monitor = IdeApp.Workbench.ProgressMonitors.GetSearchProgressMonitor (true, true);

			var tasks = new List<Task> ();

			foreach (var import in doc.Context.GetDescendentImports ()) {
				if (!import.IsResolved || !File.Exists (import.Filename)) {
					continue;
				}
				tasks.Add (Task.Run (() => {
					try {
						var xmlParser = new XmlParser (new XmlRootState (), true);
						var textDoc = TextEditorFactory.CreateNewDocument (import.Filename, MSBuildMimeType);
						xmlParser.Parse (textDoc.CreateReader ());
						var xdoc = xmlParser.Nodes.GetRoot ();
						FindReferences (monitor, rr, import.Filename, xdoc, textDoc);
					} catch (Exception ex) {
						monitor.ReportError ($"Error searching file {Path.GetFileName (import.Filename)}", ex);
						LoggingService.LogError ($"Error searching MSBuild file {import.Filename}", ex);
					}
				}));
			}

			tasks.Add (Task.Run (() => {
				try {
					FindReferences (monitor, rr, doc.FileName, doc.XDocument, TextEditorFactory.CreateNewDocument (doc.Text, doc.FileName, MSBuildMimeType));
				} catch (Exception ex) {
					monitor.ReportError ($"Error searching file {Path.GetFileName (doc.FileName)}", ex);
					LoggingService.LogError ($"Error searching MSBuild file {doc.FileName}", ex);
				}
			}));

			Task.WhenAll (tasks).ContinueWith (t => monitor?.Dispose ());
		}

		void FindReferences (SearchProgressMonitor monitor, MSBuildResolveResult rr, string filename, XDocument doc, IReadonlyTextDocument textDoc)
		{
			var collector = MSBuildReferenceCollector.Create (rr);
			collector.Run (filename, textDoc, doc);
			var fileProvider = new FileProvider (filename);
			if (collector.Results.Count > 0) {
				monitor.ReportResults (collector.Results.Select (r => new SearchResult (fileProvider, r.Offset, r.Length)));
			}
		}

		[CommandUpdateHandler (Refactoring.RefactoryCommands.FindReferences)]
		void UpdateFindReferences (CommandInfo info)
		{
			var rr = ResolveCurrentLocation ();
			info.Enabled = MSBuildReferenceCollector.CanCreate (rr);
		}

		static string GetCounterpartFile (string name)
		{
			switch (Path.GetExtension (name.ToLower ())) {
			case ".targets":
				name = Path.ChangeExtension (name, ".props");
				break;
			case ".props":
				name = Path.ChangeExtension (name, ".targets");
				break;
			default:
				return null;
			}
			return File.Exists (name) ? name : null;
		}

		[CommandHandler (DesignerSupport.Commands.SwitchBetweenRelatedFiles)]
		protected void Run ()
		{
			var counterpart = GetCounterpartFile (FileName);
			IdeApp.Workbench.OpenDocument (counterpart, DocumentContext.Project, true);
		}

		[CommandUpdateHandler (DesignerSupport.Commands.SwitchBetweenRelatedFiles)]
		protected void Update (CommandInfo info)
		{
			info.Enabled = GetCounterpartFile (FileName) != null;
		}
	}
}