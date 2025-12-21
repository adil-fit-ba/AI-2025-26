import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, combineLatest, map } from 'rxjs';
import {
  MessageDto,
  MessageCard,
  ModelVersionDto,
  SystemStatusDto,
  QueueStatsDto,
  SettingsDto,
  MessageScoredEvent
} from '../models/api.models';

@Injectable({
  providedIn: 'root'
})
export class StateService {
  // Messages by status
  private queuedMessagesSubject = new BehaviorSubject<MessageCard[]>([]);
  private inboxMessagesSubject = new BehaviorSubject<MessageCard[]>([]);
  private spamMessagesSubject = new BehaviorSubject<MessageCard[]>([]);
  private pendingMessagesSubject = new BehaviorSubject<MessageCard[]>([]);

  // System state
  private systemStatusSubject = new BehaviorSubject<SystemStatusDto | null>(null);
  private activeModelSubject = new BehaviorSubject<ModelVersionDto | null>(null);
  private allModelsSubject = new BehaviorSubject<ModelVersionDto[]>([]);
  private settingsSubject = new BehaviorSubject<SettingsDto | null>(null);
  private queueStatsSubject = new BehaviorSubject<QueueStatsDto | null>(null);

  // UI state
  private loadingSubject = new BehaviorSubject<boolean>(false);
  private errorSubject = new BehaviorSubject<string | null>(null);

  // Public observables
  queuedMessages$ = this.queuedMessagesSubject.asObservable();
  inboxMessages$ = this.inboxMessagesSubject.asObservable();
  spamMessages$ = this.spamMessagesSubject.asObservable();
  pendingMessages$ = this.pendingMessagesSubject.asObservable();

  systemStatus$ = this.systemStatusSubject.asObservable();
  activeModel$ = this.activeModelSubject.asObservable();
  allModels$ = this.allModelsSubject.asObservable();
  settings$ = this.settingsSubject.asObservable();
  queueStats$ = this.queueStatsSubject.asObservable();

  loading$ = this.loadingSubject.asObservable();
  error$ = this.errorSubject.asObservable();

  // Derived observables
  totalProcessed$: Observable<number> = this.queueStats$.pipe(
    map(stats => stats?.totalProcessed ?? 0)
  );

  goldProgress$: Observable<{ current: number; threshold: number; percentage: number }> = this.settings$.pipe(
    map(settings => ({
      current: settings?.newGoldSinceLastTrain ?? 0,
      threshold: settings?.retrainGoldThreshold ?? 100,
      percentage: settings ? (settings.newGoldSinceLastTrain / settings.retrainGoldThreshold) * 100 : 0
    }))
  );

  // Maximum messages to keep in each column
  private readonly MAX_MESSAGES = 50;

  // ════════════════════════════════════════════════════════════════════════════════
  //                     MESSAGE OPERATIONS
  // ════════════════════════════════════════════════════════════════════════════════

  setQueuedMessages(messages: MessageDto[]): void {
    this.queuedMessagesSubject.next(messages.map(m => ({ ...m, isNew: false })));
  }

  setInboxMessages(messages: MessageDto[]): void {
    this.inboxMessagesSubject.next(messages.map(m => ({ ...m, isNew: false })));
  }

  setSpamMessages(messages: MessageDto[]): void {
    this.spamMessagesSubject.next(messages.map(m => ({ ...m, isNew: false })));
  }

  setPendingMessages(messages: MessageDto[]): void {
    this.pendingMessagesSubject.next(messages.map(m => ({ ...m, isNew: false })));
  }

  addQueuedMessage(message: MessageCard): void {
    const current = this.queuedMessagesSubject.value;
    const newMessage = { ...message, isNew: true, animationClass: 'animate-slide-in-left' };
    const updated = [newMessage, ...current].slice(0, this.MAX_MESSAGES);
    this.queuedMessagesSubject.next(updated);

    // Remove animation class after animation completes
    setTimeout(() => {
      this.updateMessageAnimation(message.id, '');
    }, 300);
  }

  handleMessageScored(event: MessageScoredEvent): void {
    // Remove from queued
    const queued = this.queuedMessagesSubject.value;
    this.queuedMessagesSubject.next(queued.filter(m => m.id !== event.messageId));

    // Create new message card
    const newCard: MessageCard = {
      id: event.messageId,
      text: event.textPreview,
      source: 'Runtime',
      status: event.newStatus as any,
      trueLabel: event.trueLabel,
      createdAtUtc: event.timestamp,
      lastPrediction: {
        pSpam: event.pSpam,
        decision: event.decision,
        modelVersion: 0,
        createdAtUtc: event.timestamp
      },
      isNew: true,
      animationClass: 'animate-slide-in-right'
    };

    // Add to appropriate column
    switch (event.newStatus) {
      case 'InInbox':
        this.addToColumn(this.inboxMessagesSubject, newCard);
        break;
      case 'InSpam':
        this.addToColumn(this.spamMessagesSubject, newCard);
        break;
      case 'PendingReview':
        this.addToColumn(this.pendingMessagesSubject, newCard);
        break;
    }

    // Update stats
    this.updateQueueStatsFromEvent(event.newStatus);
  }

