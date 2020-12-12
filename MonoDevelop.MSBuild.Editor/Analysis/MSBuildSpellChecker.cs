// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Utilities;

using MonoDevelop.MSBuild.Editor.Completion;
using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;

using Roslyn.Utilities;

namespace MonoDevelop.MSBuild.Editor.Analysis
{
	class MSBuildSpellChecker
	{
		object locker = new object ();
		MSBuildDocument lastDocument;

		Task<SpellChecker> itemCheckerTask, propertyCheckerTask;
		Dictionary<string, Task<SpellChecker>> metadataCheckerTasks
			= new Dictionary<string, Task<SpellChecker>> (StringComparer.OrdinalIgnoreCase);

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
			}
		}

		Task<SpellChecker> GetItemChecker (MSBuildDocument document)
		{
			lock (locker) {
				CheckHash (document);
				return itemCheckerTask ?? (itemCheckerTask = Task.Run (() =>
					new SpellChecker (
						Checksum.Null,
						document.GetSchemas ().GetItems ().Select (i => new StringSlice (i.Name)))
					)
				);
			}
		}

		Task<SpellChecker> GetPropertyChecker (MSBuildDocument document)
		{
			lock (locker) {
				CheckHash (document);
				return propertyCheckerTask ?? (propertyCheckerTask = Task.Run (() =>
					new SpellChecker (
						Checksum.Null,
						document.GetSchemas ().GetProperties (true).Select (p => new StringSlice (p.Name)))
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
							Checksum.Null,
							document.GetSchemas ().GetMetadata (itemName, true).Select (p => new StringSlice (p.Name)))
						);
				}
				return checker;
			}
		}

		public async Task<IEnumerable<ItemInfo>> FindSimilarItems (MSBuildDocument document, string name)
			=> GetItems (document, await GetItemChecker (document), name);

		IEnumerable<ItemInfo> GetItems (MSBuildDocument document, SpellChecker checker, string name)
		{
			foreach (var match in checker.FindSimilarWords (name)) {
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
			foreach (var match in checker.FindSimilarWords (name)) {
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
			foreach (var match in checker.FindSimilarWords (name)) {
				if (string.Equals (match, name, StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				if (document.GetSchemas ().GetMetadata (itemName, match, true) is MetadataInfo info) {
					yield return info;
				}
			}
		}
	}
}
