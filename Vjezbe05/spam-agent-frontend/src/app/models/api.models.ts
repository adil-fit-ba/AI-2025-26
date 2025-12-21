// ════════════════════════════════════════════════════════════════════════════════
//                     API RESPONSE MODELS
// ════════════════════════════════════════════════════════════════════════════════

export interface MessageDto {
  id: number;
  text: string;
  source: string;
  status: MessageStatus;
  trueLabel?: string;
  createdAtUtc: string;
  lastPrediction?: PredictionDto;
}

export interface PredictionDto {
  pSpam: number;
  decision: string;
  modelVersion: number;
  createdAtUtc: string;
}

export interface ModelVersionDto {
  id: number;
  version: number;
  trainTemplate: string;
  trainSetSize: number;
  goldIncludedCount: number;
  validationSetSize: number;
  metrics: MetricsDto;
  thresholdAllow: number;
  thresholdBlock: number;
  isActive: boolean;
  createdAtUtc: string;
}

export interface MetricsDto {
  accuracy: number;
  precision: number;
  recall: number;
  f1: number;
}

export interface SystemStatusDto {
  activeModel?: ModelVersionDto;
  settings: SettingsDto;
  queueStats: QueueStatsDto;
  datasetStats: DatasetStatsDto;
}

export interface SettingsDto {
  thresholdAllow: number;
  thresholdBlock: number;
  retrainGoldThreshold: number;
  newGoldSinceLastTrain: number;
  autoRetrainEnabled: boolean;
  lastRetrainAtUtc?: string;
}

export interface QueueStatsDto {
  queued: number;
  inInbox: number;
  inSpam: number;
  pendingReview: number;
  totalProcessed: number;
}

export interface DatasetStatsDto {
  totalMessages: number;
  uciMessages: number;
  runtimeMessages: number;
  trainPoolCount: number;
  validationCount: number;
  hamCount: number;
  spamCount: number;
  totalGoldLabels: number;
}

// ════════════════════════════════════════════════════════════════════════════════
//                     REQUEST MODELS
// ════════════════════════════════════════════════════════════════════════════════

export interface SendMessageRequest {
  text: string;
  source?: string;
}

export interface ReviewRequest {
  label: 'ham' | 'spam';
  note?: string;
  reviewedBy?: string;
}

export interface TrainRequest {
  template: 'Light' | 'Medium' | 'Full';
  activate: boolean;
}

export interface SettingsRequest {
  thresholdAllow?: number;
  thresholdBlock?: number;
  retrainGoldThreshold?: number;
  autoRetrainEnabled?: boolean;
  simulatorEnabled?: boolean;
  simulatorIntervalMs?: number;
  simulatorBatchSize?: number;
}

// ════════════════════════════════════════════════════════════════════════════════
//                     SIGNALR EVENT MODELS
// ════════════════════════════════════════════════════════════════════════════════

export interface MessageQueuedEvent {
  messageId: number;
  text: string;
  timestamp: string;
}

export interface MessageScoredEvent {
  messageId: number;
  textPreview: string;
  pSpam: number;
  decision: string;
  newStatus: string;
  trueLabel?: string;
  isCorrect?: boolean;
  timestamp: string;
}

export interface MessageMovedEvent {
  messageId: number;
  oldStatus: string;
  newStatus: string;
  label?: string;
  timestamp: string;
}

export interface ModelRetrainedEvent {
  newVersion: number;
  template: string;
  metrics: MetricsDto;
  isActivated: boolean;
  timestamp: string;
}

export interface StatsUpdatedEvent {
  queueStats: QueueStatsDto;
  newGoldSinceLastTrain: number;
  retrainGoldThreshold: number;
  timestamp: string;
}

// ════════════════════════════════════════════════════════════════════════════════
//                     ENUMS & TYPES
// ════════════════════════════════════════════════════════════════════════════════

export type MessageStatus = 'Queued' | 'InInbox' | 'InSpam' | 'PendingReview' | 'Archived';
export type SpamDecision = 'Allow' | 'PendingReview' | 'Block';
export type TrainTemplate = 'Light' | 'Medium' | 'Full';

// ════════════════════════════════════════════════════════════════════════════════
//                     UI MODELS
// ════════════════════════════════════════════════════════════════════════════════

export interface MessageCard extends MessageDto {
  isNew?: boolean;
  animationClass?: string;
}

export interface GoldProgress {
  current: number;
  threshold: number;
  percentage: number;
  willRetrain: boolean;
}

export interface SimulatorStatus {
  available: boolean;
  enabled: boolean;
  intervalMs: number;
  batchSize: number;
}
