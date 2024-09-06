// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Syntax;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.PackageSearch;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.SdkResolution;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Editor;
using MonoDevelop.Xml.Editor.Completion;
using MonoDevelop.Xml.Parser;

using ProjectFileTools.NuGetSearch.Feeds;

using static MonoDevelop.MSBuild.Language.ExpressionCompletion;


// todo: switch to IAsyncCompletionUniversalSource to allow per-item span and commit chars
namespace MonoDevelop.MSBuild.Editor.Completion
{
	partial class MSBuildCompletionSource : XmlCompletionSource<MSBuildCompletionContext>
	{
		readonly IMSBuildFileSystem fileSystem;
		readonly MSBuildCompletionSourceProvider provider;

		public MSBuildCompletionSource (ITextView textView, MSBuildCompletionSourceProvider provider, ILogger logger) : base (textView, logger, provider.XmlParserProvider)
		{
			fileSystem = provider.FileSystem ?? DefaultMSBuildFileSystem.Instance;
			this.provider = provider;
		}

		protected override MSBuildCompletionContext CreateTriggerContext (IAsyncCompletionSession session, CompletionTrigger trigger, XmlSpineParser spineParser, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan)
			=> new (
				provider.ParserProvider.GetParser (triggerLocation.Snapshot.TextBuffer),
				provider, session, triggerLocation, spineParser, trigger, applicableToSpan
			);

		protected override Task<IList<CompletionItem>> GetElementCompletionsAsync (MSBuildCompletionContext context, bool includeBracket, CancellationToken token)
		{
			var doc = context.Document;

			var nodePath = context.NodePath;
			if (!CompletionHelpers.TryGetElementSyntaxForElementCompletion(nodePath, out MSBuildElementSyntax languageElement, out string elementName)) {
				return TaskCompleted (null);
			}

			var items = new List<CompletionItem> ();

			foreach (var el in doc.GetElementCompletions (languageElement, elementName)) {
				if (el is ItemInfo) {
					items.Add (CreateCompletionItem (context.DocumentationProvider, el, XmlCompletionItemKind.SelfClosingElement, includeBracket ? "<" : null));
				} else {
					items.Add (CreateCompletionItem (context.DocumentationProvider, el, XmlCompletionItemKind.Element, includeBracket ? "<" : null));
				}
			}

			bool allowCData = languageElement != null && languageElement.ValueKind != MSBuildValueKind.Nothing;
			foreach (var c in GetMiscellaneousTags (context.TriggerLocation, nodePath, includeBracket, allowCData)) {
				items.Add (c);
			}

			return TaskCompleted (items);
		}

		static Task<IList<CompletionItem>> TaskCompleted (IList<CompletionItem> items) => Task.FromResult (items);

		protected override Task<IList<CompletionItem>> GetAttributeCompletionsAsync (MSBuildCompletionContext context, IAttributedXObject attributedObject, Dictionary<string, string> existingAttributes, CancellationToken token)
		{
			var rr = context.ResolveResult;
			var doc = context.Document;

			if (rr?.ElementSyntax == null) {
				return TaskCompleted (null);
			}

			var items = new List<CompletionItem> ();

			foreach (var att in rr.GetAttributeCompletions (doc, doc.ToolsVersion)) {
				if (!existingAttributes.ContainsKey (att.Name)) {
					items.Add (CreateCompletionItem (context.DocumentationProvider, att, XmlCompletionItemKind.Attribute));
				}
			}

			return TaskCompleted (items);
		}

		protected override Task<IList<CompletionItem>> GetEntityCompletionsAsync (MSBuildCompletionContext context, CancellationToken token)
		{

			// don't want entity completion in elements we know don't have text content
			if (context.ResolveResult is MSBuildResolveResult rr && rr.AttributeName is null && rr.ElementSyntax is MSBuildElementSyntax element && element.ValueKind == MSBuildValueKind.Nothing) {
				return TaskCompleted (null);
			}

			return TaskCompleted (GetBuiltInEntityItems ());
		}

