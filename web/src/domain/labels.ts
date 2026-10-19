import type { ReportStatus, ScheduleStatus, Weather } from '@/api/types'

/** 画面の色分け。CSS の .tone-* と対応する */
export type Tone = 'neutral' | 'info' | 'success' | 'warning' | 'danger'

export const reportStatusLabel: Record<ReportStatus, string> = {
  Draft: '下書き',
  Submitted: '承認待ち',
  Approved: '承認済み',
  Rejected: '差戻し',
}

export const reportStatusTone: Record<ReportStatus, Tone> = {
  Draft: 'neutral',
  Submitted: 'info',
  Approved: 'success',
  Rejected: 'warning', // 差戻しは「対応が必要」。赤はエラーと期限超過だけに使う
}

export const weatherLabel: Record<Weather, string> = {
  Sunny: '晴れ',
  Cloudy: '曇り',
  Rainy: '雨',
  Snowy: '雪',
}

export const weatherOptions = Object.entries(weatherLabel).map(([value, label]) => ({
  value: value as Weather,
  label,
}))

export const scheduleStatusLabel: Record<ScheduleStatus, string> = {
  NotStarted: '未着手',
  OnTrack: '予定どおり',
  Behind: '遅れ',
  Overdue: '期限超過',
  Completed: '完了',
}

export const scheduleStatusTone: Record<ScheduleStatus, Tone> = {
  NotStarted: 'neutral',
  OnTrack: 'info',
  Behind: 'warning',
  Overdue: 'danger',
  Completed: 'success',
}
