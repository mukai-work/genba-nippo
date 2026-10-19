import { describe, expect, it } from 'vitest'
import { availableActions, isEditable } from './reportActions'

describe('availableActions', () => {
  it.each([
    [null, ['save', 'submit']],
    ['Draft', ['save', 'submit']],
    ['Rejected', ['save', 'submit']],
    ['Submitted', ['approve', 'reject']],
    ['Approved', []],
  ] as const)('状態 %s のときに出すボタン', (status, expected) => {
    expect(availableActions(status)).toEqual(expected)
  })

  it('編集できるのは新規・下書き・差戻しだけ', () => {
    expect(isEditable(null)).toBe(true)
    expect(isEditable('Draft')).toBe(true)
    expect(isEditable('Rejected')).toBe(true)
    expect(isEditable('Submitted')).toBe(false)
    expect(isEditable('Approved')).toBe(false)
  })
})
