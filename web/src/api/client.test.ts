import { describe, expect, it, vi } from 'vitest'
import { ApiError, createApiClient, ProblemType, toFieldName } from './client'

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
  })
}

describe('createApiClient', () => {
  it('更新時は画面が持っている版番号を本文に入れて送る', async () => {
    const fetcher = vi.fn().mockResolvedValue(jsonResponse(200, { id: 7, version: 3 }))
    const api = createApiClient(fetcher)

    await api.updateReport(7, 2, { weather: 'Sunny', workerCount: 5, workSummary: '配筋', progressEntries: [] })

    const [url, init] = fetcher.mock.calls[0]!
    expect(url).toBe('/api/reports/7')
    expect(init.method).toBe('PUT')
    expect(JSON.parse(init.body)).toMatchObject({ version: 2, workerCount: 5 })
  })

  it('同時編集の衝突は type で見分け、サーバーのメッセージをそのまま使う', async () => {
    const detail = '他の人が先にこのデータ（日報または工程）を更新しました。画面を再読み込みしてから、もう一度操作してください。'
    const api = createApiClient(vi.fn().mockResolvedValue(
      jsonResponse(409, { status: 409, type: ProblemType.concurrencyConflict, title: '他の人が先に更新しました', detail }),
    ))

    const error = await api.submitReport(1, 1).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).isConcurrencyConflict).toBe(true)
    expect((error as ApiError).message).toBe(detail)
  })

  it('同じ 409 でも状態違い（承認済みの編集など）は再読み込みの対象にしない', async () => {
    const api = createApiClient(vi.fn().mockResolvedValue(
      jsonResponse(409, { status: 409, type: ProblemType.invalidState, detail: '承認済みの日報は編集できません。' }),
    ))

    const error = (await api.submitReport(1, 1).catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(409)
    expect(error.isConcurrencyConflict).toBe(false)
  })

  it('入力エラーは項目名を画面側の名前（先頭小文字）にそろえる', async () => {
    const api = createApiClient(vi.fn().mockResolvedValue(jsonResponse(400, {
      title: '入力内容に誤りがあります',
      errors: { Name: ['現場名を入力してください。'], EndDate: ['工期の終了日を入力してください。'] },
    })))

    const error = (await api.createSite({ name: '', address: null, startDate: '', endDate: '' }).catch((e: unknown) => e)) as ApiError

    expect(error.fieldErrors).toEqual({
      name: ['現場名を入力してください。'],
      endDate: ['工期の終了日を入力してください。'],
    })
    expect(error.message).toContain('現場名を入力してください。')
  })

  it('サーバーに届かないときは接続できない旨を伝える', async () => {
    const api = createApiClient(vi.fn().mockRejectedValue(new TypeError('Failed to fetch')))

    const error = (await api.listSites().catch((e: unknown) => e)) as ApiError

    expect(error.status).toBe(0)
    expect(error.message).toContain('サーバーに接続できません')
  })

  it('本文のない 500 は汎用メッセージにする', async () => {
    const api = createApiClient(vi.fn().mockResolvedValue(new Response('', { status: 500 })))

    const error = (await api.listSites().catch((e: unknown) => e)) as ApiError

    expect(error.message).toContain('サーバーでエラーが起きました')
  })
})

describe('toFieldName', () => {
  it('C# のプロパティ名と JSON パスを画面の項目名にそろえる', () => {
    expect(toFieldName('EndDate')).toBe('endDate')
    expect(toFieldName('$.startDate')).toBe('startDate')
  })
})
