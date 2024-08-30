// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using ISymbol = MonoDevelop.MSBuild.Language.ISymbol;

using Roslyn.Utilities;
using Microsoft.CodeAnalysis.Shared.Collections;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	class MSBuildSpellChecker
	{
		object locker = new object ();
		MSBuildDocument lastDocument;

		Task<SpellChecker> itemCheckerTask, propertyCheckerTask;
		readonly Dictionary<string, Task<SpellChecker>> metadataCheckerTasks = new(StringComparer.OrdinalIgnoreCase);
		readonly Dictionary<CustomTypeInfo, Task<SpellChecker>> customTypeCheckerTasks = new();

		// this does not need to be cleared when the doc changes
		readonly Dictionary<MSBuildValueKind, Task<SpellChecker>> valueKindCheckerTasks = new ();

		public MSBuildSpellChecker ()
		{
		}

		void CheckHash (MSBuildDocument document)
		{
			if (lastDocument?.ImportsHash != document.ImportsHash) {
				lastDocument = document;
				itemCheckerTask = null;
				propertyCheckerTask = null;
				metadataCheckerTasks.Clear ();
				customTypeCheckerTasks.Clear ();
			}
		}

		Task<SpellChecker> GetItemChecker (MSBuildDocument document)
		{
			lock (locker) {
				CheckHash (document);
				return itemCheckerTask ??= Task.Run (() =>
					new SpellChecker (
						document.GetSchemas ().GetItems ().Select (i => i.Name)
					)
				);
			;
			}
		}

		Task<SpellChecker> GetPropertyChecker (MSBuildDocument document)
		{
			lock (locker) {
				CheckHash (document);
				return propertyCheckerTask ??= Task.Run (() =>
					new SpellChecker (
						document.GetSchemas ().GetProperties (true).Select (p => p.Name)
					)
				);
			}
		}

		Task<SpellChecker> GetMetadataChecker (MSBuildDocument document, string itemName)
		{
			lock (locker) {
				CheckHash (document);
				if (!metadataCheckerTasks.TryGetValue (itemName, out var checker)) {
					metadataCheckerTasks[itemName] = checker = Task.Run (() =>
						new SpellChecker (
							document.GetSchemas ().GetMetadata (itemName, true).Select (p => p.Name)
						)
					);
				}
				return checker;
			}
		}

		Task<SpellChecker> GetValueChecker (MSBuildDocument document, MSBuildValueKind kind, CustomTypeInfo customType)
		{
			Task<SpellChecker> checker;
			lock (locker) {
				if (customType is null) {
					if (!valueKindCheckerTasks.TryGetValue (kind, out checker)) {
						var knownVals = kind.GetSimpleValues ();

						valueKindCheckerTasks[kind] = checker = Task.Run (() =>
							new SpellChecker (
								knownVals.Select (p => p.Name))
							);
					}
					return checker;
				}

				CheckHash (document);
				if (!customTypeCheckerTasks.TryGetValue (customType, out checker)) {
					customTypeCheckerTasks[customType] = checker = Task.Run (() => {
						var knownVals = customType.Values;
						return new SpellChecker (
							knownVals.Select (p => p.Name)
						);
					});
				}
				return checker;
			}
		}

		public async Task<IEnumerable<ItemInfo>> FindSimilarItems (MSBuildDocument document, string name)
			=> GetItems (document, await GetItemChecker (document), name);

		IEnumerable<ItemInfo> GetItems (MSBuildDocument document, SpellChecker checker, string name)
		{
			var matches = TemporaryArray<string>.Empty;
			checker.FindSimilarWords (ref matches, name, false);

			foreach (var match in matches) {
				if (string.Equals (match, name, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if (document.GetSchemas ().GetItem (match) is ItemInfo info) {
					yield return info;
				}
			}
		}

		public async Task<IEnumerable<PropertyInfo>> FindSimilarProperties (MSBuildDocument document, string name)
			=> GetProperties (document, await GetPropertyChecker (document), name);

		IEnumerable<PropertyInfo> GetProperties (MSBuildDocument document, SpellChecker checker, string name)
		{
			var matches = TemporaryArray<string>.Empty;
			checker.FindSimilarWords (ref matches, name, false);

			foreach (var match in matches) {
				if (string.Equals (match, name, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if (document.GetSchemas ().GetProperty (match, true) is PropertyInfo info) {
					yield return info;
				}
			}
		}

		public async Task<IEnumerable<MetadataInfo>> FindSimilarMetadata (MSBuildDocument document, string itemName, string name)
			=> GetMetadata (document, await GetMetadataChecker (document, itemName), itemName, name);

		IEnumerable<MetadataInfo> GetMetadata (MSBuildDocument document, SpellChecker checker, string itemName, string name)
		{
			var matches = TemporaryArray<string>.Empty;
			checker.FindSimilarWords (ref matches, name, false);

			foreach (var match in matches) {
				if (string.Equals (match, name, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if (document.GetSchemas ().GetMetadata (itemName, match, true) is MetadataInfo info) {
					yield return info;
				}
			}
		}

		public async Task<IEnumerable<ISymbol>> FindSimilarValues (MSBuildDocument document, MSBuildValueKind kind, CustomTypeInfo customType, string name)
			=> GetValue (await GetValueChecker (document, kind, customType), kind, customType, name);

		IEnumerable<ISymbol> GetValue (SpellChecker checker, MSBuildValueKind kind, CustomTypeInfo customType, string name)
		{
			var knownVals = (IReadOnlyList<ISymbol>)customType?.Values ?? kind.GetSimpleValues ();
			var valueComparer = (customType?.CaseSensitive ?? false) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

			var knownValDict = knownVals.ToDictionary (v => v.Name, StringComparer.OrdinalIgnoreCase);

			var matches = TemporaryArray<string>.Empty;
			checker.FindSimilarWords (ref matches, name, false);

			foreach (var match in matches) {
				if (string.Equals (match, name, valueComparer)) {
					continue;
				}
				if (knownValDict.TryGetValue (match, out var info)) {
					yield return info;
				}
			}
		}
	}
}