		CompletionItem CreateCompletionItem (MSBuildCompletionDocumentationProvider documentationProvider, ISymbol symbol, XmlCompletionItemKind xmlCompletionItemKind, string prefix = null, string annotation = null, bool addDescriptionHint = false)
		{
			ImageElement image;

			if (symbol.IsDeprecated ()) {
				image = provider.DisplayElementFactory.GetImageElement (KnownImages.Deprecated);
			} else {
				image = provider.DisplayElementFactory.GetImageElement (symbol);
			}

			var value = symbol.Name;
			if (prefix is not null) {
				value = prefix + value;
			}

			string displayText = value;
			string insertText = value;
			string filterText = value;
			string sortText = value;
			string suffix = null;

			if (annotation is not null) {
				filterText = $"{value} {annotation}";
				sortText = annotation;
				suffix = annotation;
			}
			else if (addDescriptionHint) {
				var descriptionHint = DescriptionFormatter.GetCompletionHint (symbol);
				suffix = descriptionHint;
			}

			var item = new CompletionItem (displayText, this, image, ImmutableArray<CompletionFilter>.Empty, suffix, insertText, sortText, filterText, ImmutableArray<ImageElement>.Empty);
			documentationProvider.AttachDocumentation (item, symbol);
			item.AddKind (xmlCompletionItemKind);
			return item;
		}

		protected override CompletionStartData InitializeCompletion (CompletionTrigger trigger, SnapshotPoint triggerLocation, XmlSpineParser spineParser, CancellationToken token)
		{
			var baseCompletion = base.InitializeCompletion (trigger, triggerLocation, spineParser, token);

			// we don't care need a real document here we're doing very basic resolution for triggering
			// FIXME: is it worth trying to reuse this ResolveResult and TriggerStare for the actual completion?
			var spine = GetSpineParser (triggerLocation);
			var rr = MSBuildResolver.Resolve (spine.Clone (), triggerLocation.Snapshot.GetTextSource (), MSBuildRootDocument.Empty, null, Logger, token);

			if (rr?.ElementSyntax is MSBuildElementSyntax elementSyntax && (rr.Attribute is not null || elementSyntax.ValueKind != MSBuildValueKind.Nothing)) {
				var reason = MSBuildCompletionContext.ConvertTriggerReason (trigger.Reason, trigger.Character);
				if (reason == ExpressionTriggerReason.Unknown || !IsPossibleExpressionCompletionContext (spine)) {
					return baseCompletion;
				}

				// TryGetIncompleteValue may return false while still outputting incomplete values, if it fails due to reaching maximum readahead.
				// It will also return false and output null values if we're in an element value that only contains whitespace.
				// In both these cases we can ignore the false return and proceed anyways.
				spineParser.TryGetIncompleteValue (triggerLocation.Snapshot, out var expression, out var valueSpan, cancellationToken: token);
				expression ??= "";
				int exprStartPos = valueSpan?.Start ?? triggerLocation;

				var triggerState = GetTriggerState (
					expression,
					triggerLocation - exprStartPos,
					reason,
					trigger.Character,
					rr.IsCondition (),
					out int spanStart,
					out int spanLength,
					out ExpressionNode _,
					out var _,
					out IReadOnlyList<ExpressionNode> _,
					Logger
				);

				if (triggerState != TriggerState.None) {
					spanStart = exprStartPos + spanStart;
					return new CompletionStartData (CompletionParticipation.ProvidesItems, new SnapshotSpan (triggerLocation.Snapshot, spanStart, spanLength));
				}
			}

			return baseCompletion;
		}

