import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { MessageDto, GoldProgress } from '../../models/api.models';

@Component({
  selector: 'app-review',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl:"review.component.html",
  styles: [`
    .line-clamp-2 {
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .bg-gray-750 {
      background-color: #374151;
    }
  `]
})
export class ReviewComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  pendingMessages: MessageDto[] = [];
  selectedMessage: MessageDto | null = null;
  reviewNote = '';
  loading = false;
  submitting = false;
  goldProgress: GoldProgress | null = null;

  toasts: Array<{ message: string; type: 'success' | 'error' | 'warning' }> = [];

  constructor(
    private apiService: ApiService,
    private signalRService: SignalRService
  ) {}

  ngOnInit(): void {
    this.loadPendingMessages();
    this.loadGoldStats();

    // Subscribe to SignalR events
    this.signalRService.messageScored$
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        // Refresh when new message enters pending
        this.loadPendingMessages();
      });

    this.signalRService.statsUpdated$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.goldProgress = {
          current: event.newGoldSinceLastTrain,
          threshold: event.retrainGoldThreshold,
          percentage: (event.newGoldSinceLastTrain / event.retrainGoldThreshold) * 100,
          willRetrain: event.newGoldSinceLastTrain >= event.retrainGoldThreshold
        };
      });

    this.signalRService.modelRetrained$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.showToast(`Model retrained to v${event.newVersion}!`, 'success');
        this.loadGoldStats();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  async loadPendingMessages(): Promise<void> {
    this.loading = true;
    try {
      const messages = await this.apiService.getReviewQueue(50).toPromise();
      this.pendingMessages = messages || [];
      
      // Clear selection if message no longer exists
      if (this.selectedMessage && !this.pendingMessages.find(m => m.id === this.selectedMessage?.id)) {
        this.selectedMessage = null;
      }
    } catch (error) {
      console.error('Error loading pending messages:', error);
      this.showToast('Error loading messages', 'error');
    } finally {
      this.loading = false;
    }
  }

  async loadGoldStats(): Promise<void> {
    try {
      const stats = await this.apiService.getReviewStats().toPromise();
      if (stats) {
        this.goldProgress = stats.goldProgress;
      }
    } catch (error) {
      console.error('Error loading gold stats:', error);
    }
  }

  selectMessage(message: MessageDto): void {
    this.selectedMessage = message;
    this.reviewNote = '';
  }

  async submitReview(label: 'ham' | 'spam'): Promise<void> {
    if (!this.selectedMessage) return;

    this.submitting = true;
    try {
      const result = await this.apiService.addReview(this.selectedMessage.id, {
        label,
        note: this.reviewNote || undefined,
        reviewedBy: 'moderator'
      }).toPromise();

      if (result?.success) {
        this.showToast(`Marked as ${label.toUpperCase()}`, 'success');
        
        // Update gold progress
        if (result.goldProgress) {
          this.goldProgress = result.goldProgress;
        }

        // Remove from list and clear selection
        this.pendingMessages = this.pendingMessages.filter(m => m.id !== this.selectedMessage?.id);
        this.selectedMessage = null;
        this.reviewNote = '';

        // Check if retrain will trigger
        if (result.goldProgress?.willRetrain) {
          this.showToast('Auto-retrain will trigger soon!', 'warning');
        }
      }
    } catch (error) {
      console.error('Error submitting review:', error);
      this.showToast('Error submitting review', 'error');
    } finally {
      this.submitting = false;
    }
  }

  getPSpamColor(pSpam: number): string {
    if (pSpam >= 0.7) return '#ef4444';
    if (pSpam >= 0.3) return '#f59e0b';
    return '#22c55e';
  }

  private showToast(message: string, type: 'success' | 'error' | 'warning'): void {
    const toast = { message, type };
    this.toasts.push(toast);
    setTimeout(() => {
      const index = this.toasts.indexOf(toast);
      if (index > -1) this.toasts.splice(index, 1);
    }, 3000);
  }
}
