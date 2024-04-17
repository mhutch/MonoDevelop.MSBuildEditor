// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Language;
using MonoDevelop.MSBuild.Language.Typesystem;
using MonoDevelop.MSBuild.Schema;
using MonoDevelop.Xml.Editor.Logging;
using MonoDevelop.Xml.Logging;

namespace MonoDevelop.MSBuild.Editor.Commands
{
	[Export (typeof (ICommandHandler))]
	[ContentType (MSBuildContentType.Name)]
	[Name (nameof (MSBuildGoToDefinitionCommandHandler))]
	class MSBuildHelpCommandHandler : ICommandHandler<HelpCommandArgs>
	{
		readonly MSBuildCachingResolver resolver;
		readonly IFunctionTypeProvider functionTypeProvider;
		readonly IEditorLoggerFactory loggerFactory;

		[ImportingConstructor]
		public MSBuildHelpCommandHandler (MSBuildCachingResolver resolver, IFunctionTypeProvider functionTypeProvider, IEditorLoggerFactory loggerFactory)
		{
			this.resolver = resolver;
			this.functionTypeProvider = functionTypeProvider;
			this.loggerFactory = loggerFactory;
		}

		public string DisplayName { get; } = "Open Documentation";

		public CommandState GetCommandState (HelpCommandArgs args)
		{
			try {
				return GetCommandStateInternal (args);
			} catch (Exception ex) {
				loggerFactory.GetLogger<MSBuildHelpCommandHandler> (args.TextView).LogInternalException (ex);
				throw;
			}
		}

		CommandState GetCommandStateInternal (HelpCommandArgs args)
		{
			resolver.GetResolvedReference (args.SubjectBuffer, args.TextView.Caret.Position.BufferPosition, out _, out var rr);
			return rr.ReferenceKind != MSBuildReferenceKind.None ? CommandState.Available : CommandState.Unavailable;
		}

		public bool ExecuteCommand (HelpCommandArgs args, CommandExecutionContext executionContext)
		{
			try {
				resolver.GetResolvedReference (args.SubjectBuffer, args.TextView.Caret.Position.BufferPosition, out var doc, out var rr, executionContext.OperationContext.UserCancellationToken);

				// todo: cancellation on GetResolvedReference
				var symbol = MSBuildCompletionExtensions.GetResolvedReference (rr, doc, functionTypeProvider);
				if (symbol is null) {
					return true;
				}

				if (symbol.HasHelpUrl (out string helpUrl)) {
					Process.Start (helpUrl);
					return true;
				}

				// for custom type values, fall back to the help page for the containing item/property/metadata
				if (symbol is CustomTypeValue knownValue) {
					var container = rr.AttributeSymbol ?? rr.ElementSymbol;
					if (container.CustomType == knownValue.CustomType && container.HasHelpUrl (out helpUrl)) {
						Process.Start (helpUrl);
						return true;
					}
				}

				// fall back to web search in some cases
				string webSearchQueryString = symbol switch {
					PropertyInfo prop => $"MSBuild {prop.Name} property",
					ItemInfo item => $"MSBuild {item.Name} item",
					TargetInfo target => $"MSBuild {target.Name} target",
					TaskInfo task => $"MSBuild {task.Name} task",
					TaskParameterInfo tpi => rr.ElementSymbol is TaskInfo t? $"MSBuild {t.Name} task {tpi.Name}" : null,
					MetadataInfo { Item: null } meta => $"MSBuild {meta.Name} metadata",
					MetadataInfo meta => $"MSBuild {meta.Item.Name} item {meta.Name} metadata",
					_ => null
				};

				if (webSearchQueryString is not null) {
					var webSearchUrl = ConstructWebSearchUrl (webSearchQueryString);
					Process.Start (webSearchUrl);
				}

				return true;
			} catch (Exception ex) {
				loggerFactory.GetLogger<MSBuildHelpCommandHandler> (args.TextView).LogInternalException (ex);
				throw;
			}
		}

		static string ConstructWebSearchUrl (string queryString)
		{
			var sb = PooledStringBuilder.GetInstance ();
			bool isFirst = false;
			sb.Builder.Append ("https://bing.com/search?q=");
			foreach (var token in queryString.Split (new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
				if (!isFirst) {
					sb.Builder.Append ('+');
				} else {
					isFirst = true;
				}
				sb.Builder.Append (Uri.EscapeDataString (token));
			}
			return sb.ToStringAndFree ();
		}
	}
}
