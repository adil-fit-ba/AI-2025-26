import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MessageCard } from '../../models/api.models';

@Component({
  selector: 'app-message-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div 
      class="message-card"
      [class.spam]="message.status === 'InSpam'"
      [class.ham]="message.status === 'InInbox'"
      [class.pending]="message.status === 'PendingReview'"
      [class.queued]="message.status === 'Queued'"
      [class]="message.animationClass || ''"
    >
      <!-- Header -->
      <div class="flex items-center justify-between mb-2">
        <span class="badge" [ngClass]="getBadgeClass()">
          {{ getStatusLabel() }}
        </span>
        <span class="text-xs text-gray-500 font-mono">
          #{{ message.id }}
        </span>
      </div>

      <!-- Text Preview -->
      <p class="text-sm text-gray-300 mb-3 line-clamp-2">
        {{ message.text }}
      </p>

      <!-- Prediction Info -->
      <div *ngIf="message.lastPrediction" class="space-y-2">
        <!-- pSpam Bar -->
        <div class="pspam-bar">
          <div 
            class="pspam-bar-fill"
            [style.width.%]="message.lastPrediction.pSpam * 100"
            [style.backgroundColor]="getPSpamColor(message.lastPrediction.pSpam)"
          ></div>
        </div>
        
        <!-- pSpam Value -->
        <div class="flex items-center justify-between text-xs">
          <span class="text-gray-400">pSpam</span>
          <span 
            class="font-mono font-semibold"
            [style.color]="getPSpamColor(message.lastPrediction.pSpam)"
          >
            {{ (message.lastPrediction.pSpam * 100).toFixed(1) }}%
          </span>
        </div>

        <!-- True Label Indicator -->
        <div *ngIf="message.trueLabel" class="flex items-center justify-between text-xs">
          <span class="text-gray-400">Ground Truth</span>
          <span 
            class="font-semibold"
            [class.text-green-400]="message.trueLabel === 'Ham'"
            [class.text-red-400]="message.trueLabel === 'Spam'"
          >
            {{ message.trueLabel }}
            <span *ngIf="isCorrect !== null" class="ml-1">
              {{ isCorrect ? '✓' : '✗' }}
            </span>
          </span>
        </div>
      </div>

      <!-- Queued indicator -->
      <div *ngIf="message.status === 'Queued'" class="mt-2">
        <div class="flex items-center gap-2 text-xs text-indigo-400">
          <span class="w-2 h-2 bg-indigo-400 rounded-full animate-pulse"></span>
          Waiting for processing...
        </div>
      </div>
    </div>
  `,
  styles: [`
    .line-clamp-2 {
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
  `]
})
export class MessageCardComponent {
  @Input() message!: MessageCard;

  getBadgeClass(): string {
    switch (this.message.status) {
      case 'InSpam': return 'badge-spam';
      case 'InInbox': return 'badge-ham';
      case 'PendingReview': return 'badge-pending';
      case 'Queued': return 'badge-queued';
      default: return 'badge-queued';
    }
  }

  getStatusLabel(): string {
    switch (this.message.status) {
      case 'InSpam': return 'SPAM';
      case 'InInbox': return 'HAM';
      case 'PendingReview': return 'REVIEW';
      case 'Queued': return 'QUEUED';
      default: return this.message.status;
    }
  }

  getPSpamColor(pSpam: number): string {
    if (pSpam >= 0.7) return '#ef4444'; // red
    if (pSpam >= 0.3) return '#f59e0b'; // yellow
    return '#22c55e'; // green
  }

  get isCorrect(): boolean | null {
    if (!this.message.trueLabel || !this.message.lastPrediction) return null;
    
    const decision = this.message.lastPrediction.decision;
    const trueLabel = this.message.trueLabel;
    
    if (decision === 'Block' && trueLabel === 'Spam') return true;
    if (decision === 'Allow' && trueLabel === 'Ham') return true;
    if (decision === 'Block' && trueLabel === 'Ham') return false;
    if (decision === 'Allow' && trueLabel === 'Spam') return false;
    
    return null; // PendingReview
  }
}
