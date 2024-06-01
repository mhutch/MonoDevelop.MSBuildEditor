// https://raw.githubusercontent.com/dotnet/vscode-csharp/89da0f69901965627158f0120e1273ea2fc2486b/src/eventStream.ts

/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/
import { Subject, Subscription } from 'rxjs';
import { BaseEvent } from './omnisharp/loggingEvents';

export class EventStream {
    private sink: Subject<BaseEvent>;

    constructor() {
        this.sink = new Subject<BaseEvent>();
    }

    public post(event: BaseEvent) {
        this.sink.next(event);
    }

    public subscribe(eventHandler: (event: BaseEvent) => void): Subscription {
        return this.sink.subscribe(eventHandler);
    }
}