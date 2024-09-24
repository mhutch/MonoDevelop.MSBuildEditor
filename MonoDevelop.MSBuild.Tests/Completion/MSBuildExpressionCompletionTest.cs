// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Expressions;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Options;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.MSBuild.Tests.Helpers;
using MonoDevelop.MSBuild.Util;
using MonoDevelop.Xml.Dom;
using MonoDevelop.Xml.Options;
using MonoDevelop.Xml.Parser;
using MonoDevelop.Xml.Tests;
using MonoDevelop.Xml.Tests.Utils;

using NUnit.Framework;

namespace MonoDevelop.MSBuild.Tests.Completion;

class MSBuildExpressionCompletionTest
{
	static MSBuildExpressionCompletionTest () => MSBuildTestHelpers.RegisterMSBuildAssemblies ();

	protected IEnumerable<ISymbol> GetExpressionCompletion (
		string sourceWithMarkers,
		out MSBuildRootDocument parsedDocument,
		char triggerChar = '\0',
		ExpressionCompletion.ExpressionTriggerReason reason = ExpressionCompletion.ExpressionTriggerReason.Invocation,
		MSBuildSchema schema = null
		)
	{
		const string projectFileName = "FakeProject.csproj";

		var token = CancellationToken.None;

		var schemas = new TestSchemaProvider ();
		if (schema is not null) {
			schemas.AddTestSchema (projectFileName, null, schema);
		}

		var environment = new NullMSBuildEnvironment ();
		var taskMetadataBuilder = new NoopTaskMetadataBuilder ();

		// internal errors should cause test failure
		var logger = TestLoggerFactory.CreateTestMethodLogger ().RethrowExceptions ();

		var textWithMarkers = TextWithMarkers.Parse (sourceWithMarkers, '|');
		var source = textWithMarkers.Text;
		int caretPos = textWithMarkers.GetMarkedPosition ();
		var textSource = new StringTextSource (source);

		parsedDocument = MSBuildRootDocument.Parse (
			textSource,
			projectFileName,
			null,
			schemas,
			environment,
			taskMetadataBuilder,
			logger,
			token);

		var spineParser = XmlSpineParser.FromDocumentPosition (new XmlRootState (), parsedDocument.XDocument, caretPos);

		var functionTypeProvider = new TestFunctionTypeProvider ();
		var rr = MSBuildResolver.Resolve (spineParser.Clone (), textSource, parsedDocument, functionTypeProvider, logger);

		Assert.NotNull (rr);

		if (triggerChar == '\0' && reason == ExpressionCompletion.ExpressionTriggerReason.Invocation) {
			reason = ExpressionCompletion.ExpressionTriggerReason.TypedChar;
		}

		// based on MSBuildCompletionSource.{GetExpressionCompletionsAsync,GetAdditionalCompletionsAsync}
		// eventually we can factor out into a shared method

		// TryGetIncompleteValue may return false while still outputting incomplete values, if it fails due to reaching maximum readahead.
		// It will also return false and output null values if we're in an element value that only contains whitespace.
		// In both these cases we can ignore the false return and proceed anyways.
		spineParser.TryGetIncompleteValue (textSource, out var expression, out var valueSpan);
		expression ??= "";
		int exprStartPos = valueSpan?.Start ?? caretPos;

		var triggerState = ExpressionCompletion.GetTriggerState (expression, caretPos - exprStartPos, reason, triggerChar, rr.IsCondition (),
			out int spanStart, out int spanLength, out ExpressionNode triggerExpression, out var listKind, out IReadOnlyList<ExpressionNode> comparandVariables,
			logger
		);

		if (triggerState == ExpressionCompletion.TriggerState.None) {
			return [];
		}

		var valueSymbol = rr.GetElementOrAttributeValueInfo (parsedDocument);
		if (valueSymbol is null || valueSymbol.ValueKind == MSBuildValueKind.Nothing) {
			return [];
		}

		var kind = valueSymbol.ValueKind;
		if (!ExpressionCompletion.ValidateListPermitted (listKind, kind)) {
			return [];
		}
		kind = kind.WithoutModifiers ();
		if (kind == MSBuildValueKind.Data || kind == MSBuildValueKind.Nothing) {
			return null;
		}

		var fileSystem = new TestFilesystem ();

		var options = new EmptyOptionsReader ();
		bool includePrivateSymbols = options.GetOption (MSBuildCompletionOptions.ShowPrivateSymbols);

		bool isValue = triggerState == ExpressionCompletion.TriggerState.Value;
		if (comparandVariables != null && isValue) {
			return ExpressionCompletion.GetComparandCompletions (parsedDocument, fileSystem, comparandVariables, logger, includePrivateSymbols);
		}

		return ExpressionCompletion.GetCompletionInfos (rr, triggerState, valueSymbol, triggerExpression, spanLength, parsedDocument, functionTypeProvider, fileSystem, logger, includePrivateSymbols);
	}

	class EmptyOptionsReader : IOptionsReader
	{
		public bool TryGetOption<T> (Option<T> option, out T value)
		{
			value = default;
			return false;
		}
	}

	class TestFunctionTypeProvider : IFunctionTypeProvider
	{
		public Task EnsureInitialized (CancellationToken token) => Task.CompletedTask;
		public ClassInfo GetClassInfo (string name) => null;
		public IEnumerable<ClassInfo> GetClassNameCompletions () => [];
		public ISymbol GetEnumInfo (string reference) => null;
		public FunctionInfo GetItemFunctionInfo (string name) => null;
		public IEnumerable<FunctionInfo> GetItemFunctionNameCompletions () => [];
		public FunctionInfo GetPropertyFunctionInfo (MSBuildValueKind valueKind, string name) => null;
		public IEnumerable<FunctionInfo> GetPropertyFunctionNameCompletions (ExpressionNode triggerExpression) => [];
		public FunctionInfo GetStaticPropertyFunctionInfo (string className, string name) => null;
		public MSBuildValueKind ResolveType (ExpressionPropertyNode node) => MSBuildValueKind.Unknown;
	}

	class TestFilesystem : IMSBuildFileSystem
	{
		public bool DirectoryExists (string basePath) => false;

		public IEnumerable<string> GetDirectories (string basePath) => [];

		public IEnumerable<string> GetFiles (string basePath) => [];
	}
}