		protected override async Task<IList<CompletionItem>> GetAdditionalCompletionsAsync (MSBuildCompletionContext context, CancellationToken token)
		{
			if (context.ExpressionTriggerReason == ExpressionTriggerReason.Unknown) {
				return null;
			}

			var msbuildTrigger = MSBuildCompletionTrigger.TryCreate (
				context.SpineParser,
				context.TriggerLocation.Snapshot.GetTextSource(),
				context.ExpressionTriggerReason,
				context.TriggerLocation,
				context.Trigger.Character,
				Logger,
				provider.FunctionTypeProvider,
				context.ResolveResult, token);

			if (msbuildTrigger is null) {
				return null;
			}

			// used by MSBuildCompletionCommitManager
			context.Session.Properties.AddProperty (typeof (TriggerState), msbuildTrigger.TriggerState);

			var info = context.ResolveResult.GetElementOrAttributeValueInfo (context.Document);
			if (info is null || info.ValueKind == MSBuildValueKind.Nothing) {
				return null;
			}

			return await GetExpressionCompletionsAsync (context, info, msbuildTrigger, token);
		}

		async Task<List<CompletionItem>> GetPackageNameCompletions (MSBuildCompletionContext context, string searchQuery, string packageType, CancellationToken token)
		{
			var tfm = context.Document.GetTargetFrameworkNuGetSearchParameter ();
			context.Session.Properties.AddProperty (typeof (NuGetSearchUpdater), new NuGetSearchUpdater (this, context, tfm, packageType, Logger));

			if (string.IsNullOrEmpty (searchQuery)) {
				return null;
			}

			var results = await provider.PackageSearchManager.SearchPackageNames (searchQuery.ToLower (), tfm, packageType).ToTask (token);

			return CreateNuGetItemsFromSearchResults (context.DocumentationProvider, results);
		}

		List<CompletionItem> CreateNuGetItemsFromSearchResults (MSBuildCompletionDocumentationProvider documentationProvider, IReadOnlyList<Tuple<string, FeedKind>> results)
		{
			var items = new List<CompletionItem> ();
			var dedup = new HashSet<string> ();

			// dedup, preferring nuget -> myget -> local
			AddItems (FeedKind.NuGet);
			AddItems (FeedKind.MyGet);
			AddItems (FeedKind.Local);

			void AddItems (FeedKind kind)
			{
				foreach (var result in results) {
					if (result.Item2 == kind) {
						if (dedup.Add (result.Item1)) {
							items.Add (CreateNuGetCompletionItem (documentationProvider, result, XmlCompletionItemKind.AttributeValue));
						}
					}
				}
			}

			return items;
		}

		async Task<List<CompletionItem>> GetPackageVersionCompletions (MSBuildCompletionContext context, CancellationToken token)
		{
			if (!PackageCompletion.TryGetPackageVersionSearchJob (context.ResolveResult, context.Document, provider.PackageSearchManager, out var packageSearchJob, out _, out _)) {
				return null;
			}

			var results = await packageSearchJob.ToTask (token);

			//FIXME should we deduplicate?
			var items = new List<CompletionItem> ();
			var index = 0;
			foreach (var result in results.Reverse ()) {
				items.Add (CreateOrderedNuGetCompletionItem (context.DocumentationProvider, result, XmlCompletionItemKind.AttributeValue, index++));
			}

			return items;
		}

