// API（GenbaNippo.Api/Contracts/Dtos.cs）と同じ形。日付は "yyyy-MM-dd" の文字列で受け取る。

export type ReportStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected'
export type Weather = 'Sunny' | 'Cloudy' | 'Rainy' | 'Snowy'
export type ScheduleStatus = 'NotStarted' | 'OnTrack' | 'Behind' | 'Overdue' | 'Completed'

export interface Site {
  id: number
  name: string
  address: string | null
  startDate: string
  endDate: string
  pendingApprovalCount: number
  delayedWorkItemCount: number
}

export interface CreateSiteRequest {
  name: string
  address: string | null
  startDate: string
  endDate: string
}

export interface WorkItem {
  id: number
  siteId: number
  name: string
  plannedStart: string
  plannedEnd: string
  progressPercent: number
  lastReportedOn: string | null
  plannedPercent: number
  scheduleStatus: ScheduleStatus
}

export interface WorkItemRequest {
  name: string
  plannedStart: string
  plannedEnd: string
}

export interface ProgressEntryRequest {
  workItemId: number
  progressPercent: number
}

export interface ReportContent {
  weather: Weather
  workerCount: number
  workSummary: string
  progressEntries: ProgressEntryRequest[]
}

export interface DailyReportSummary {
  id: number
  reportDate: string
  weather: Weather
  workerCount: number
  status: ReportStatus
  workSummary: string
  version: number
}

export interface DailyReport {
  id: number
  siteId: number
  reportDate: string
  weather: Weather
  workerCount: number
  workSummary: string
  status: ReportStatus
  rejectionReason: string | null
  submittedAt: string | null
  approvedAt: string | null
  version: number
  progressEntries: { workItemId: number; workItemName: string; progressPercent: number }[]
}

/** RFC 9457 ProblemDetails（入力エラーのときは errors に項目ごとのメッセージが入る） */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}
