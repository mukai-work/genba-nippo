import { describe, expect, it } from 'vitest'
import { daysBetween, formatDateShort, formatDateTime, formatMonth, shiftMonth, toIsoDate } from './dates'

describe('dates', () => {
  it('月をまたいで前後の月を計算できる', () => {
    expect(shiftMonth('2026-10', 1)).toBe('2026-11')
    expect(shiftMonth('2026-12', 1)).toBe('2027-01')
    expect(shiftMonth('2026-01', -1)).toBe('2025-12')
    expect(shiftMonth('2026-10', -14)).toBe('2025-08')
  })

  it('日付の差を日数で返す（夏時間などの影響を受けない）', () => {
    expect(daysBetween('2026-10-01', '2026-10-01')).toBe(0)
    expect(daysBetween('2026-10-01', '2026-10-10')).toBe(9)
    expect(daysBetween('2026-12-31', '2027-01-01')).toBe(1)
    expect(daysBetween('2026-10-10', '2026-10-01')).toBe(-9)
  })

  it('曜日つきの短い表記にする', () => {
    expect(formatDateShort('2026-10-18')).toBe('10/18（日）')
    expect(formatMonth('2026-10')).toBe('2026年10月')
  })

  it('ローカル日付を yyyy-MM-dd にする', () => {
    expect(toIsoDate(new Date(2026, 0, 5))).toBe('2026-01-05')
  })
})

describe('formatDateTime', () => {
  it('秒を出さず日本時間で表示する', () => {
    expect(formatDateTime('2026-10-18T17:32:05+09:00')).toBe('2026/10/18 17:32')
    expect(formatDateTime('2026-10-18T08:32:05Z')).toBe('2026/10/18 17:32')
  })
})
