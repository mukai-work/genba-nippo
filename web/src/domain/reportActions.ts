import type { ReportStatus } from '@/api/types'

export type ReportAction = 'save' | 'submit' | 'approve' | 'reject'

/**
 * 日報の状態ごとに、画面に出すボタンを決める。
 * 最終的な判断はサーバー（DailyReport クラス）がするが、押せないボタンを最初から出さないためにここでも持つ。
 */
export function availableActions(status: ReportStatus | null): ReportAction[] {
  switch (status) {
    case null: // 新規作成（保存と同時に提出もできる）
    case 'Draft':
    case 'Rejected':
      return ['save', 'submit']
    case 'Submitted':
      return ['approve', 'reject']
    case 'Approved':
      return []
  }
}

export function isEditable(status: ReportStatus | null): boolean {
  return status === null || status === 'Draft' || status === 'Rejected'
}
