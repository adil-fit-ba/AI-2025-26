import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  MessageDto,
  ModelVersionDto,
  SystemStatusDto,
  SettingsDto,
  QueueStatsDto,
  SendMessageRequest,
  ReviewRequest,
  TrainRequest,
  SettingsRequest,
  SimulatorStatus,
  GoldProgress
} from '../models/api.models';

@Injectable({
  providedIn: 'root'
})
export class ApiService {
  private readonly baseUrl = 'http://localhost:5000/api';

  constructor(private http: HttpClient) {}

  // ════════════════════════════════════════════════════════════════════════════════
  //                     MESSAGES
  // ════════════════════════════════════════════════════════════════════════════════

  sendMessage(request: SendMessageRequest): Observable<MessageDto> {
    return this.http.post<MessageDto>(`${this.baseUrl}/messages`, request);
  }

  getMessage(id: number): Observable<MessageDto> {
    return this.http.get<MessageDto>(`${this.baseUrl}/messages/${id}`);
  }

  getRecentMessages(take: number = 50, status?: string): Observable<MessageDto[]> {
    let params = new HttpParams().set('take', take.toString());
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<MessageDto[]>(`${this.baseUrl}/messages/recent`, { params });
  }

  getQueuedMessages(take: number = 50): Observable<MessageDto[]> {
    const params = new HttpParams().set('take', take.toString());
    return this.http.get<MessageDto[]>(`${this.baseUrl}/messages/queued`, { params });
  }

  enqueueFromValidation(count: number = 10): Observable<{ enqueued: number }> {
    const params = new HttpParams().set('count', count.toString());
    return this.http.post<{ enqueued: number }>(`${this.baseUrl}/messages/enqueue`, null, { params });
  }

  getMessageStats(): Observable<QueueStatsDto> {
    return this.http.get<QueueStatsDto>(`${this.baseUrl}/messages/stats`);
  }

  // ════════════════════════════════════════════════════════════════════════════════
  //                     REVIEW
  // ════════════════════════════════════════════════════════════════════════════════

  getReviewQueue(take: number = 50): Observable<MessageDto[]> {
    const params = new HttpParams().set('take', take.toString());
    return this.http.get<MessageDto[]>(`${this.baseUrl}/review/queue`, { params });
  }

  getPendingCount(): Observable<{ pendingCount: number }> {
    return this.http.get<{ pendingCount: number }>(`${this.baseUrl}/review/count`);
  }

  addReview(messageId: number, request: ReviewRequest): Observable<{
    success: boolean;
    message: string;
    newStatus: string;
    goldProgress: GoldProgress;
  }> {
    return this.http.post<any>(`${this.baseUrl}/review/${messageId}`, request);
  }

  getReviewStats(): Observable<{
    totalGoldLabels: number;
    pendingReviewCount: number;
    goldProgress: GoldProgress;
  }> {
    return this.http.get<any>(`${this.baseUrl}/review/stats`);
  }

  // ════════════════════════════════════════════════════════════════════════════════
  //                     ADMIN
  // ════════════════════════════════════════════════════════════════════════════════

  getSystemStatus(): Observable<SystemStatusDto> {
    return this.http.get<SystemStatusDto>(`${this.baseUrl}/admin/status`);
  }

  importDataset(force: boolean = false): Observable<{ imported: number; skipped: number; message: string }> {
    const params = new HttpParams().set('force', force.toString());
    return this.http.post<any>(`${this.baseUrl}/admin/import`, null, { params });
  }

  trainModel(request: TrainRequest): Observable<ModelVersionDto> {
    return this.http.post<ModelVersionDto>(`${this.baseUrl}/admin/train`, request);
  }

  forceRetrain(template: string = 'Medium', activate: boolean = true): Observable<ModelVersionDto> {
    const params = new HttpParams()
      .set('template', template)
      .set('activate', activate.toString());
    return this.http.post<ModelVersionDto>(`${this.baseUrl}/admin/retrain`, null, { params });
  }

  getAllModels(): Observable<ModelVersionDto[]> {
    return this.http.get<ModelVersionDto[]>(`${this.baseUrl}/admin/models`);
  }

  activateModel(version: number): Observable<{ message: string; version: number }> {
    return this.http.post<any>(`${this.baseUrl}/admin/models/${version}/activate`, null);
  }

  getActiveModelStatus(): Observable<ModelVersionDto> {
    return this.http.get<ModelVersionDto>(`${this.baseUrl}/admin/model/status`);
  }

  getSettings(): Observable<SettingsDto> {
    return this.http.get<SettingsDto>(`${this.baseUrl}/admin/settings`);
  }

  updateSettings(request: SettingsRequest): Observable<SettingsDto> {
    return this.http.put<SettingsDto>(`${this.baseUrl}/admin/settings`, request);
  }

  setThresholds(thresholdAllow: number, thresholdBlock: number): Observable<any> {
    return this.http.put(`${this.baseUrl}/admin/thresholds`, { thresholdAllow, thresholdBlock });
  }

  setAutoRetrain(enabled: boolean): Observable<{ autoRetrainEnabled: boolean }> {
    return this.http.post<any>(`${this.baseUrl}/admin/auto-retrain/${enabled}`, null);
  }

  // ════════════════════════════════════════════════════════════════════════════════
  //                     SIMULATOR
  // ════════════════════════════════════════════════════════════════════════════════

  getSimulatorStatus(): Observable<SimulatorStatus> {
    return this.http.get<SimulatorStatus>(`${this.baseUrl}/admin/simulator`);
  }

  setSimulatorEnabled(enabled: boolean): Observable<{ enabled: boolean }> {
    return this.http.post<any>(`${this.baseUrl}/admin/simulator/${enabled}`, null);
  }
}
