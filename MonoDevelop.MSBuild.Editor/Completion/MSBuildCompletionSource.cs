// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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

			// we can't use the LanguageElement from the resolveresult here.
			// if completion is triggered in an existing element's name, the resolveresult
			// will be for that element, so completion will be for the element's children
			// rather than for the element itself.
			var nodePath = context.NodePath;
			MSBuildElementSyntax languageElement = null;
			string elName = null;
			for (int i = 1; i < nodePath.Count; i++) {
				if (nodePath[i] is XElement el) {
					elName = el.Name.Name;
					languageElement = MSBuildElementSyntax.Get (elName, languageElement);
					continue;
				}
				return TaskCompleted (null);
			}

			// if we don't have a language element and we're not at root level, we're in an invalid location
			if (languageElement == null && nodePath.Count > 2) {
				return TaskCompleted (null);
			}

			var items = new List<CompletionItem> ();

			foreach (var el in doc.GetElementCompletions (languageElement, elName)) {
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

				string expression = spine.GetIncompleteValue (triggerLocation.Snapshot);
				int exprStartPos = triggerLocation - expression.Length;
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
			if (context.ResolveResult?.ElementSyntax is null || context.ExpressionTriggerReason == ExpressionTriggerReason.Unknown || !IsPossibleExpressionCompletionContext (context.SpineParser)) {
				return null;
			}

			var triggerLocation = context.TriggerLocation;
			string expression = context.SpineParser.GetIncompleteValue (triggerLocation.Snapshot);
			int exprStartPos = triggerLocation.Position - expression.Length;
			var triggerState = GetTriggerState (expression, triggerLocation - exprStartPos, context.ExpressionTriggerReason, context.Trigger.Character, context.ResolveResult.IsCondition (),
				out int spanStart, out int spanLength, out ExpressionNode triggerExpression, out var listKind, out IReadOnlyList<ExpressionNode> comparandVariables,
				Logger
			);
			spanStart = exprStartPos + spanStart;

			if (triggerState == TriggerState.None) {
				return null;
			}

			// used by MSBuildCompletionCommitManager
			context.Session.Properties.AddProperty (typeof (TriggerState), triggerState);

			var info = context.ResolveResult.GetElementOrAttributeValueInfo (context.Document);
			if (info is null || info.ValueKind == MSBuildValueKind.Nothing) {
				return null;
			}

			return await GetExpressionCompletionsAsync (context, info, triggerState, listKind, spanLength, triggerExpression, comparandVariables, token);
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

		static bool ItemIsInItemGroup (XElement itemEl) => itemEl.Parent is XElement parent && parent.Name.Equals (MSBuildElementSyntax.ItemGroup.Name, true);

		static XElement GetItemGroupItemFromMetadata (MSBuildResolveResult rr)
			=> rr.ElementSyntax.SyntaxKind switch {
				MSBuildSyntaxKind.Item => rr.Element,
				MSBuildSyntaxKind.Metadata => rr.Element.Parent is XElement parentEl && ItemIsInItemGroup (parentEl)? parentEl : null,
				_ => null
			};

		static XAttribute GetIncludeOrUpdateAttribute (XElement item)
			=> item.Attributes.FirstOrDefault (att => MSBuildElementSyntax.Item.GetAttribute (att)?.SyntaxKind switch {
				MSBuildSyntaxKind.Item_Include => true,
				MSBuildSyntaxKind.Item_Update => true,
				_ => false
			});

		async Task<List<CompletionItem>> GetPackageVersionCompletions (MSBuildCompletionContext context, CancellationToken token)
		{
			if (context.ResolveResult is not MSBuildResolveResult rr || GetItemGroupItemFromMetadata (rr) is not XElement itemEl || GetIncludeOrUpdateAttribute (itemEl) is not XAttribute includeAtt) {
				return null;
			}

			// we can only provide version completions if the item's value type is non-list nugetid
			var itemInfo = context.Document.GetSchemas ().GetItem (itemEl.Name.Name);
			if (itemInfo == null || !itemInfo.ValueKind.IsKindOrListOfKind (MSBuildValueKind.NuGetID)) {
				return null;
			}

			var packageType = itemInfo.CustomType?.Values[0].Name;

			var packageId = includeAtt.Value;
			if (string.IsNullOrEmpty (packageId)) {
				return null;
			}

			// check it's a non-list literal value, we can't handle anything else
			var expr = ExpressionParser.Parse (packageId, ExpressionOptions.ItemsMetadataAndLists);
			if (expr.NodeKind != ExpressionNodeKind.Text) {
				return null;
			}

			var tfm = context.Document.GetTargetFrameworkNuGetSearchParameter ();

			var results = await provider.PackageSearchManager.SearchPackageVersions (packageId.ToLower (), tfm, packageType).ToTask (token);

			//FIXME should we deduplicate?
			var items = new List<CompletionItem> ();
			var index = 0;
			foreach (var result in results.Reverse ()) {
				items.Add (CreateOrderedNuGetCompletionItem (context.DocumentationProvider, result, XmlCompletionItemKind.AttributeValue, index++));
			}

			return items;
		}

		//FIXME: SDK version completion
		//FIXME: enumerate SDKs from NuGet
		Task<List<CompletionItem>> GetSdkCompletions (MSBuildRootDocument doc, CancellationToken token)
		{
			return Task.Run (() => {
				var items = new List<CompletionItem> ();
				var sdks = new HashSet<string> ();

				foreach (var sdk in doc.Environment.GetRegisteredSdks ()) {
					if (sdks.Add (sdk.Name)) {
						items.Add (CreateSdkCompletionItem (sdk));
					}
				}

				//FIXME we should be able to cache these
				doc.Environment.ToolsetProperties.TryGetValue (WellKnownProperties.MSBuildSDKsPath, out var sdksPath);
				if (sdksPath != null) {
					AddSdksFromDir (sdksPath);
				}

				var dotNetSdk = doc.Environment.ResolveSdk (("Microsoft.NET.Sdk", null, null), null, null, Logger);
				if (dotNetSdk?.Path is string sdkPath) {
					string dotNetSdkPath = Path.GetDirectoryName (Path.GetDirectoryName (sdkPath));
					if (sdksPath == null || Path.GetFullPath (dotNetSdkPath) != Path.GetFullPath (sdksPath)) {
						AddSdksFromDir (dotNetSdkPath);
					}
				}

				void AddSdksFromDir (string sdkDir)
				{
					if (!Directory.Exists (sdkDir)) {
						return;
					}
					foreach (var dir in Directory.GetDirectories (sdkDir)) {
						string name = Path.GetFileName (dir);
						var targetsFileExists = File.Exists (Path.Combine (dir, "Sdk", "Sdk.targets"));
						if (targetsFileExists && sdks.Add (name)) {
							items.Add (CreateSdkCompletionItem (new SdkInfo (name, null, Path.Combine (dir, name))));
						}
					}
				}

				return items;
			}, token);
		}

		async Task<List<CompletionItem>> GetExpressionCompletionsAsync (
			MSBuildCompletionContext context,
			ITypedSymbol valueSymbol, TriggerState triggerState, ListKind listKind,
			int triggerLength, ExpressionNode triggerExpression,
			IReadOnlyList<ExpressionNode> comparandVariables,
			CancellationToken token)
		{
			var doc = context.Document;
			var rr = context.ResolveResult;
			var kind = valueSymbol.ValueKind;

			if (!ValidateListPermitted (listKind, kind)) {
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

			if (comparandVariables != null && isValue) {
				foreach (var ci in ExpressionCompletion.GetComparandCompletions (doc, fileSystem, comparandVariables, Logger)) {
					items.Add (CreateCompletionItem (context.DocumentationProvider, ci, XmlCompletionItemKind.AttributeValue));
				}
			}

			if (isValue) {
				switch (kind) {
				case MSBuildValueKind.NuGetID:
					if (triggerExpression is ExpressionText t) {
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
					var sdkItems = await GetSdkCompletions (doc, token);
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
			if (valueSymbol.CustomType != null && valueSymbol.CustomType.Values.Count > 0 && isValue) {
				// if it's a list of ints or guids, add an annotation to make it easier to navigate
				bool addAnnotation = valueSymbol.CustomType.BaseKind switch {
					MSBuildValueKind.Guid => true,
					MSBuildValueKind.Int => true,
					_ => false
				};
				foreach (var value in valueSymbol.CustomType.Values) {
					items.Add (CreateCompletionItem (context.DocumentationProvider, value, XmlCompletionItemKind.AttributeValue, annotation: addAnnotation? value.Description.Text : null));
				}

			} else {
				//FIXME: can we avoid awaiting this unless we actually need to resolve a function? need to propagate async downwards
				await provider.FunctionTypeProvider.EnsureInitialized (token);
				if (GetCompletionInfos (rr, triggerState, valueSymbol, triggerExpression, triggerLength, doc, provider.FunctionTypeProvider, fileSystem, Logger, kindIfUnknown: kind) is IEnumerable<ISymbol> completionInfos) {
					bool addDescriptionHint = valueSymbol.IsKindOrDerived (MSBuildValueKind.WarningCode);
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
				if (MSBuildCompletionSource.IsMetadataAllowed (triggerExpression, rr)) {
					items.Add (CreateSpecialItem ("%(", "Metadata reference", KnownImages.MSBuildItem, MSBuildCommitItemKind.MetadataReference));
				}
			}

			return items;
		}

		//FIXME: improve logic for determining where metadata is permitted
		static bool IsMetadataAllowed (ExpressionNode triggerExpression, MSBuildResolveResult rr)
		{
			//if any a parent node is an item transform or function, metadata is allowed
			if (triggerExpression != null) {
				var node = triggerExpression.Find (triggerExpression.Length);
				while (node != null) {
					if (node is ExpressionItemTransform || node is ExpressionItemFunctionInvocation) {
						return true;
					}
					node = node.Parent;
				}
			}

			if (rr.AttributeSyntax != null) {
				switch (rr.AttributeSyntax.SyntaxKind) {
				// metadata attributes on items can refer to other metadata on the items
				case MSBuildSyntaxKind.Item_Metadata:
				// task params can refer to metadata in batched items
				case MSBuildSyntaxKind.Task_Parameter:
				// target inputs and outputs can use metadata from each other's items
				case MSBuildSyntaxKind.Target_Inputs:
				case MSBuildSyntaxKind.Target_Outputs:
					return true;
				//conditions on metadata elements can refer to metadata on the items
				case MSBuildSyntaxKind.Metadata_Condition:
					return true;
				}
			}

			if (rr.ElementSyntax != null) {
				switch (rr.ElementSyntax.SyntaxKind) {
				// metadata elements can refer to other metadata in the items
				case MSBuildSyntaxKind.Metadata:
					return true;
				}
			}
			return false;
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
