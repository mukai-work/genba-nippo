import { describe, expect, it, vi } from 'vitest'
import { ApiError, createApiClient } from './client'

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

  it('409 は ApiError(isConflict) になり、サーバーのメッセージをそのまま使う', async () => {
    const detail = '他の人がこの日報を更新しました。画面を再読み込みしてから、もう一度操作してください。'
    const api = createApiClient(vi.fn().mockResolvedValue(jsonResponse(409, { status: 409, title: '他の人が先に更新しました', detail })))

    const error = await api.submitReport(1, 1).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).isConflict).toBe(true)
    expect((error as ApiError).message).toBe(detail)
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