  handleMessageMoved(messageId: number, oldStatus: string, newStatus: string): void {
    // Remove from old column
    this.removeFromColumn(oldStatus, messageId);

    // We'd need to fetch the full message to add to new column
    // For now, just update counts
    this.refreshStats();
  }

  removeFromPending(messageId: number): void {
    const pending = this.pendingMessagesSubject.value;
    this.pendingMessagesSubject.next(pending.filter(m => m.id !== messageId));
  }

  private addToColumn(subject: BehaviorSubject<MessageCard[]>, message: MessageCard): void {
    const current = subject.value;
    const updated = [message, ...current].slice(0, this.MAX_MESSAGES);
    subject.next(updated);

    // Remove animation class after animation completes
    setTimeout(() => {
      const messages = subject.value;
      subject.next(messages.map(m => 
        m.id === message.id ? { ...m, isNew: false, animationClass: '' } : m
      ));
    }, 300);
  }

  private removeFromColumn(status: string, messageId: number): void {
    switch (status) {
      case 'Queued':
        this.queuedMessagesSubject.next(
          this.queuedMessagesSubject.value.filter(m => m.id !== messageId)
        );
        break;
      case 'InInbox':
        this.inboxMessagesSubject.next(
          this.inboxMessagesSubject.value.filter(m => m.id !== messageId)
        );
        break;
      case 'InSpam':
        this.spamMessagesSubject.next(
          this.spamMessagesSubject.value.filter(m => m.id !== messageId)
        );
        break;
      case 'PendingReview':
        this.pendingMessagesSubject.next(
          this.pendingMessagesSubject.value.filter(m => m.id !== messageId)
        );
        break;
    }
  }

  private updateMessageAnimation(messageId: number, animationClass: string): void {
    // Update in all columns
    [this.queuedMessagesSubject, this.inboxMessagesSubject, 
     this.spamMessagesSubject, this.pendingMessagesSubject].forEach(subject => {
      const messages = subject.value;
      const index = messages.findIndex(m => m.id === messageId);
      if (index >= 0) {
        const updated = [...messages];
        updated[index] = { ...updated[index], animationClass, isNew: false };
        subject.next(updated);
      }
    });
  }

  // ════════════════════════════════════════════════════════════════════════════════
  //                     SYSTEM STATE OPERATIONS
  // ════════════════════════════════════════════════════════════════════════════════

  setSystemStatus(status: SystemStatusDto): void {
    this.systemStatusSubject.next(status);
    if (status.activeModel) {
      this.activeModelSubject.next(status.activeModel);
    }
    this.settingsSubject.next(status.settings);
    this.queueStatsSubject.next(status.queueStats);
  }

  setActiveModel(model: ModelVersionDto | null): void {
    this.activeModelSubject.next(model);
  }

  setAllModels(models: ModelVersionDto[]): void {
    this.allModelsSubject.next(models);
  }

  setSettings(settings: SettingsDto): void {
    this.settingsSubject.next(settings);
  }

  setQueueStats(stats: QueueStatsDto): void {
    this.queueStatsSubject.next(stats);
  }

  updateQueueStats(stats: Partial<QueueStatsDto>): void {
    const current = this.queueStatsSubject.value;
    if (current) {
      this.queueStatsSubject.next({ ...current, ...stats });
    }
  }

  private updateQueueStatsFromEvent(newStatus: string): void {
    const current = this.queueStatsSubject.value;
    if (!current) return;

    const updated = { ...current };
    updated.queued = Math.max(0, updated.queued - 1);

    switch (newStatus) {
      case 'InInbox':
        updated.inInbox++;
        break;
      case 'InSpam':
        updated.inSpam++;
        break;
      case 'PendingReview':
        updated.pendingReview++;
        break;
    }

    updated.totalProcessed = updated.inInbox + updated.inSpam + updated.pendingReview;
    this.queueStatsSubject.next(updated);
  }

  updateGoldProgress(newGold: number, threshold: number): void {
    const settings = this.settingsSubject.value;
    if (settings) {
      this.settingsSubject.next({
        ...settings,
        newGoldSinceLastTrain: newGold,
        retrainGoldThreshold: threshold
      });
    }
  }

  // ════════════════════════════════════════════════════════════════════════════════
  //                     UI STATE
  // ════════════════════════════════════════════════════════════════════════════════

  setLoading(loading: boolean): void {
    this.loadingSubject.next(loading);
  }

  setError(error: string | null): void {
    this.errorSubject.next(error);
  }

  clearError(): void {
    this.errorSubject.next(null);
  }

  // Placeholder for refresh - will be implemented by component
  private refreshStats: () => void = () => {};
  
  setRefreshCallback(callback: () => void): void {
    this.refreshStats = callback;
  }
}
