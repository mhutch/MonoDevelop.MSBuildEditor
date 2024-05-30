// https://raw.githubusercontent.com/dotnet/vscode-csharp/ba156937926e760759a7e5e13bcf2d1be8729dc8/src/shared/constants/IHostExecutableResolver.ts

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import { HostExecutableInformation } from './hostExecutableInformation';

export interface IHostExecutableResolver {
    getHostExecutableInfo(): Promise<HostExecutableInformation>;
}