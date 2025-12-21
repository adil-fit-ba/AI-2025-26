import { Injectable, OnDestroy } from '@angular/core';
import { Subject, BehaviorSubject } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import {
  MessageQueuedEvent,
  MessageScoredEvent,
  MessageMovedEvent,
  ModelRetrainedEvent,
  StatsUpdatedEvent
} from '../models/api.models';

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

@Injectable({
  providedIn: 'root'
})
export class SignalRService implements OnDestroy {
  private hubConnection: signalR.HubConnection | null = null;
  private readonly hubUrl = 'http://localhost:5000/hubs/spamAgent';

  // Connection status
  private connectionStatusSubject = new BehaviorSubject<ConnectionStatus>('disconnected');
  connectionStatus$ = this.connectionStatusSubject.asObservable();

  // Event subjects
  private messageQueuedSubject = new Subject<MessageQueuedEvent>();
  private messageScoredSubject = new Subject<MessageScoredEvent>();
  private messageMovedSubject = new Subject<MessageMovedEvent>();
  private modelRetrainedSubject = new Subject<ModelRetrainedEvent>();
  private statsUpdatedSubject = new Subject<StatsUpdatedEvent>();

  // Public observables
  messageQueued$ = this.messageQueuedSubject.asObservable();
  messageScored$ = this.messageScoredSubject.asObservable();
  messageMoved$ = this.messageMovedSubject.asObservable();
  modelRetrained$ = this.modelRetrainedSubject.asObservable();
  statsUpdated$ = this.statsUpdatedSubject.asObservable();

  constructor() {
    this.initConnection();
  }

  private initConnection(): void {
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0, 2, 4, 8, 16, 30 seconds
          const delays = [0, 2000, 4000, 8000, 16000, 30000];
          return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)];
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.registerEventHandlers();
    this.registerConnectionHandlers();
  }

  private registerEventHandlers(): void {
    if (!this.hubConnection) return;

    // Message events
    this.hubConnection.on('MessageQueued', (event: MessageQueuedEvent) => {
      console.log('[SignalR] MessageQueued:', event);
      this.messageQueuedSubject.next(event);
    });

    this.hubConnection.on('MessageScored', (event: MessageScoredEvent) => {
      console.log('[SignalR] MessageScored:', event);
      this.messageScoredSubject.next(event);
    });

    this.hubConnection.on('MessageMoved', (event: MessageMovedEvent) => {
      console.log('[SignalR] MessageMoved:', event);
      this.messageMovedSubject.next(event);
    });

    // Model events
    this.hubConnection.on('ModelRetrained', (event: ModelRetrainedEvent) => {
      console.log('[SignalR] ModelRetrained:', event);
      this.modelRetrainedSubject.next(event);
    });

    this.hubConnection.on('ModelActivated', (event: { version: number; timestamp: string }) => {
      console.log('[SignalR] ModelActivated:', event);
    });

    // Stats events
    this.hubConnection.on('StatsUpdated', (event: StatsUpdatedEvent) => {
      console.log('[SignalR] StatsUpdated:', event);
      this.statsUpdatedSubject.next(event);
    });

    // Group management responses
    this.hubConnection.on('Joined', (groupName: string) => {
      console.log('[SignalR] Joined group:', groupName);
    });

    this.hubConnection.on('Left', (groupName: string) => {
      console.log('[SignalR] Left group:', groupName);
    });

    this.hubConnection.on('Pong', (timestamp: string) => {
      console.log('[SignalR] Pong:', timestamp);
    });
  }

  private registerConnectionHandlers(): void {
    if (!this.hubConnection) return;

    this.hubConnection.onreconnecting((error) => {
      console.log('[SignalR] Reconnecting...', error);
      this.connectionStatusSubject.next('reconnecting');
    });

    this.hubConnection.onreconnected((connectionId) => {
      console.log('[SignalR] Reconnected:', connectionId);
      this.connectionStatusSubject.next('connected');
    });

    this.hubConnection.onclose((error) => {
      console.log('[SignalR] Connection closed:', error);
      this.connectionStatusSubject.next('disconnected');
    });
  }

  async connect(): Promise<void> {
    if (!this.hubConnection) {
      this.initConnection();
    }

    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.connectionStatusSubject.next('connecting');

    try {
      await this.hubConnection!.start();
      console.log('[SignalR] Connected successfully');
      this.connectionStatusSubject.next('connected');
    } catch (error) {
      console.error('[SignalR] Connection failed:', error);
      this.connectionStatusSubject.next('disconnected');
      // Retry after 5 seconds
      setTimeout(() => this.connect(), 5000);
    }
  }

  async disconnect(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.stop();
      this.connectionStatusSubject.next('disconnected');
    }
  }

  async joinGroup(groupName: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('JoinGroup', groupName);
    }
  }

  async leaveGroup(groupName: string): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('LeaveGroup', groupName);
    }
  }

  async ping(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      await this.hubConnection.invoke('Ping');
    }
  }

  get isConnected(): boolean {
    return this.hubConnection?.state === signalR.HubConnectionState.Connected;
  }

  ngOnDestroy(): void {
    this.disconnect();
  }
}
