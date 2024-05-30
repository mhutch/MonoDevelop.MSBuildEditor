import * as vscode from 'vscode';
import * as roslyn from './roslynImport/main';
import { workspace, ExtensionContext } from 'vscode';
import { msbuildEditorOptions, MSBuildEditorOptions, MSBuildEditorOptionsImpl } from './options';

export function activate(context: ExtensionContext) {

	const options : MSBuildEditorOptions = new MSBuildEditorOptionsImpl();
	roslyn.activate(context, options);

	console.log('Congratulations, your extension "msbuild-editor" is now active!');

	/*
	let disposable = vscode.commands.registerCommand('msbuild-editor.helloWorld', () => {
		vscode.window.showInformationMessage('Hello World from msbuild-editor!');
	});
	context.subscriptions.push(disposable);
	*/
}

// This method is called when your extension is deactivated
export function deactivate() {
}
