// Copyright (c) 2014 Xamarin Inc.
// Copyright (c) Microsoft. ALl rights reserved.
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
using MonoDevelop.MSBuildEditor.Schema;

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

		MSBuildParsedDocument GetDocument ()
		{
			return (MSBuildParsedDocument)DocumentContext.ParsedDocument;
		}

		public override Task<ICompletionDataList> HandleCodeCompletionAsync (CodeCompletionContext completionContext, CompletionTriggerInfo triggerInfo, CancellationToken token = default (CancellationToken))
		{
			var expressionCompletion = HandleExpressionCompletion (completionContext, triggerInfo, token);
			if (expressionCompletion != null) {
				return Task.FromResult (expressionCompletion);
			}

			return base.HandleCodeCompletionAsync (completionContext, triggerInfo, token);
		}

		MSBuildResolveResult ResolveCurrentLocation ()
		{
			if (Tracker == null) {
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

			if (rr?.SchemaElement == null) {
				list.Add (new XmlCompletionData ("Project", XmlCompletionData.DataType.XmlElement));
				return Task.FromResult (list);
			}

			foreach (var c in rr.SchemaElement.Children) {
				list.Add (new XmlCompletionData (c, XmlCompletionData.DataType.XmlElement));
			}

			var doc = GetDocument ();
			if (doc != null) {
				foreach (var item in doc.Context.GetInferredChildren (rr)) {
					list.Add (new MSBuildCompletionData (item, doc));
				}
			}

			return Task.FromResult (list);
		}

		protected override Task<CompletionDataList> GetAttributeCompletions (IAttributedXObject attributedOb,
			Dictionary<string, string> existingAtts, CancellationToken token)
		{
			var rr = ResolveCurrentLocation ();
			if (rr?.SchemaElement == null)
				return null;

			var list = new CompletionDataList ();
			foreach (var a in rr.SchemaElement.Attributes)
				if (!existingAtts.ContainsKey (a))
					list.Add (new XmlCompletionData (a, XmlCompletionData.DataType.XmlAttribute));

			var inferredAttributes = GetInferredAttributes (rr);
			if (inferredAttributes != null)
				foreach (var a in inferredAttributes)
					if (!existingAtts.ContainsKey (a))
						list.Add (new XmlCompletionData (a, XmlCompletionData.DataType.XmlAttribute));

			return Task.FromResult (list);
		}

		IEnumerable<string> GetInferredAttributes (MSBuildResolveResult rr)
		{
			var doc = GetDocument ();
			if (doc == null) {
				return Array.Empty<string> ();
			}

			//metadata as attributes
			if (rr.SchemaElement.Kind == MSBuildKind.Item && doc.ToolsVersion.IsAtLeast (MSBuildToolsVersion.V15_0)) {
				return doc.Context.GetItemMetadata (rr.ElementName, false).Where (a => !a.WellKnown).Select (a => a.Name);
			}

			if (rr.SchemaElement.Kind == MSBuildKind.Task) {
				var result = new HashSet<string> ();
				foreach (var task in doc.Context.GetTask (rr.ElementName)) {
					foreach (var p in task.Parameters) {
						result.Add (p);
					}
				}
				return result;
			}

			return null;
		}

		protected override Task<CompletionDataList> GetAttributeValueCompletions (IAttributedXObject attributedOb, XAttribute att, CancellationToken token)
		{
			var rr = ResolveCurrentLocation ();
			if (rr?.SchemaElement == null) {
				return null;
			}
			var path = GetCurrentPath ();

			int triggerLength = ((IXmlParserContext)Tracker.Engine).KeywordBuilder.Length;
			int startIdx = Editor.CaretOffset - triggerLength;

			if ((rr.SchemaElement.Kind == MSBuildKind.Import || rr.SchemaElement.Kind == MSBuildKind.Project) && rr.AttributeName == "Sdk") {
				return GetSdkCompletions (token);
			}

			if (rr.SchemaElement.Kind == MSBuildKind.Item && rr.ElementName == "PackageReference") {
				var tfm = GetDocument ().Frameworks.FirstOrDefault ()?.ToString () ?? ".NETStandard,Version=v2.0";
				if (rr.AttributeName == "Include") {
					string name = ((IXmlParserContext)Tracker.Engine).KeywordBuilder.ToString ();
					if (string.IsNullOrWhiteSpace (name)) {
						return null;
					}
					return Task.FromResult<CompletionDataList> (
						new PackageSearchCompletionDataList (
							name,
							(n) => PackageSearchManager.SearchPackageNames (n.ToLower (), tfm)
						) {
							TriggerWordStart = startIdx,
							TriggerWordLength = triggerLength
						}
					);
				}
				if (rr.AttributeName == "Version") {
					var name = path.OfType<XElement> ().Last ().Attributes.FirstOrDefault (a => a.Name.FullName == "Include")?.Value;
					if (string.IsNullOrEmpty (name)) {
						return null;
					}
					return Task.FromResult<CompletionDataList> (
						new PackageSearchCompletionDataList (
							PackageSearchManager.SearchPackageVersions (name, tfm)
						) {
							TriggerWordStart = startIdx,
							TriggerWordLength = triggerLength
						}
					);
				}
			}

			return base.GetAttributeValueCompletions (attributedOb, att, token);
		}

		Task<CompletionDataList> GetSdkCompletions (CancellationToken token)
		{
			var list = new CompletionDataList ();
			var doc = GetDocument ();
			if (doc == null) {
				return Task.FromResult (list);
			}

			var sdks = new HashSet<string> ();

			var resolver = doc.SdkResolver;
			foreach (var sdk in resolver.GetRegisteredSdks ()) {
				if (sdks.Add (sdk.Name)) {
					list.Add (System.IO.Path.GetFileName (sdk.Name));
				}
			}

			//TODO: how can we find SDKs in the non-default locations?
			return Task.Run (() => {
				foreach (var d in Directory.GetDirectories (resolver.DefaultSdkPath)) {
					string name = Path.GetFileName (d);
					if (sdks.Add (name)) {
						list.Add (name);
					}
				}
				return list;
			}, token);
		}

		ICompletionDataList HandleExpressionCompletion (CodeCompletionContext completionContext, CompletionTriggerInfo triggerInfo, CancellationToken token)
		{
			var doc = GetDocument ();
			if (doc == null)
				return null;

			var rr = ResolveCurrentLocation ();

			if (rr?.SchemaElement == null) {
				return null;
			}

			var state = Tracker.Engine.CurrentState;
			bool isAttribute = state is XmlAttributeValueState;
			if (isAttribute) {
				//FIXME: assume all attributes accept expressions for now
			} else if (state is XmlRootState) {
				if (rr.SchemaElement.ChildType != MSBuildKind.Expression)
					return null;
			} else {
				return null;
			}

			//FIXME: This is very rudimentary. We should parse the expression for real.
			int currentPosition = Editor.CaretOffset;
			int lineStart = Editor.GetLine (Editor.CaretLine).Offset;
			int expressionStart = currentPosition - Tracker.Engine.CurrentStateLength;
			if (isAttribute && GetAttributeValueDelimiter (Tracker.Engine) != 0) {
				expressionStart += 1;
			}
			int start = Math.Max (expressionStart, lineStart);
			var expression = Editor.GetTextAt (start, currentPosition - start);

			if (expression.Length < 2) {
				return null;
			}

			//trigger on letter after $(, @(
			if (expression.Length >= 3 && char.IsLetter (expression [expression.Length - 1]) && expression [expression.Length - 2] == '(') {
				char c = expression [expression.Length - 3];
				if (c == '$') {
					return new CompletionDataList (GetPropertyExpressionCompletions (doc)) { TriggerWordLength = 1 };
				}
				if (c == '@') {
					return new CompletionDataList (GetItemExpressionCompletions (doc)) { TriggerWordLength = 1 };
				}
				return null;
			}

			//trigger on $(, @(
			if (expression [expression.Length - 1] == '(') {
				char c = expression [expression.Length - 2];
				if (c == '$') {
					return new CompletionDataList (GetPropertyExpressionCompletions (doc));
				}
				if (c == '@') {
					return new CompletionDataList (GetItemExpressionCompletions (doc));
				}
				return null;
			}

			return null;
		}

		//FIXME: this is fragile, need API in core
		static char GetAttributeValueDelimiter (XmlParser parser)
		{
			var ctx = (IXmlParserContext)parser;
			switch (ctx.StateTag) {
			case 3: return '"';
			case 2: return '\'';
			default: return (char)0;
			}
		}

		IEnumerable<CompletionData> GetItemExpressionCompletions (MSBuildParsedDocument doc)
		{
			foreach (var item in doc.Context.GetItems ()) {
				yield return new CompletionData (item.Name, Ide.Gui.Stock.Class, item.Description);
			}
		}

		IEnumerable<CompletionData> GetPropertyExpressionCompletions (MSBuildParsedDocument doc)
		{
			foreach (var prop in doc.Context.GetProperties (true)) {
				yield return new CompletionData (prop.Name, Ide.Gui.Stock.Class, prop.Description);
			}
		}

		IEnumerable<T> GetAnnotationsAtLocation<T> (DocumentLocation location)
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