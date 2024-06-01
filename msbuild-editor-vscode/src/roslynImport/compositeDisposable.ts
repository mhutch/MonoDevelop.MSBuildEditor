// https://raw.githubusercontent.com/dotnet/vscode-csharp/ba156937926e760759a7e5e13bcf2d1be8729dc8/src/compositeDisposable.ts

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import { Subscription } from 'rxjs';
import Disposable, { IDisposable } from './disposable';

export default class CompositeDisposable extends Disposable {
    private disposables = new Subscription();

    constructor(...disposables: IDisposable[]) {
        super(() => this.disposables.unsubscribe());

        for (const disposable of disposables) {
            if (disposable) {
                this.add(disposable);
            } else {
                throw new Error('null disposables are not supported');
            }
        }
    }

    public add(disposable: IDisposable) {
        if (!disposable) {
            throw new Error('disposable cannot be null');
        }

        this.disposables.add(() => disposable.dispose());
    }
}