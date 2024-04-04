// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

using MonoDevelop.MSBuild.Language;
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

				return true;
			} catch (Exception ex) {
				loggerFactory.GetLogger<MSBuildHelpCommandHandler> (args.TextView).LogInternalException (ex);
				throw;
			}
		}
	}
}
