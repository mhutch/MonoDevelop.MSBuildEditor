import * as vscode from 'vscode';
import * as lsp from 'vscode-languageclient';

export default async function hoverMiddleware(
	document: vscode.TextDocument,
	position: vscode.Position,
	token: vscode.CancellationToken,
	next: lsp.ProvideHoverSignature,
): Promise<vscode.Hover | null | undefined> {
	const result = await next(document, position, token);
	if (!result) {
		return result;
	}
	result.contents.map(content => {
		if (content instanceof vscode.MarkdownString) {
			content.supportThemeIcons = true;
			content.isTrusted = {
				enabledCommands: [
					'editor.action.goToReferences'
				]
			};
		}
	});
	return result;
}