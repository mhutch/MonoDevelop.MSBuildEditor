// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.MSBuildEditor.Language;
using NuGet.Frameworks;

namespace MonoDevelop.MSBuildEditor.Schema
{
	static class MSBuildCompletionExtensions
	{
		public static IEnumerable<BaseInfo> GetAttributeCompletions (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas, MSBuildToolsVersion tv)
		{
			foreach (var att in rr.LanguageElement.Attributes) {
				if (!att.IsAbstract) {
					yield return att;
				}
			}

			if (rr.LanguageElement.Kind == MSBuildKind.Item && tv.IsAtLeast (MSBuildToolsVersion.V15_0)) {
				foreach (var item in schemas.GetMetadata (rr.ElementName, false)) {
					yield return item;
				}
			}

			if (rr.LanguageElement.Kind == MSBuildKind.Task) {
				foreach (var parameter in schemas.GetTaskParameters (rr.ElementName)) {
					yield return parameter;

				}
			}
		}

		static IEnumerable<BaseInfo> GetAbstractAttributes (this IEnumerable<IMSBuildSchema> schemas, MSBuildKind kind, string elementName)
		{
			switch (kind) {
			case MSBuildKind.Item:
				return schemas.GetItems ();
			case MSBuildKind.Task:
				return schemas.GetTasks ();
			case MSBuildKind.Property:
				return schemas.GetProperties (false);
			case MSBuildKind.Metadata:
				return schemas.GetMetadata (elementName, false);
			}
			return null;
		}

		public static IEnumerable<BaseInfo> GetElementCompletions (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			if (rr?.LanguageElement == null) {
				yield return MSBuildLanguageElement.Get ("Project");
				yield break;
			}

			if (rr.LanguageElement.Children == null) {
				yield break;
			}

			foreach (var c in rr.LanguageElement.Children) {
				if (c.IsAbstract) {
					var abstractChildren = GetAbstractChildren (schemas, rr.LanguageElement.AbstractChild.Kind, rr.ElementName);
					if (abstractChildren != null) {
						foreach (var child in abstractChildren) {
							yield return child;
						}
					}
				} else {
					yield return c;
				}
			}
		}

		static IEnumerable<BaseInfo> GetAbstractChildren (this IEnumerable<IMSBuildSchema> schemas, MSBuildKind kind, string elementName)
		{
			switch (kind) {
			case MSBuildKind.Item:
				return schemas.GetItems ();
			case MSBuildKind.Task:
				return schemas.GetTasks ();
			case MSBuildKind.Property:
				return schemas.GetProperties (false);
			case MSBuildKind.Metadata:
				return schemas.GetMetadata (elementName, false);
			}
			return null;
		}

		public static IReadOnlyList<BaseInfo> GetValueCompletions (MSBuildValueKind kind)
		{
			switch (kind.GetDatatype ()) {
			case MSBuildValueKind.Bool:
				return new BaseInfo [] {
					new ConstantInfo ("True", null),
					new ConstantInfo ("False", null),
				};
			case MSBuildValueKind.TaskArchitecture:
				return new BaseInfo [] {
					new ConstantInfo ("*", "Any architecture"),
					new ConstantInfo ("CurrentArchitecture", "The architecture on which MSBuild is running"),
					new ConstantInfo ("x86", "The 32-bit x86 architecture"),
					new ConstantInfo ("x64", "The 64-bit x64 architecture"),
				};
			case MSBuildValueKind.TaskRuntime:
				return new BaseInfo [] {
					new ConstantInfo ("*", "Any runtime"),
					new ConstantInfo ("CurrentRuntime", "The runtime on which MSBuild is running"),
					new ConstantInfo ("CLR2", "The .NET 2.0 runtime"),
					new ConstantInfo ("CLR4", "The .NET 4.0 runtime"),
				};
			case MSBuildValueKind.Importance:
				return new BaseInfo [] {
					new ConstantInfo ("high", "High importance, only displayed for all log verbosity settings"),
					new ConstantInfo ("normal", "Normal importance"),
					new ConstantInfo ("low", "Low importance, only displayed for highly verbose log settings")
				};
			case MSBuildValueKind.HostOS:
				return new BaseInfo [] {
					new ConstantInfo ("Windows_NT", "Running on Windows"),
					new ConstantInfo ("Unix", "Running on Unix")
					// deliberately ignoring Mac as it doesn't actually work
				};
			case MSBuildValueKind.HostRuntime:
				return new BaseInfo [] {
					new ConstantInfo ("Mono", "Running on Mono"),
					new ConstantInfo ("Core", "Running on .NET Core"),
					new ConstantInfo ("Full", "Running on .NET Framework")
				};
			case MSBuildValueKind.ContinueOnError:
				return new BaseInfo [] {
					new ConstantInfo ("WarnAndContinue", "When the task outputs errors, convert them to warnings, and continue executing other tasks and targets"),
					new ConstantInfo ("true", "Equivalent to `WarnAndContinue`"),
					new ConstantInfo ("ErrorAndContinue", "When the task outputs errors, continue executing other tasks and targets"),
					new ConstantInfo ("ErrorAndStop", "When the task outputs errors, do not execute further tasks and targets"),
					new ConstantInfo ("true", "Equivalent to `ErrorAndStop`"),

				};
			case MSBuildValueKind.TargetFramework:
				var frameworkNames = new List<BaseInfo> ();
				var provider = DefaultFrameworkNameProvider.Instance;
				foreach (var fx in provider.GetCompatibleCandidates ()) {
					if (fx.IsSpecificFramework && fx.Version.Major != int.MaxValue) {
						frameworkNames.Add (new ConstantInfo (
							fx.GetShortFolderName (),
							fx.GetDotNetFrameworkName (provider)
						));
					}
				}
				return frameworkNames;
			}
			return null;
		}

