// https://raw.githubusercontent.com/dotnet/vscode-csharp/89da0f69901965627158f0120e1273ea2fc2486b/src/shared/observers/optionChangeObserver.ts
// made generic and simplified to only deal with one option type

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import { Observable } from 'rxjs';
import Disposable from '../../disposable';
import { isDeepStrictEqual } from 'util';

export function HandleOptionChanges<TOptions>(
    optionObservable: Observable<void>,
    optionChangeObserver: OptionChangeObserver<TOptions>,
	options : TOptions
): Disposable {
    let oldRelevantOptions: Map<keyof TOptions, any>;
    const subscription = optionObservable.pipe().subscribe(() => {
        const relevantKeys = optionChangeObserver.getRelevantOptions();
        const newRelevantOptions = new Map(relevantKeys.map((key) => [key, options[key]]));

        if (!oldRelevantOptions) {
            oldRelevantOptions = newRelevantOptions;
        }

        const changedRelevantOptions = relevantKeys.filter(
            (key) => !isDeepStrictEqual(oldRelevantOptions.get(key), newRelevantOptions.get(key))
        );

        oldRelevantOptions = newRelevantOptions;

        if (changedRelevantOptions.length > 0) {
            optionChangeObserver.handleOptionChanges(changedRelevantOptions);
        }
    });

    return new Disposable(subscription);
}

export interface OptionChangeObserver<TOptions> {
    getRelevantOptions: () => ReadonlyArray<keyof TOptions>;
    handleOptionChanges: (optionChanges: ReadonlyArray<keyof TOptions>) => void;
}
