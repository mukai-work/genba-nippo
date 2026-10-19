import { describe, expect, it } from 'vitest'
import { barGeometry, markerPct, monthTicks } from './timeline'

// 表示期間 10/1〜10/10 の10日間で考える（1日 = 10%）
const START = '2026-10-01'
const END = '2026-10-10'

describe('barGeometry', () => {
  it('開始日と終了日を両方含む幅にする', () => {
    expect(barGeometry(START, END, '2026-10-01', '2026-10-10')).toEqual({ leftPct: 0, widthPct: 100 })
    expect(barGeometry(START, END, '2026-10-03', '2026-10-04')).toEqual({ leftPct: 20, widthPct: 20 })
  })

  it('表示期間からはみ出した部分は切り落とす', () => {
    expect(barGeometry(START, END, '2026-09-25', '2026-10-02')).toEqual({ leftPct: 0, widthPct: 20 })
    expect(barGeometry(START, END, '2026-10-09', '2026-10-20')).toEqual({ leftPct: 80, widthPct: 20 })
  })
})

describe('markerPct', () => {
  it('今日の線はその日の中央に引く', () => {
    expect(markerPct(START, END, '2026-10-01')).toBeCloseTo(5)
    expect(markerPct(START, END, '2026-10-10')).toBeCloseTo(95)
  })

  it('表示期間の外なら線を引かない', () => {
    expect(markerPct(START, END, '2026-09-30')).toBeNull()
    expect(markerPct(START, END, '2026-10-11')).toBeNull()
  })
})

describe('monthTicks', () => {
  it('月の区切りを各月1日の位置に置く', () => {
    const ticks = monthTicks('2026-09-21', '2026-11-09') // 50日間
    expect(ticks.map((t) => t.label)).toEqual(['9月', '10月', '11月'])
    expect(ticks[0]!.leftPct).toBe(0)
    expect(ticks[1]!.leftPct).toBeCloseTo(20) // 10/1 は 10日目
    expect(ticks[2]!.leftPct).toBeCloseTo(82) // 11/1 は 41日目
  })

  it('次の区切りまでが狭い月は文字を出さない（重なり防止）', () => {
    // 8/29 開始・約半年の工期 → 8月の区切りは3日分しかない
    const ticks = monthTicks('2026-08-29', '2027-02-28')
    expect(ticks[0]).toMatchObject({ label: '8月', showLabel: false })
    expect(ticks.slice(1).every((t) => t.showLabel)).toBe(true)
  })

  it('年をまたいでも並ぶ', () => {
    expect(monthTicks('2026-12-15', '2027-02-01').map((t) => t.label)).toEqual(['12月', '1月', '2月'])
  })
})
