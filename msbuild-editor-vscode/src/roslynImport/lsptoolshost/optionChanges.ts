// https://raw.githubusercontent.com/dotnet/vscode-csharp/ba156937926e760759a7e5e13bcf2d1be8729dc8/src/lsptoolshost/optionChanges.ts
// altered to deal with a single option type and to restart the server in a simpler way

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import { Observable } from 'rxjs';
import { HandleOptionChanges, OptionChangeObserver } from '../shared/observers/optionChangeObserver';
import Disposable from '../disposable';
import { RoslynLanguageServer, RoslynLanguageServerOptions } from './roslynLanguageServer';

export function registerLanguageServerOptionChanges(
	optionObservable: Observable<void>,
	languageServer : RoslynLanguageServer,
	lspOptions : RoslynLanguageServerOptions,
	lspOptionsThatTriggerReload : ReadonlyArray<keyof RoslynLanguageServerOptions>
): Disposable {
    const optionChangeObserver: OptionChangeObserver<RoslynLanguageServerOptions> = {
        getRelevantOptions: () => lspOptionsThatTriggerReload,
        handleOptionChanges(optionChanges) {
			if (optionChanges.length !== 0) {
				languageServer.restart();
			}
        },
    };

    const disposable = HandleOptionChanges(optionObservable, optionChangeObserver, lspOptions);
    return disposable;
}