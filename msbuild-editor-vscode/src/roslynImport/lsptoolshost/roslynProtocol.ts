// https://raw.githubusercontent.com/dotnet/vscode-csharp/ba156937926e760759a7e5e13bcf2d1be8729dc8/src/lsptoolshost/roslynProtocol.ts

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import { Command } from 'vscode';
import * as lsp from 'vscode-languageserver-protocol';

export interface ShowToastNotificationParams {
    messageType: lsp.MessageType;
    message: string;
    commands: Command[];
}

export interface NamedPipeInformation {
    pipeName: string;
}

export namespace ShowToastNotification {
    export const method = 'window/_roslyn_showToast';
    export const messageDirection: lsp.MessageDirection = lsp.MessageDirection.serverToClient;
    export const type = new lsp.NotificationType<ShowToastNotificationParams>(method);
}