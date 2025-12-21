import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { SignalRService, ConnectionStatus } from '../../services/signalr.service';
import { StateService } from '../../services/state.service';
import { MessageCardComponent } from '../../components/message-card/message-card.component';
import { StatsPanelComponent } from '../../components/stats-panel/stats-panel.component';
import { MessageCard, MessageScoredEvent } from '../../models/api.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, MessageCardComponent, StatsPanelComponent],
  templateUrl: "dashboard.component.html",
})
export class DashboardComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  connectionStatus: ConnectionStatus = 'disconnected';
  loading = false;
  enqueueing = false;

  // Observables from state service
  queuedMessages$ = this.stateService.queuedMessages$;
  inboxMessages$ = this.stateService.inboxMessages$;
  spamMessages$ = this.stateService.spamMessages$;
  pendingMessages$ = this.stateService.pendingMessages$;
  activeModel$ = this.stateService.activeModel$;
  queueStats$ = this.stateService.queueStats$;
  settings$ = this.stateService.settings$;

  toasts: Array<{ message: string; type: 'success' | 'error' | 'info' | 'warning' }> = [];

  constructor(
    private apiService: ApiService,
    private signalRService: SignalRService,
    private stateService: StateService
  ) {}

  ngOnInit(): void {
    // Set refresh callback
    this.stateService.setRefreshCallback(() => this.loadStats());

    // Connect to SignalR
    this.signalRService.connect();

    // Subscribe to connection status
    this.signalRService.connectionStatus$
      .pipe(takeUntil(this.destroy$))
      .subscribe(status => {
        this.connectionStatus = status;
        if (status === 'connected') {
          this.showToast('Connected to server', 'success');
        } else if (status === 'disconnected') {
          this.showToast('Disconnected from server', 'error');
        }
      });

    // Subscribe to SignalR events
    this.subscribeToEvents();

    // Initial data load
    this.refreshAll();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    this.signalRService.disconnect();
  }

  private subscribeToEvents(): void {
    // Message queued
    this.signalRService.messageQueued$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        const card: MessageCard = {
          id: event.messageId,
          text: event.text,
          source: 'Runtime',
          status: 'Queued',
          createdAtUtc: event.timestamp,
          isNew: true
        };
        this.stateService.addQueuedMessage(card);
      });

    // Message scored
    this.signalRService.messageScored$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.stateService.handleMessageScored(event);
      });

    // Message moved (review)
    this.signalRService.messageMoved$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.stateService.handleMessageMoved(event.messageId, event.oldStatus, event.newStatus);
      });

    // Model retrained
    this.signalRService.modelRetrained$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.showToast(`New model v${event.newVersion} trained! Accuracy: ${(event.metrics.accuracy * 100).toFixed(1)}%`, 'success');
        this.loadSystemStatus();
      });

    // Stats updated
    this.signalRService.statsUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.stateService.setQueueStats(event.queueStats);
        this.stateService.updateGoldProgress(event.newGoldSinceLastTrain, event.retrainGoldThreshold);
      });
  }

  async refreshAll(): Promise<void> {
    this.loading = true;
    try {
      await Promise.all([
        this.loadSystemStatus(),
        this.loadMessages()
      ]);
    } catch (error) {
      console.error('Error refreshing data:', error);
      this.showToast('Error loading data', 'error');
    } finally {
      this.loading = false;
    }
  }

  private async loadSystemStatus(): Promise<void> {
    try {
      const status = await this.apiService.getSystemStatus().toPromise();
      if (status) {
        this.stateService.setSystemStatus(status);
      }
    } catch (error) {
      console.error('Error loading system status:', error);
    }
  }

  private async loadMessages(): Promise<void> {
    try {
      const [queued, recent] = await Promise.all([
        this.apiService.getQueuedMessages(50).toPromise(),
        this.apiService.getRecentMessages(100).toPromise()
      ]);

      if (queued) {
        this.stateService.setQueuedMessages(queued);
      }

      if (recent) {
        const inbox = recent.filter(m => m.status === 'InInbox');
        const spam = recent.filter(m => m.status === 'InSpam');
        const pending = recent.filter(m => m.status === 'PendingReview');

        this.stateService.setInboxMessages(inbox);
        this.stateService.setSpamMessages(spam);
        this.stateService.setPendingMessages(pending);
      }
    } catch (error) {
      console.error('Error loading messages:', error);
    }
  }

  private async loadStats(): Promise<void> {
    try {
      const stats = await this.apiService.getMessageStats().toPromise();
      if (stats) {
        this.stateService.setQueueStats(stats);
      }
    } catch (error) {
      console.error('Error loading stats:', error);
    }
  }

  async enqueueMessages(): Promise<void> {
    this.enqueueing = true;
    try {
      const result = await this.apiService.enqueueFromValidation(5).toPromise();
      if (result) {
        this.showToast(`Added ${result.enqueued} messages to queue`, 'info');
      }
    } catch (error) {
      console.error('Error enqueueing messages:', error);
      this.showToast('Error adding messages', 'error');
    } finally {
      this.enqueueing = false;
    }
  }

  trackByMessageId(index: number, message: MessageCard): number {
    return message.id;
  }

  private showToast(message: string, type: 'success' | 'error' | 'info' | 'warning'): void {
    const toast = { message, type };
    this.toasts.push(toast);
    
    // Remove toast after 3 seconds
    setTimeout(() => {
      const index = this.toasts.indexOf(toast);
      if (index > -1) {
        this.toasts.splice(index, 1);
      }
    }, 3000);
  }
}
