// 日付は API と同じ "yyyy-MM-dd" 文字列のまま扱う。
// Date に変換するとタイムゾーンで1日ずれる事故が起きやすいので、計算は年月日の数値で行う。

const pad = (n: number) => String(n).padStart(2, '0')

export function toIsoDate(date: Date): string {
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

/** "2026-10-18" → "10/18（日）" */
export function formatDateShort(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number) as [number, number, number]
  const weekday = '日月火水木金土'[new Date(y, m - 1, d).getDay()]
  return `${m}/${d}（${weekday}）`
}

/** "2026-10-18" → "2026/10/18" */
export function formatDate(iso: string): string {
  return iso.replaceAll('-', '/')
}

/** "2026-10" に月数を足す */
export function shiftMonth(month: string, delta: number): string {
  const [y, m] = month.split('-').map(Number) as [number, number]
  const total = y * 12 + (m - 1) + delta
  return `${Math.floor(total / 12)}-${pad((total % 12) + 1)}`
}

/** "2026-10" → "2026年10月" */
export function formatMonth(month: string): string {
  const [y, m] = month.split('-').map(Number) as [number, number]
  return `${y}年${m}月`
}

/** 2つの日付の間の日数（同じ日なら0） */
export function daysBetween(fromIso: string, toIso: string): number {
  const toUtc = (iso: string) => {
    const [y, m, d] = iso.split('-').map(Number) as [number, number, number]
    return Date.UTC(y, m - 1, d)
  }
  return Math.round((toUtc(toIso) - toUtc(fromIso)) / 86_400_000)
}
