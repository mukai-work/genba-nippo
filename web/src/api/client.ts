import type {
  CreateSiteRequest,
  DailyReport,
  DailyReportSummary,
  ProblemDetails,
  ReportContent,
  Site,
  WorkItem,
  WorkItemRequest,
} from './types'

/** API が返したエラー。画面は message をそのまま出し、fieldErrors で入力欄ごとに赤くする。 */
export class ApiError extends Error {
  readonly status: number
  readonly fieldErrors: Record<string, string[]>

  constructor(status: number, problem: ProblemDetails | null) {
    super(ApiError.messageOf(status, problem))
    this.name = 'ApiError'
    this.status = status
    // サーバーは C# のプロパティ名（Name, EndDate）で返すので、画面側の名前（name, endDate）に揃える
    this.fieldErrors = Object.fromEntries(
      Object.entries(problem?.errors ?? {}).map(([key, value]) => [toCamel(key), value]),
    )
  }

  /** 他の人が先に更新していた（楽観的同時実行制御で弾かれた）か */
  get isConflict(): boolean {
    return this.status === 409
  }

  private static messageOf(status: number, problem: ProblemDetails | null): string {
    if (problem?.detail) return problem.detail
    if (problem?.errors) return Object.values(problem.errors).flat().join(' ')
    if (problem?.title) return problem.title
    return status >= 500
      ? 'サーバーでエラーが起きました。時間をおいてもう一度お試しください。'
      : `通信に失敗しました（${status}）。`
  }
}

function toCamel(key: string): string {
  return key.length > 0 ? key[0]!.toLowerCase() + key.slice(1) : key
}

type Fetcher = typeof fetch

export function createApiClient(fetcher: Fetcher = (...args) => fetch(...args), baseUrl = '/api') {
  async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
    let response: Response
    try {
      response = await fetcher(`${baseUrl}${path}`, {
        method,
        headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
        body: body === undefined ? undefined : JSON.stringify(body),
      })
    } catch {
      throw new ApiError(0, { detail: 'サーバーに接続できません。API が起動しているか確認してください。' })
    }

    if (!response.ok) {
      const problem = await response.json().catch(() => null) as ProblemDetails | null
      throw new ApiError(response.status, problem)
    }
    return (response.status === 204 ? undefined : await response.json()) as T
  }

  return {
    listSites: () => request<Site[]>('GET', '/sites'),
    getSite: (id: number) => request<Site>('GET', `/sites/${id}`),
    createSite: (body: CreateSiteRequest) => request<Site>('POST', '/sites', body),

    listWorkItems: (siteId: number) => request<WorkItem[]>('GET', `/sites/${siteId}/work-items`),
    createWorkItem: (siteId: number, body: WorkItemRequest) =>
      request<WorkItem>('POST', `/sites/${siteId}/work-items`, body),
    updateWorkItem: (id: number, body: WorkItemRequest) => request<WorkItem>('PUT', `/work-items/${id}`, body),

    listReports: (siteId: number, month: string) =>
      request<DailyReportSummary[]>('GET', `/sites/${siteId}/reports?month=${encodeURIComponent(month)}`),
    getReport: (id: number) => request<DailyReport>('GET', `/reports/${id}`),
    createReport: (siteId: number, body: ReportContent & { reportDate: string }) =>
      request<DailyReport>('POST', `/sites/${siteId}/reports`, body),
    updateReport: (id: number, version: number, body: ReportContent) =>
      request<DailyReport>('PUT', `/reports/${id}`, { ...body, version }),
    submitReport: (id: number, version: number) => request<DailyReport>('POST', `/reports/${id}/submit`, { version }),
    approveReport: (id: number, version: number) => request<DailyReport>('POST', `/reports/${id}/approve`, { version }),
    rejectReport: (id: number, version: number, reason: string) =>
      request<DailyReport>('POST', `/reports/${id}/reject`, { version, reason }),

    exportUrl: (siteId: number, month: string) =>
      `${baseUrl}/sites/${siteId}/reports/export?month=${encodeURIComponent(month)}`,
  }
}

export type ApiClient = ReturnType<typeof createApiClient>

export const api = createApiClient()
