import * as vscode from 'vscode';
import * as roslyn from './roslynImport/main';
import { workspace, ExtensionContext } from 'vscode';
import { msbuildEditorOptions } from './options';
import hoverMiddleware from './hoverMiddleware';
import completionItemMiddleware from './completionItemMiddleware';
import { RoslynLanguageServerDefinition } from './roslynImport/lsptoolshost/roslynLanguageServer';
import path from 'path';

const msbuildDocumentSelector: vscode.DocumentSelector = [{ scheme: 'file', language: 'msbuild' }];

export function activate(context: ExtensionContext) {
	const lspDefinition : RoslynLanguageServerDefinition = {
		clientId: 'msbuild-lsp',
		clientName: 'MSBuild LSP',
		clientOptions:  {
			// FIXME: use msbuildDocumentSelector here, currently results in weird type error
			documentSelector: [{ scheme: 'file', language: 'msbuild' }],
			synchronize: {
				fileEvents: [],
			},
			markdown: {
				supportHtml: true
			},
			middleware: {
				provideHover: hoverMiddleware,
				resolveCompletionItem: completionItemMiddleware
			}
		},
		serverPathEnvVar: 'MSBUILD_LANGUAGE_SERVER_PATH',
		bundledServerPath: path.join(context.extensionPath, 'server', 'MSBuildLanguageServer.dll'),
		commandIdPrefix: 'msbuild'
	};

	roslyn.activate(context, msbuildEditorOptions, lspDefinition);

	context.subscriptions.push(
		handleCompletionTrigger()
	);

	/*
	let disposable = vscode.commands.registerCommand('msbuild-editor.helloWorld', () => {
		vscode.window.showInformationMessage('Hello World from msbuild-editor!');
	});
	context.subscriptions.push(disposable);
	*/
}

export function deactivate() {
}

// trigger completion automatically in a few places where VS Code would not normally do so
function handleCompletionTrigger(): vscode.Disposable {
	return vscode.workspace.onDidChangeTextDocument(async (e) => {
		if (!vscode.languages.match(msbuildDocumentSelector, e.document)) {
			return;
		}

		// don't support completion for multi-caret editing
		if (e.contentChanges.length !== 1) {
			return;
		}

		// only if the change is in the active document
		if(e.document !== vscode.window.activeTextEditor?.document) {
			return;
		}

		const change = e.contentChanges[0];

		if(isExpressionCompletionInsertion(change) || isAttributeStartCharTrigger(change, e.document)) {
			await vscode.commands.executeCommand('editor.action.triggerSuggest');
		}
	});
}

// Trigger completion immediately after using completion to commit $(), @(), or %().
// If the user manually types the expression out, then completion will be triggered
// by the server when they type '(', as it is a trigger char.
function isExpressionCompletionInsertion(change: vscode.TextDocumentContentChangeEvent) : boolean
{
	switch(change.text) {
		case '$()':
		case '@()':
		case '%()':
		case '$(':
		case '@(':
		case '%(':
			// we cannot determine whether the change came from completion or from paste etc, so this could
			// be accidentally triggered. however, we can at least verify that the caret is where we expect.
			return vscode.window.activeTextEditor?.selection.start.compareTo(change.range.start) === 0;
		default:
			return false;
	}
}

// For some reason VS Code does not trigger completion when typing the first char in an empty attribute.
// This function attempts to trigger completion in that case i.e. typing an alphanumeric character between quotes.
// It's pretty naive and we could probably check more context to make sure we're inside an attribute.
function isAttributeStartCharTrigger(change: vscode.TextDocumentContentChangeEvent, document: vscode.TextDocument) : boolean
{
	// trigger for any alphanumeric char or underscore
	if(!change.text.match(/[a-zA-Z0-9_]/)) {
		return false;
	}

	// we're checking if we're inside quotes, so can't be at the start of the line
	if (change.range.start.character === 0) {
		return false;
	}

	// get the chars before and after the change.
	var range = new vscode.Range(change.range.start.translate(0, -1), change.range.end.translate(0, 1));
	var text = document.getText(range);

	// the previous char must be a an attribute quote
	var quoteChar = text[0];
	if(!isAttributeQuote(quoteChar)) {
		return false;
	}

	// to trigger completion after typing an alphanumeric char after a quote, the next char must be the matching quote or EOL
	var nextCharIdx = change.range.start.character === 0 ? 1 : 2;
	if(text.length > nextCharIdx && text[nextCharIdx] !== quoteChar){
		return false;
	}

	return true;
}

function isAttributeQuote(text : string) : boolean
{
	return text === '"' || text === "'";
}
