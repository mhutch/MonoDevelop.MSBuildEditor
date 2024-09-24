// https://raw.githubusercontent.com/dotnet/vscode-csharp/89da0f69901965627158f0120e1273ea2fc2486b/src/shared/options.ts
// removed all the option definitions, now only has helpers

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';

function getExcludedPaths(): string[] {
    const workspaceConfig = vscode.workspace.getConfiguration();

    const excludePaths = getExcludes(workspaceConfig, 'files.exclude');
    return excludePaths;

    function getExcludes(config: vscode.WorkspaceConfiguration, option: string): string[] {
        const optionValue = config.get<{ [i: string]: boolean }>(option, {});
        return Object.entries(optionValue)
            .filter(([_, value]) => value)
            .map(([key, _]) => key);
    }
}

/**
 * Reads an option from the vscode config with an optional back compat parameter.
 */
function readOptionFromConfig<T>(
    config: vscode.WorkspaceConfiguration,
    option: string,
    defaultValue: T,
    ...backCompatOptionNames: string[]
): T {
    let value = config.get<T>(option);

    if (value === undefined && backCompatOptionNames.length > 0) {
        // Search the back compat options for a defined value.
        value = backCompatOptionNames.map((name) => config.get<T>(name)).find((val) => val);
    }

    return value ?? defaultValue;
}

export function readOption<T>(option: string, defaultValue: T, ...backCompatOptionNames: string[]): T {
    return readOptionFromConfig(vscode.workspace.getConfiguration(), option, defaultValue, ...backCompatOptionNames);
}
