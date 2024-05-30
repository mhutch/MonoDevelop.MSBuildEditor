// https://raw.githubusercontent.com/dotnet/vscode-csharp/89da0f69901965627158f0120e1273ea2fc2486b/src/omnisharp/loggingEvents.ts
// culled a lot of types we aren't using, more to come

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

import { PlatformInformation } from '../shared/platform';
import { EventType } from './eventType';

export interface BaseEvent {
    type: EventType;
}

export class TelemetryEvent implements BaseEvent {
    type = EventType.TelemetryEvent;
    constructor(
        public eventName: string,
        public properties?: { [key: string]: string },
        public measures?: { [key: string]: number }
    ) {}
}

export class TelemetryErrorEvent implements BaseEvent {
    type = EventType.TelemetryErrorEvent;
    constructor(
        public eventName: string,
        public properties?: { [key: string]: string },
        public measures?: { [key: string]: number },
        public errorProps?: string[]
    ) {}
}

export class TelemetryEventWithMeasures implements BaseEvent {
    type = EventType.TelemetryEvent;
    constructor(public eventName: string, public measures: { [key: string]: number }) {}
}

export class PackageInstallStart implements BaseEvent {
    type = EventType.PackageInstallStart;
}

export class PackageInstallation implements BaseEvent {
    type = EventType.PackageInstallation;
    constructor(public packageInfo: string) {}
}

export class LogPlatformInfo implements BaseEvent {
    type = EventType.LogPlatformInfo;
    constructor(public info: PlatformInformation) {}
}

export class InstallationStart implements BaseEvent {
    type = EventType.InstallationStart;
    constructor(public packageDescription: string) {}
}

export class InstallationFailure implements BaseEvent {
    type = EventType.InstallationFailure;
    constructor(public stage: string, public error: any) {}
}

export class DownloadProgress implements BaseEvent {
    type = EventType.DownloadProgress;
    constructor(public downloadPercentage: number, public packageDescription: string) {}
}

export class TestExecutionCountReport implements BaseEvent {
    type = EventType.TestExecutionCountReport;
    constructor(
        public debugCounts: { [testFrameworkName: string]: number } | undefined,
        public runCounts: { [testFrameworkName: string]: number } | undefined
    ) {}
}

export class EventWithMessage implements BaseEvent {
    type = EventType.EventWithMessage;
    constructor(public message: string) {}
}

export class DownloadStart implements BaseEvent {
    type = EventType.DownloadStart;
    constructor(public packageDescription: string) {}
}

export class DownloadFallBack implements BaseEvent {
    type = EventType.DownloadFallBack;
    constructor(public fallbackUrl: string) {}
}

export class DownloadSizeObtained implements BaseEvent {
    type = EventType.DownloadSizeObtained;
    constructor(public packageSize: number) {}
}

export class ZipError implements BaseEvent {
    type = EventType.ZipError;
    constructor(public message: string) {}
}

export class DotNetTestRunStart implements BaseEvent {
    type = EventType.DotNetTestRunStart;
    constructor(public testMethod: string) {}
}

export class DotNetTestDebugStart implements BaseEvent {
    type = EventType.DotNetTestDebugStart;
    constructor(public testMethod: string) {}
}

export class DotNetTestDebugProcessStart implements BaseEvent {
    type = EventType.DotNetTestDebugProcessStart;
    constructor(public targetProcessId: number) {}
}

export class DotNetTestsInClassRunStart implements BaseEvent {
    type = EventType.DotNetTestsInClassRunStart;
    constructor(public className: string) {}
}

export class DotNetTestsInClassDebugStart implements BaseEvent {
    type = EventType.DotNetTestsInClassDebugStart;
    constructor(public className: string) {}
}

export class DotNetTestRunInContextStart implements BaseEvent {
    type = EventType.DotNetTestRunInContextStart;
    constructor(public fileName: string, public line: number, public column: number) {}
}

export class DotNetTestDebugInContextStart implements BaseEvent {
    type = EventType.DotNetTestDebugInContextStart;
    constructor(public fileName: string, public line: number, public column: number) {}
}

export class DocumentSynchronizationFailure implements BaseEvent {
    type = EventType.DocumentSynchronizationFailure;
    constructor(public documentPath: string, public errorMessage: string) {}
}

export class IntegrityCheckFailure {
    type = EventType.IntegrityCheckFailure;
    constructor(public packageDescription: string, public url: string, public retry: boolean) {}
}

export class IntegrityCheckSuccess {
    type = EventType.IntegrityCheckSuccess;
}

export class RazorPluginPathSpecified implements BaseEvent {
    type = EventType.RazorPluginPathSpecified;
    constructor(public path: string) {}
}

export class RazorPluginPathDoesNotExist implements BaseEvent {
    type = EventType.RazorPluginPathDoesNotExist;
    constructor(public path: string) {}
}

export class DebuggerPrerequisiteFailure extends EventWithMessage {
    type = EventType.DebuggerPrerequisiteFailure;
}
export class DebuggerPrerequisiteWarning extends EventWithMessage {
    type = EventType.DebuggerPrerequisiteWarning;
}
export class CommandDotNetRestoreProgress extends EventWithMessage {
    type = EventType.CommandDotNetRestoreProgress;
}
export class CommandDotNetRestoreSucceeded extends EventWithMessage {
    type = EventType.CommandDotNetRestoreSucceeded;
}
export class CommandDotNetRestoreFailed extends EventWithMessage {
    type = EventType.CommandDotNetRestoreFailed;
}
export class DownloadSuccess extends EventWithMessage {
    type = EventType.DownloadSuccess;
}
export class DownloadFailure extends EventWithMessage {
    type = EventType.DownloadFailure;
}
export class DotNetTestMessage extends EventWithMessage {
    type = EventType.DotNetTestMessage;
}
export class DotNetTestRunFailure extends EventWithMessage {
    type = EventType.DotNetTestRunFailure;
}
export class DotNetTestDebugWarning extends EventWithMessage {
    type = EventType.DotNetTestDebugWarning;
}
export class DotNetTestDebugStartFailure extends EventWithMessage {
    type = EventType.DotNetTestDebugStartFailure;
}

export class RazorDevModeActive implements BaseEvent {
    type = EventType.RazorDevModeActive;
}
export class ProjectModified implements BaseEvent {
    type = EventType.ProjectModified;
}
export class ActivationFailure implements BaseEvent {
    type = EventType.ActivationFailure;
}
export class ShowOmniSharpChannel implements BaseEvent {
    type = EventType.ShowOmniSharpChannel;
}
export class DebuggerNotInstalledFailure implements BaseEvent {
    type = EventType.DebuggerNotInstalledFailure;
}
export class CommandDotNetRestoreStart implements BaseEvent {
    type = EventType.CommandDotNetRestoreStart;
}
export class InstallationSuccess implements BaseEvent {
    type = EventType.InstallationSuccess;
}
export class DotNetTestDebugComplete implements BaseEvent {
    type = EventType.DotNetTestDebugComplete;
}
export class DownloadValidation implements BaseEvent {
    type = EventType.DownloadValidation;
}
export class ShowChannel implements BaseEvent {
    type = EventType.ShowChannel;
}