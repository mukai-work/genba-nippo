import { daysBetween, shiftMonth } from './dates'

// 工程表（ガントチャート風）の座標計算。表示期間（現場の工期）に対する位置を % で返す。
// 日付は両端を含む（10/1〜10/10 は 10日分の幅）。

export interface BarGeometry {
  leftPct: number
  widthPct: number
}

const clamp = (v: number, min: number, max: number) => Math.min(Math.max(v, min), max)

export function barGeometry(rangeStart: string, rangeEnd: string, start: string, end: string): BarGeometry {
  const total = daysBetween(rangeStart, rangeEnd) + 1
  const left = clamp(daysBetween(rangeStart, start), 0, total)
  const right = clamp(daysBetween(rangeStart, end) + 1, 0, total)
  return {
    leftPct: (left / total) * 100,
    widthPct: (Math.max(right - left, 0) / total) * 100,
  }
}

/** 今日の縦線の位置（その日の中央）。表示期間の外なら null */
export function markerPct(rangeStart: string, rangeEnd: string, day: string): number | null {
  const total = daysBetween(rangeStart, rangeEnd) + 1
  const offset = daysBetween(rangeStart, day)
  if (offset < 0 || offset >= total) return null
  return ((offset + 0.5) / total) * 100
}

export interface MonthTick {
  label: string
  leftPct: number
  /** 次の区切りまでが狭く、文字を出すと重なるとき false（線だけ引く） */
  showLabel: boolean
}

/** 文字を出すのに必要な区切りの間隔（表示期間に対する %） */
const MIN_LABEL_GAP_PCT = 8

/** 月の区切り（各月1日の位置）。表示期間の開始月は開始日の位置に置く */
export function monthTicks(rangeStart: string, rangeEnd: string): MonthTick[] {
  const total = daysBetween(rangeStart, rangeEnd) + 1
  const ticks: MonthTick[] = []
  let month = rangeStart.slice(0, 7)
  const lastMonth = rangeEnd.slice(0, 7)
  while (month <= lastMonth) {
    const first = month === rangeStart.slice(0, 7) ? rangeStart : `${month}-01`
    ticks.push({
      label: `${Number(month.slice(5))}月`,
      leftPct: (daysBetween(rangeStart, first) / total) * 100,
      showLabel: true,
    })
    month = shiftMonth(month, 1)
  }
  ticks.forEach((tick, i) => {
    const nextLeft = ticks[i + 1]?.leftPct ?? 100
    tick.showLabel = nextLeft - tick.leftPct >= MIN_LABEL_GAP_PCT
  })
  return ticks
}
