// https://raw.githubusercontent.com/dotnet/vscode-csharp/ba156937926e760759a7e5e13bcf2d1be8729dc8/src/lsptoolshost/commands.ts
// reduced down to just a few reusable commands and parameterized extension-specific info

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import { RoslynLanguageServer } from './roslynLanguageServer';
import reportIssue from '../shared/reportIssue';
import { getDotnetInfo } from '../shared/utils/getDotnetInfo';
import { IHostExecutableResolver } from '../shared/constants/IHostExecutableResolver';

export function registerCommands(
	commandPrefix:string,
    context: vscode.ExtensionContext,
    languageServer: RoslynLanguageServer,
    hostExecutableResolver: IHostExecutableResolver,
    outputChannel: vscode.OutputChannel
) {
    context.subscriptions.push(
        vscode.commands.registerCommand(`${commandPrefix}.restartServer`, async () => restartServer(languageServer))
    );
    context.subscriptions.push(
        vscode.commands.registerCommand(`${commandPrefix}.reportIssue`, async () =>
            reportIssue(
                context,
                `${commandPrefix}.server.trace`,
                getDotnetInfo,
                hostExecutableResolver
            )
        )
    );
    context.subscriptions.push(
        vscode.commands.registerCommand(`${commandPrefix}.showOutputWindow`, async () => outputChannel.show())
    );
}

async function restartServer(languageServer: RoslynLanguageServer): Promise<void> {
    await languageServer.restart();
}