		async Task<List<CompletionItem>> GetExpressionCompletionsAsync (
			MSBuildCompletionContext context,
			ITypedSymbol valueSymbol, MSBuildCompletionTrigger trigger,
			CancellationToken token)
		{
			var doc = context.Document;
			var rr = trigger.ResolveResult;
			var kind = valueSymbol.ValueKind;
			var triggerState = trigger.TriggerState;

			if (!ValidateListPermitted (trigger.ListKind, kind)) {
				return null;
			}

			bool allowExpressions = kind.AllowsExpressions ();

			kind = kind.WithoutModifiers ();

			if (kind == MSBuildValueKind.Data || kind == MSBuildValueKind.Nothing) {
				return null;
			}

			// FIXME: This is a temporary hack so we have completion for imported XSD schemas with missing type info.
			// It is not needed for inferred schemas, as they have already performed the inference.
			if (kind == MSBuildValueKind.Unknown) {
				kind = MSBuildInferredSchema.InferValueKindFromName (valueSymbol);
			}

			bool isValue = triggerState == TriggerState.Value;

			var items = new List<CompletionItem> ();

			if (trigger.ComparandVariables != null && isValue) {
				foreach (var ci in ExpressionCompletion.GetComparandCompletions (doc, fileSystem, trigger.ComparandVariables, Logger)) {
					items.Add (CreateCompletionItem (context.DocumentationProvider, ci, XmlCompletionItemKind.AttributeValue));
				}
			}

			if (isValue) {
				switch (kind) {
				case MSBuildValueKind.NuGetID:
					if (trigger.Expression is ExpressionText t) {
						var packageType = valueSymbol.CustomType?.Values[0].Name;
						var packageNameItems = await GetPackageNameCompletions (context, t.Value, packageType, token);
						if (packageNameItems != null) {
							items.AddRange (packageNameItems);
						}
					}
					break;
				case MSBuildValueKind.NuGetVersion: {
						var packageVersionItems = await GetPackageVersionCompletions (context, token);
						if (packageVersionItems != null) {
							items.AddRange (packageVersionItems);
						}
						break;
					}
				case MSBuildValueKind.Sdk:
				case MSBuildValueKind.SdkWithVersion: {
					var sdkItems = SdkCompletion.GetSdkCompletions (doc, Logger, token).Select(s => CreateSdkCompletionItem (s));
						if (sdkItems != null) {
							items.AddRange (sdkItems);
						}
						break;
					}
				case MSBuildValueKind.Lcid:
					items.AddRange (GetLcidCompletions (context.DocumentationProvider));
					break;
				case MSBuildValueKind.Culture:
					items.AddRange (GetCultureCompletions (context.DocumentationProvider));
					break;
				}

				if (kind == MSBuildValueKind.Guid || valueSymbol.CustomType is CustomTypeInfo { BaseKind: MSBuildValueKind.Guid, AllowUnknownValues: true }) {
					items.Add (CreateSpecialItem ("New GUID", "Inserts a new GUID", KnownImages.Add, MSBuildCommitItemKind.NewGuid));
				}
			}

			//TODO: better metadata support
			// NOTE: can't just check CustomTypeInfo isn't null, must check kind, as NuGetID stashes the dependency type in the CustomTypeInfo
			if (kind == MSBuildValueKind.CustomType && valueSymbol.CustomType != null && valueSymbol.CustomType.Values.Count > 0 && isValue) {
				bool addDescriptionHint = CompletionHelpers.ShouldAddHintForCompletions (valueSymbol);
				foreach (var value in valueSymbol.CustomType.Values) {
					items.Add (CreateCompletionItem (context.DocumentationProvider, value, XmlCompletionItemKind.AttributeValue, addDescriptionHint: addDescriptionHint));
				}

			} else {
				//FIXME: can we avoid awaiting this unless we actually need to resolve a function? need to propagate async downwards
				await provider.FunctionTypeProvider.EnsureInitialized (token);
				if (GetCompletionInfos (rr, triggerState, valueSymbol, trigger.Expression, trigger.SpanLength, doc, provider.FunctionTypeProvider, fileSystem, Logger, kindIfUnknown: kind) is IEnumerable<ISymbol> completionInfos) {
					bool addDescriptionHint = CompletionHelpers.ShouldAddHintForCompletions (valueSymbol);
					foreach (var ci in completionInfos) {
						items.Add (CreateCompletionItem (context.DocumentationProvider, ci, XmlCompletionItemKind.AttributeValue, addDescriptionHint: addDescriptionHint));
					}
				}
			}

			if ((allowExpressions && isValue) || triggerState == TriggerState.BareFunctionArgumentValue) {
				items.Add (CreateSpecialItem ("$(", "Property reference", KnownImages.MSBuildProperty, MSBuildCommitItemKind.PropertyReference));
			}

			if (allowExpressions && isValue) {
				items.Add (CreateSpecialItem ("@(", "Item reference", KnownImages.MSBuildItem, MSBuildCommitItemKind.ItemReference));
				if (CompletionHelpers.IsMetadataAllowed (trigger.Expression, rr)) {
					items.Add (CreateSpecialItem ("%(", "Metadata reference", KnownImages.MSBuildItem, MSBuildCommitItemKind.MetadataReference));
				}
			}

			return items;
		}

