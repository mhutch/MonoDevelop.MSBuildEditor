// https://raw.githubusercontent.com/dotnet/vscode-csharp/89da0f69901965627158f0120e1273ea2fc2486b/src/shared/observables/createOptionStream.ts
// parameterized the configuration name, removed the adapter

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

//import { vscode } from '../../vscodeAdapter';
import * as vscode from 'vscode';
import { Observable, Observer } from 'rxjs';
import { publishBehavior } from 'rxjs/operators';

export default function createOptionStream(/*vscode: vscode*/ observedConfigurationName : string): Observable<void> {
    return Observable.create((observer: Observer<void>) => {
        const disposable = vscode.workspace.onDidChangeConfiguration((e) => {
            //if the observed are affected only then read the options
            if ( e.affectsConfiguration(observedConfigurationName)) {
                observer.next();
            }
        });

        return () => disposable.dispose();
    })
        .pipe(
            publishBehavior(() => {
                return;
            })
        )
        .refCount();
}