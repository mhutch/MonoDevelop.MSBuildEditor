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

		MSBuildParsedDocument GetDocument ()
		{
			return (MSBuildParsedDocument)DocumentContext.ParsedDocument;
		}

		public override Task<ICompletionDataList> HandleCodeCompletionAsync (CodeCompletionContext completionContext, CompletionTriggerInfo triggerInfo, CancellationToken token = default (CancellationToken))
		{
			var doc = GetDocument ();
			if (doc != null) {
				var rr = ResolveCurrentLocation ();
				if (rr?.LanguageElement != null) {
					var expressionCompletion = HandleExpressionCompletion (rr);
					if (expressionCompletion != null) {
						return Task.FromResult (expressionCompletion);
					}
				}
			}

			return base.HandleCodeCompletionAsync (completionContext, triggerInfo, token);
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
					list.Add (new MSBuildCompletionData (el, doc.Context, XmlCompletionData.DataType.XmlElement));
				}
			}

			return Task.FromResult (list);
		}

		static Task<CompletionDataList> ToCompletionList (IEnumerable<BaseInfo> infos, MSBuildResolveContext ctx, XmlCompletionData.DataType type)
		{
			var data = infos.Select (i => new MSBuildCompletionData (i, ctx, type));
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
				list.Add (new MSBuildCompletionData (att, doc.Context, XmlCompletionData.DataType.XmlElement));
			}

			return Task.FromResult (list);
		}

		protected override Task<CompletionDataList> GetAttributeValueCompletions (IAttributedXObject attributedOb, XAttribute att, CancellationToken token)
		{
			var rr = ResolveCurrentLocation ();
			if (rr?.LanguageElement == null) {
				return null;
			}
			var doc = GetDocument ();

			int triggerLength = ((IXmlParserContext)Tracker.Engine).KeywordBuilder.Length;
			int startIdx = Editor.CaretOffset - triggerLength;

			if ((rr.LanguageElement.Kind == MSBuildKind.Import || rr.LanguageElement.Kind == MSBuildKind.Project) && rr.AttributeName == "Sdk") {
				return GetSdkCompletions (token);
			}

			if (rr.LanguageElement.Kind == MSBuildKind.Item) {
				if (rr.ElementName == "PackageReference") {
					if (rr.AttributeName == "Include") {
						return GetPackageNameCompletions (doc, startIdx, triggerLength);
					}
					if (rr.AttributeName == "Include") {
						return GetPackageVersionCompletions (doc, rr, startIdx, triggerLength);
					}
				}
			}

			var list = new CompletionDataList ();
			foreach (var value in rr.GetAttributeValueCompletions (doc.Context.GetSchemas (), doc.ToolsVersion, out char[] valueSeparators)) {
				list.Add (new MSBuildCompletionData (value, doc.Context, XmlCompletionData.DataType.XmlAttributeValue));
			}
			return Task.FromResult (list);
		}

		string GetTargetFramework (MSBuildParsedDocument doc)
		{
			return doc.Frameworks.FirstOrDefault ()?.ToString () ?? ".NETStandard,Version=v2.0";
		}

		Task<CompletionDataList> GetPackageNameCompletions (MSBuildParsedDocument doc, int startIdx, int triggerLength)
		{
			string name = ((IXmlParserContext)Tracker.Engine).KeywordBuilder.ToString ();
			if (string.IsNullOrWhiteSpace (name)) {
				return null;
			}
			return Task.FromResult<CompletionDataList> (
				new PackageSearchCompletionDataList (
					name,
					(n) => PackageSearchManager.SearchPackageNames (n.ToLower (), GetTargetFramework (doc))
				) {
					TriggerWordStart = startIdx,
					TriggerWordLength = triggerLength
				}
			);
		}

		Task<CompletionDataList> GetPackageVersionCompletions (MSBuildParsedDocument doc, MSBuildResolveResult rr, int startIdx, int triggerLength)
		{
			var name = rr.XElement.Attributes.FirstOrDefault (a => a.Name.FullName == "Include")?.Value;
			if (string.IsNullOrEmpty (name)) {
				return null;
			}
			return Task.FromResult<CompletionDataList> (
				new PackageSearchCompletionDataList (
					PackageSearchManager.SearchPackageVersions (name, GetTargetFramework (doc))
				) {
					TriggerWordStart = startIdx,
					TriggerWordLength = triggerLength
				}
			);
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
					list.Add (Path.GetFileName (sdk.Name));
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

		ICompletionDataList HandleExpressionCompletion (MSBuildResolveResult rr)
		{
			var doc = GetDocument ();

			var state = Tracker.Engine.CurrentState;
			bool isAttribute = state is XmlAttributeValueState;
			if (isAttribute) {
				//FIXME: assume all attributes accept expressions for now
			} else if (state is XmlRootState) {
				if (rr.LanguageElement.ChildType != MSBuildKind.Expression)
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

			char[] valueSeparators;
			IReadOnlyList<BaseInfo> values;
			if (state is XmlRootState) {
				values = rr.GetElementValueCompletions (doc.Context.GetSchemas (), doc.ToolsVersion, out valueSeparators);
			} else {
				values = rr.GetAttributeValueCompletions (doc.Context.GetSchemas (), doc.ToolsVersion, out valueSeparators);
			}

			var triggerState = GetTriggerState (expression, valueSeparators, out int triggerLength);

			switch (triggerState) {
			case ExpressionTriggerState.Value: {
					var list = new CompletionDataList { TriggerWordLength = triggerLength};
					list.Add ("$(");
					list.Add ("@(");
					list.AutoSelect = false;
					foreach (var v in values) {
						list.Add (new MSBuildCompletionData (v, doc.Context, XmlCompletionData.DataType.XmlAttributeValue));
					}
					return list;
				}
			case ExpressionTriggerState.Item:
				return new CompletionDataList (GetItemExpressionCompletions (doc)) { TriggerWordLength = triggerLength };
			case ExpressionTriggerState.Property:
				return new CompletionDataList (GetPropertyExpressionCompletions (doc)) { TriggerWordLength = triggerLength };
			}

			return null;
		}

		ExpressionTriggerState GetTriggerState (string expression, char[] valueSeparators, out int triggerLength)
		{
			triggerLength = 0;

			if (expression.Length == 0) {
				return ExpressionTriggerState.Value;
			}

			char lastChar = expression[expression.Length - 1];

			if (valueSeparators != null && valueSeparators.Contains (lastChar)) {
				return ExpressionTriggerState.Value;
			}

			//trigger on letter after $(, @(
			if (expression.Length >= 3 && char.IsLetter (lastChar) && expression[expression.Length - 2] == '(') {
				char c = expression[expression.Length - 3];
				switch (c) {
				case '$':
					triggerLength = 1;
					return ExpressionTriggerState.Property;
				case '@':
					triggerLength = 1;
					return ExpressionTriggerState.Item;
				case '%':
					triggerLength = 1;
					return ExpressionTriggerState.Metadata;
				}
			}

			//trigger on $(, @(
			if (expression[expression.Length - 1] == '(') {
				char c = expression[expression.Length - 2];
				switch (c) {
				case '$':
					return ExpressionTriggerState.Property;
				case '@':
					return ExpressionTriggerState.Item;
				case '%':
					return ExpressionTriggerState.Metadata;
				}
			}

			return ExpressionTriggerState.None;
		}

		enum ExpressionTriggerState
		{
			None,
			Value,
			Item,
			Property,
			Metadata
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
			foreach (var item in doc.Context.GetSchemas ().GetItems ()) {
				yield return new MSBuildCompletionData (item, doc.Context, XmlCompletionData.DataType.XmlAttributeValue);
			}
		}

		IEnumerable<CompletionData> GetPropertyExpressionCompletions (MSBuildParsedDocument doc)
		{
			foreach (var prop in doc.Context.GetSchemas ().GetProperties (true)) {
				yield return new MSBuildCompletionData (prop, doc.Context, XmlCompletionData.DataType.XmlAttributeValue);
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