		CompletionItem CreateSpecialItem (string text, string description, KnownImages image, MSBuildCommitItemKind kind)
		{
			var item = new CompletionItem (text, this, provider.DisplayElementFactory.GetImageElement (image));
			item.Properties.AddProperty (typeof (MSBuildCommitItemKind), kind);
			item.AddDocumentation (description);
			return item;
		}

		CompletionItem CreateNuGetCompletionItem (MSBuildCompletionDocumentationProvider documentationProvider, Tuple<string,FeedKind> info, XmlCompletionItemKind xmlCompletionItemKind)
		{
			var kindImage = provider.DisplayElementFactory.GetImageElement (info.Item2);
			var item = new CompletionItem (info.Item1, this, kindImage);
			item.AddKind (xmlCompletionItemKind);
			documentationProvider.AttachDocumentation (item, info);
			return item;
		}

		CompletionItem CreateOrderedNuGetCompletionItem (MSBuildCompletionDocumentationProvider documentationProvider, Tuple<string,FeedKind> info, XmlCompletionItemKind xmlCompletionItemKind, int index)
		{
			var kindImage = provider.DisplayElementFactory.GetImageElement (info.Item2);
			string displayText = info.Item1;
			var item = new CompletionItem (displayText, this, kindImage, ImmutableArray<CompletionFilter>.Empty, string.Empty, displayText, $"_{index:D5}", displayText, ImmutableArray<ImageElement>.Empty);
			item.AddKind (xmlCompletionItemKind);
			documentationProvider.AttachDocumentation (item, info);
			return item;
		}

		CompletionItem CreateSdkCompletionItem (SdkInfo info)
		{
			var img = provider.DisplayElementFactory.GetImageElement (KnownImages.Sdk);
			var item = new CompletionItem (info.Name, this, img);

			//FIXME better tooltips for SDKs
			if (info.Path is string sdkPath) {
				item.AddDocumentation (sdkPath);
			}

			return item;
		}

		IEnumerable<CompletionItem> GetLcidCompletions (MSBuildCompletionDocumentationProvider documentationProvider)
		{
			var imageEl = provider.DisplayElementFactory.GetImageElement (KnownImages.Constant);
			foreach (var culture in CultureHelper.GetKnownCultures ()) {
				string cultureLcid = culture.Lcid.ToString ();
				string displayName = culture.DisplayName;
				// add the culture name to the filter text so ppl can just type the actual language/country instead of looking up the code
				string filterText = $"{cultureLcid} {displayName}";
				var item = new CompletionItem (cultureLcid, this, imageEl, ImmutableArray<CompletionFilter>.Empty, displayName, cultureLcid, cultureLcid, filterText, ImmutableArray<ImageElement>.Empty);
				documentationProvider.AttachDocumentation (item, culture.CreateLcidSymbol ());
				yield return item;
			}
		}

		IEnumerable<CompletionItem> GetCultureCompletions (MSBuildCompletionDocumentationProvider documentationProvider)
		{
			var imageEl = provider.DisplayElementFactory.GetImageElement (KnownImages.Constant);
			foreach (var culture in CultureHelper.GetKnownCultures ()) {
				string cultureName = culture.Name;
				if (string.IsNullOrEmpty (cultureName)) {
					continue;
				}
				// add the culture name to the filter text so ppl can just type the actual language/country instead of looking up the code
				string filterText = $"{culture.Name} {culture.DisplayName}";
				var item = new CompletionItem (culture.Name, this, imageEl, ImmutableArray<CompletionFilter>.Empty, culture.DisplayName, cultureName, cultureName, filterText, ImmutableArray<ImageElement>.Empty);
				documentationProvider.AttachDocumentation (item, culture.CreateCultureSymbol ());
				yield return item;
			}
		}
	}

	/// <summary>
	/// Annotate an item type for special handling in the commit manager
	/// </summary>
	enum MSBuildCommitItemKind
	{
		NewGuid,
		PropertyReference,
		ItemReference,
		MetadataReference
	}

}
