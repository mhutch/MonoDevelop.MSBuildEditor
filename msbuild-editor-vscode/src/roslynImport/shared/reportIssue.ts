// https://raw.githubusercontent.com/dotnet/vscode-csharp/ba156937926e760759a7e5e13bcf2d1be8729dc8/src/shared/reportIssue.ts
// https://raw.githubusercontent.com/dotnet/vscode-csharp/ba156937926e760759a7e5e13bcf2d1be8729dc8/src/lsptoolshost/commands.ts
// parameterized extension-specific info to make cleanly reusable

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import * as vscode from 'vscode';
import { IHostExecutableResolver } from '../shared/constants/IHostExecutableResolver';
import { basename, dirname } from 'path';
import { DotnetInfo } from './utils/dotnetInfo';

export default async function reportIssue(
	context: vscode.ExtensionContext,
	lspTraceOptionId : string,
    getDotnetInfo: (dotNetCliPaths: string[]) => Promise<DotnetInfo>,
    dotnetResolver: IHostExecutableResolver
) {
	const extensionId : string = context.extension.id;
	const extensionVersion : string = context.extension.packageJSON.version;
	const extensionName : string = context.extension.packageJSON.displayName;

    // Get info for the dotnet that the language server executable is run on, not the dotnet the language server will execute user code on.
    let fullDotnetInfo: string | undefined;
    try {
        const info = await dotnetResolver.getHostExecutableInfo();
        const dotnetInfo = await getDotnetInfo([dirname(info.path)]);
        fullDotnetInfo = dotnetInfo.FullInfo;
    } catch (error) {
        const message = error instanceof Error ? error.message : `${error}`;
        fullDotnetInfo = message;
    }

    const extensions = getInstalledExtensions();

    const body = `## Issue Description ##
## Steps to Reproduce ##

## Expected Behavior ##

## Actual Behavior ##

## Logs ##

<!--
If you can, it would be the most helpful to zip up and attach the entire extensions log folder.  The folder can be opened by running the \`workbench.action.openExtensionLogsFolder\` command.

Additionally, if you can reproduce the issue reliably, set the value of the \`${lspTraceOptionId}\` option to \`Trace\` and re-run the scenario to get more detailed logs.
-->

### C# log ###
<details>Post the output from Output-->${extensionName} here</details>

### C# LSP Trace Logs ###
<details>Post the output from Output-->${extensionName} LSP Trace Logs here.  Requires \`${lspTraceOptionId}\` to be set to \`Trace\`</details>

## Environment information ##

**VSCode version**: ${vscode.version}
**${extensionName} version**: ${extensionVersion}

<details><summary>Dotnet Information</summary>
${fullDotnetInfo}</details>
<details><summary>Visual Studio Code Extensions</summary>
${generateExtensionTable(extensions)}
</details>
`;

    await vscode.commands.executeCommand('workbench.action.openIssueReporter', {
        extensionId: extensionId,
        issueBody: body,
    });
}

function sortExtensions(a: vscode.Extension<any>, b: vscode.Extension<any>): number {
    if (a.packageJSON.name.toLowerCase() < b.packageJSON.name.toLowerCase()) {
        return -1;
    }
    if (a.packageJSON.name.toLowerCase() > b.packageJSON.name.toLowerCase()) {
        return 1;
    }
    return 0;
}

function generateExtensionTable(extensions: vscode.Extension<any>[]) {
    if (extensions.length <= 0) {
        return 'none';
    }

    const tableHeader = `|Extension|Author|Version|Folder Name|\n|---|---|---|---|`;
    const table = extensions
        .map(
            (e) =>
                `|${e.packageJSON.name}|${e.packageJSON.publisher}|${e.packageJSON.version}|${basename(
                    e.extensionPath
                )}|`
        )
        .join('\n');

    const extensionTable = `
${tableHeader}\n${table};
`;

    return extensionTable;
}

function getInstalledExtensions() {
    const extensions = vscode.extensions.all.filter((extension) => extension.packageJSON.isBuiltin === false);

    return extensions.sort(sortExtensions);
}