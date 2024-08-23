import * as vscode from 'vscode';
import * as lsp from 'vscode-languageclient';

export default async function completionItemMiddleware(
	item: vscode.CompletionItem,
	token: vscode.CancellationToken,
	next: lsp.ResolveCompletionItemSignature,
): Promise<vscode.CompletionItem | null | undefined> {
	const result = await next(item, token);
	if (!result) {
		return result;
	}
	if (result.documentation instanceof vscode.MarkdownString) {
		result.documentation.supportThemeIcons = true;
		result.documentation.isTrusted = {
			enabledCommands: [
				'editor.action.goToReferences'
			]
		};
	}
	return result;
}