// https://raw.githubusercontent.com/dotnet/vscode-csharp/ba156937926e760759a7e5e13bcf2d1be8729dc8/src/packageManager/absolutePath.ts

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as path from 'path';

export class AbsolutePath {
    constructor(public value: string) {
        if (!path.isAbsolute(value)) {
            throw new Error('The path must be absolute');
        }
    }

    public static getAbsolutePath(...pathSegments: string[]): AbsolutePath {
        return new AbsolutePath(path.resolve(...pathSegments));
    }
}