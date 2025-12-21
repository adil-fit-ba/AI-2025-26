import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { ApiService } from '../../services/api.service';
import { SignalRService } from '../../services/signalr.service';
import { 
  SystemStatusDto, 
  ModelVersionDto, 
  SettingsDto,
  SimulatorStatus,
  TrainTemplate 
} from '../../models/api.models';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: "admin.component.html",
  styles: [`
    .bg-gray-750 { background-color: #374151; }
  `]
})
export class AdminComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  systemStatus: SystemStatusDto | null = null;
  models: ModelVersionDto[] = [];
  settings: SettingsDto | null = null;
  simulator: SimulatorStatus | null = null;

  loading = false;
  training = false;
  importing = false;
  enqueueing = false;
  savingSettings = false;

  selectedTemplate: TrainTemplate = 'Medium';
  autoActivate = true;

  templates = [
    { value: 'Light' as TrainTemplate, label: 'Light', samples: 500 },
    { value: 'Medium' as TrainTemplate, label: 'Medium', samples: 2000 },
    { value: 'Full' as TrainTemplate, label: 'Full', samples: 4000 }
  ];

  toasts: Array<{ message: string; type: 'success' | 'error' | 'info' }> = [];

  constructor(
    private apiService: ApiService,
    private signalRService: SignalRService
  ) {}

  ngOnInit(): void {
    this.refreshAll();

    // Subscribe to model retrained events
    this.signalRService.modelRetrained$
      .pipe(takeUntil(this.destroy$))
      .subscribe(event => {
        this.showToast(`Model v${event.newVersion} trained! Accuracy: ${(event.metrics.accuracy * 100).toFixed(1)}%`, 'success');
        this.loadModels();
        this.loadSystemStatus();
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  async refreshAll(): Promise<void> {
    this.loading = true;
    try {
      await Promise.all([
        this.loadSystemStatus(),
        this.loadModels(),
        this.loadSimulatorStatus()
      ]);
    } finally {
      this.loading = false;
    }
  }

  private async loadSystemStatus(): Promise<void> {
    try {
      const status = await this.apiService.getSystemStatus().toPromise();
      this.systemStatus = status || null;
      this.settings = status?.settings || null;
    } catch (error) {
      console.error('Error loading system status:', error);
    }
  }

  private async loadModels(): Promise<void> {
    try {
      const models = await this.apiService.getAllModels().toPromise();
      this.models = models || [];
    } catch (error) {
      console.error('Error loading models:', error);
    }
  }

  private async loadSimulatorStatus(): Promise<void> {
    try {
      const status = await this.apiService.getSimulatorStatus().toPromise();
      this.simulator = status || null;
    } catch (error) {
      console.error('Error loading simulator status:', error);
    }
  }

  async importDataset(): Promise<void> {
    this.importing = true;
    try {
      const result = await this.apiService.importDataset().toPromise();
      if (result) {
        this.showToast(result.message, 'success');
        this.loadSystemStatus();
      }
    } catch (error) {
      console.error('Error importing dataset:', error);
      this.showToast('Error importing dataset', 'error');
    } finally {
      this.importing = false;
    }
  }

  async enqueueMessages(): Promise<void> {
    this.enqueueing = true;
    try {
      const result = await this.apiService.enqueueFromValidation(10).toPromise();
      if (result) {
        this.showToast(`Added ${result.enqueued} messages`, 'info');
      }
    } catch (error) {
      console.error('Error enqueueing:', error);
      this.showToast('Error adding messages', 'error');
    } finally {
      this.enqueueing = false;
    }
  }

  async toggleSimulator(): Promise<void> {
    if (!this.simulator?.available) return;
    
    try {
      const result = await this.apiService.setSimulatorEnabled(!this.simulator.enabled).toPromise();
      if (result) {
        this.simulator.enabled = result.enabled;
        this.showToast(`Simulator ${result.enabled ? 'started' : 'stopped'}`, 'info');
      }
    } catch (error) {
      console.error('Error toggling simulator:', error);
      this.showToast('Error toggling simulator', 'error');
    }
  }

  async trainModel(): Promise<void> {
    this.training = true;
    try {
      const model = await this.apiService.trainModel({
        template: this.selectedTemplate,
        activate: this.autoActivate
      }).toPromise();
      
      if (model) {
        this.showToast(`Model v${model.version} trained! Accuracy: ${(model.metrics.accuracy * 100).toFixed(1)}%`, 'success');
        this.loadModels();
        this.loadSystemStatus();
      }
    } catch (error) {
      console.error('Error training model:', error);
      this.showToast('Error training model', 'error');
    } finally {
      this.training = false;
    }
  }

  async forceRetrain(): Promise<void> {
    this.training = true;
    try {
      const model = await this.apiService.forceRetrain('Medium', true).toPromise();
      if (model) {
        this.showToast(`Force retrained to v${model.version}`, 'success');
        this.loadModels();
        this.loadSystemStatus();
      }
    } catch (error) {
      console.error('Error force retraining:', error);
      this.showToast('Error force retraining', 'error');
    } finally {
      this.training = false;
    }
  }

  async activateModel(version: number): Promise<void> {
    try {
      await this.apiService.activateModel(version).toPromise();
      this.showToast(`Model v${version} activated`, 'success');
      this.loadModels();
    } catch (error) {
      console.error('Error activating model:', error);
      this.showToast('Error activating model', 'error');
    }
  }

  async saveSettings(): Promise<void> {
    if (!this.settings) return;
    
    this.savingSettings = true;
    try {
      const updated = await this.apiService.updateSettings({
        thresholdAllow: this.settings.thresholdAllow,
        thresholdBlock: this.settings.thresholdBlock,
        retrainGoldThreshold: this.settings.retrainGoldThreshold,
        autoRetrainEnabled: this.settings.autoRetrainEnabled
      }).toPromise();
      
      if (updated) {
        this.settings = updated;
        this.showToast('Settings saved', 'success');
      }
    } catch (error) {
      console.error('Error saving settings:', error);
      this.showToast('Error saving settings', 'error');
    } finally {
      this.savingSettings = false;
    }
  }

  private showToast(message: string, type: 'success' | 'error' | 'info'): void {
    const toast = { message, type };
    this.toasts.push(toast);
    setTimeout(() => {
      const index = this.toasts.indexOf(toast);
      if (index > -1) this.toasts.splice(index, 1);
    }, 3000);
  }
}
