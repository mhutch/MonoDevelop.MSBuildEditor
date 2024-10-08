// https://raw.githubusercontent.com/dotnet/vscode-csharp/ba156937926e760759a7e5e13bcf2d1be8729dc8/src/disposable.ts

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import { Subscription } from 'rxjs';

export default class Disposable implements IDisposable {
    private onDispose: { (): void };

    constructor(onDispose: { (): void } | Subscription) {
        if (!onDispose) {
            throw new Error('onDispose cannot be null or empty.');
        }

        if (onDispose instanceof Subscription) {
            this.onDispose = () => onDispose.unsubscribe();
        } else {
            this.onDispose = onDispose;
        }
    }

    public dispose = (): void => {
        this.onDispose();
    };
}

export interface IDisposable {
    dispose: () => void;
}