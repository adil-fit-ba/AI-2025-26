import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ModelVersionDto, QueueStatsDto, SettingsDto } from '../../models/api.models';

@Component({
  selector: 'app-stats-panel',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="bg-gray-800/50 rounded-xl p-4 space-y-4">
      <!-- Active Model -->
      <div *ngIf="activeModel" class="stat-card">
        <div class="flex items-center justify-between mb-3">
          <h3 class="text-sm font-semibold text-gray-400 uppercase tracking-wide">Active Model</h3>
          <span class="badge badge-ham">v{{ activeModel.version }}</span>
        </div>
        
        <div class="space-y-2">
          <div class="metric-row">
            <span class="metric-label">Accuracy</span>
            <span class="metric-value text-blue-400">{{ (activeModel.metrics.accuracy * 100).toFixed(1) }}%</span>
          </div>
          <div class="metric-row">
            <span class="metric-label">Precision</span>
            <span class="metric-value text-green-400">{{ (activeModel.metrics.precision * 100).toFixed(1) }}%</span>
          </div>
          <div class="metric-row">
            <span class="metric-label">Recall</span>
            <span class="metric-value text-yellow-400">{{ (activeModel.metrics.recall * 100).toFixed(1) }}%</span>
          </div>
          <div class="metric-row">
            <span class="metric-label">F1 Score</span>
            <span class="metric-value text-purple-400">{{ (activeModel.metrics.f1 * 100).toFixed(1) }}%</span>
          </div>
        </div>

        <div class="mt-3 pt-3 border-t border-gray-700">
          <div class="text-xs text-gray-500">
            Template: {{ activeModel.trainTemplate }} • 
            Train: {{ activeModel.trainSetSize }} • 
            Gold: {{ activeModel.goldIncludedCount }}
          </div>
        </div>
      </div>

      <div *ngIf="!activeModel" class="stat-card text-center py-8">
        <div class="text-gray-500">
          <svg class="w-12 h-12 mx-auto mb-2 opacity-50" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" 
              d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
          </svg>
          <p>No active model</p>
          <p class="text-xs mt-1">Train a model in Admin</p>
        </div>
      </div>

      <!-- Queue Stats -->
      <div *ngIf="queueStats" class="stat-card">
        <h3 class="text-sm font-semibold text-gray-400 uppercase tracking-wide mb-3">Queue Stats</h3>
        
        <div class="grid grid-cols-2 gap-3">
          <div class="text-center p-2 bg-gray-700/50 rounded-lg">
            <div class="stat-value text-indigo-400">{{ queueStats.queued }}</div>
            <div class="stat-label">Queued</div>
          </div>
          <div class="text-center p-2 bg-gray-700/50 rounded-lg">
            <div class="stat-value text-green-400">{{ queueStats.inInbox }}</div>
            <div class="stat-label">Inbox</div>
          </div>
          <div class="text-center p-2 bg-gray-700/50 rounded-lg">
            <div class="stat-value text-red-400">{{ queueStats.inSpam }}</div>
            <div class="stat-label">Spam</div>
          </div>
          <div class="text-center p-2 bg-gray-700/50 rounded-lg">
            <div class="stat-value text-yellow-400">{{ queueStats.pendingReview }}</div>
            <div class="stat-label">Review</div>
          </div>
        </div>

        <div class="mt-3 pt-3 border-t border-gray-700 text-center">
          <div class="stat-value text-white">{{ queueStats.totalProcessed }}</div>
          <div class="stat-label">Total Processed</div>
        </div>
      </div>

      <!-- Gold Progress -->
      <div *ngIf="settings" class="stat-card">
        <h3 class="text-sm font-semibold text-gray-400 uppercase tracking-wide mb-3">Gold Labels</h3>
        
        <div class="flex items-center justify-between mb-2">
          <span class="text-2xl font-bold text-yellow-400">{{ settings.newGoldSinceLastTrain }}</span>
          <span class="text-gray-500">/ {{ settings.retrainGoldThreshold }}</span>
        </div>

        <!-- Progress bar -->
        <div class="h-3 bg-gray-700 rounded-full overflow-hidden">
          <div 
            class="h-full bg-gradient-to-r from-yellow-600 to-yellow-400 rounded-full transition-all duration-500"
            [style.width.%]="goldPercentage"
          ></div>
        </div>

        <div class="mt-2 flex items-center justify-between text-xs">
          <span class="text-gray-500">Progress to auto-retrain</span>
          <span class="font-mono" [class.text-yellow-400]="goldPercentage >= 80" [class.text-gray-400]="goldPercentage < 80">
            {{ goldPercentage.toFixed(0) }}%
          </span>
        </div>

        <div class="mt-2 flex items-center gap-2 text-xs">
          <span 
            class="w-2 h-2 rounded-full"
            [class.bg-green-400]="settings.autoRetrainEnabled"
            [class.bg-gray-500]="!settings.autoRetrainEnabled"
          ></span>
          <span [class.text-green-400]="settings.autoRetrainEnabled" [class.text-gray-500]="!settings.autoRetrainEnabled">
            Auto-retrain {{ settings.autoRetrainEnabled ? 'enabled' : 'disabled' }}
          </span>
        </div>
      </div>

      <!-- Thresholds -->
      <div *ngIf="settings" class="stat-card">
        <h3 class="text-sm font-semibold text-gray-400 uppercase tracking-wide mb-3">Decision Thresholds</h3>
        
        <div class="relative h-8 bg-gradient-to-r from-green-500/20 via-yellow-500/20 to-red-500/20 rounded-lg overflow-hidden">
          <!-- Allow threshold marker -->
          <div 
            class="absolute top-0 bottom-0 w-0.5 bg-green-400"
            [style.left.%]="settings.thresholdAllow * 100"
          ></div>
          <!-- Block threshold marker -->
          <div 
            class="absolute top-0 bottom-0 w-0.5 bg-red-400"
            [style.left.%]="settings.thresholdBlock * 100"
          ></div>
          
          <!-- Labels -->
          <div class="absolute inset-0 flex items-center justify-between px-2 text-xs font-medium">
            <span class="text-green-400">HAM</span>
            <span class="text-yellow-400">REVIEW</span>
            <span class="text-red-400">SPAM</span>
          </div>
        </div>

        <div class="mt-2 flex justify-between text-xs text-gray-500">
          <span>0</span>
          <span>T_ALLOW: {{ settings.thresholdAllow }}</span>
          <span>T_BLOCK: {{ settings.thresholdBlock }}</span>
          <span>1</span>
        </div>
      </div>
    </div>
  `
})
export class StatsPanelComponent {
  @Input() activeModel: ModelVersionDto | null = null;
  @Input() queueStats: QueueStatsDto | null = null;
  @Input() settings: SettingsDto | null = null;

  get goldPercentage(): number {
    if (!this.settings) return 0;
    return (this.settings.newGoldSinceLastTrain / this.settings.retrainGoldThreshold) * 100;
  }
}