		public static BaseInfo GetResolvedReference (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			switch (rr.ReferenceKind) {
			case MSBuildReferenceKind.Item:
				return schemas.GetItem (rr.ReferenceName);
			case MSBuildReferenceKind.Metadata:
				return schemas.GetMetadata (rr.ReferenceItemName, rr.ReferenceName, true);
			case MSBuildReferenceKind.Property:
				return schemas.GetProperty (rr.ReferenceName);
			case MSBuildReferenceKind.Task:
				return schemas.GetTask (rr.ReferenceName);
			case MSBuildReferenceKind.Keyword:
				var attName = rr.AttributeName;
				if (attName != null) {
					var att = rr.LanguageElement.GetAttribute (attName);
					if (att != null && !att.IsAbstract) {
						return att;
					}
				} else {
					if (!rr.LanguageElement.IsAbstract) {
						return rr.LanguageElement;
					}
				}
				break;
			}
			return null;
		}

		public static VariableInfo GetElementOrAttributeValueInfo (this MSBuildResolveResult rr, IEnumerable<IMSBuildSchema> schemas)
		{
			if (rr.LanguageElement == null) {
				return null;
			}

			if (rr.AttributeName != null) {
				var att = rr.LanguageElement.GetAttribute (rr.AttributeName);
				if (att.IsAbstract) {
					switch (att.AbstractKind.Value) {
					case MSBuildKind.TaskParameter:
						return schemas.GetTaskParameter (rr.ElementName, rr.AttributeName);
					case MSBuildKind.Metadata:
						return schemas.GetMetadata (rr.ElementName, rr.AttributeName, false);
					default:
						throw new InvalidOperationException ($"Unsupported abstract attribute kind {att.AbstractKind}");
					}
				}

				if (att.ValueKind == MSBuildValueKind.MatchItem) {
					var item = schemas.GetItem (rr.ElementName);
					return new MSBuildLanguageAttribute (
						att.Name, att.Description, item.ValueKind, att.Required, att.AbstractKind
					);
				}

				return att;
			}

			if (rr.LanguageElement.IsAbstract) {
				switch (rr.LanguageElement.Kind) {
				case MSBuildKind.Item:
				case MSBuildKind.ItemDefinition:
					//item doesn't have any value completions
					return null;
				case MSBuildKind.Metadata:
					return schemas.GetMetadata (rr.ParentName, rr.ElementName, false);
				case MSBuildKind.Property:
					return schemas.GetProperty (rr.ElementName);
				case MSBuildKind.TaskParameter:
					return schemas.GetTaskParameter (rr.ElementName, rr.AttributeName);
				default:
					throw new InvalidOperationException ($"Unsupported abstract element kind {rr.LanguageElement.Kind}");
				}
			}

			return null;
		}
	}
}
