// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

using MonoDevelop.MSBuild.Schema;

namespace MonoDevelop.MSBuild.Analysis
{
	/*
	class MSBuildAnalysisContextImpl : MSBuildAnalysisContext
	{
		Dictionary<MSBuildKind, List<Action<ResolvedElementDiagnosticContext>>> elementActions = new Dictionary<MSBuildKind, List<Action<ResolvedElementDiagnosticContext>>> ();
		Dictionary<string, List<Action<ItemDiagnosticContext>>> itemActions = new Dictionary<string, List<Action<ItemDiagnosticContext>>> ();
		Dictionary<string, List<Action<PropertyDiagnosticContext>>> propertyActions = new Dictionary<string, List<Action<PropertyDiagnosticContext>>> ();

		public override void RegisterElementAction (Action<ResolvedElementDiagnosticContext> action, ImmutableArray<MSBuildKind> elementKinds)
		{
			foreach (var kind in elementKinds) {
				if (!elementActions.TryGetValue (kind, out var list)) {
					list = new List<Action<ResolvedElementDiagnosticContext>> ();
					elementActions.Add (kind, list);
				}
				list.Add (action);
			}
		}

		public override void RegisterItemAction (Action<ItemDiagnosticContext> action, ImmutableArray<string> itemNames)
		{
			foreach (var name in itemNames) {
				if (!itemActions.TryGetValue (name, out var list)) {
					list = new List<Action<ItemDiagnosticContext>> ();
					itemActions.Add (name, list);
				}
				list.Add (action);
			}
		}

		public override void RegisterPropertyAction (Action<PropertyDiagnosticContext> action, ImmutableArray<string> propertyNames)
		{
			foreach (var name in propertyNames) {
				if (!propertyActions.TryGetValue (name, out var list)) {
					list = new List<Action<PropertyDiagnosticContext>> ();
					propertyActions.Add (name, list);
				}
				list.Add (action);
			}
		}
	}
	*/
}
