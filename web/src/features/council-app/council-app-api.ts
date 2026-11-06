import type { CandidateQuestionnaire, CandidateSource } from '@/features/recruiting/api/recruiting-api'
import type { CouncilDecision } from '@/features/recruiting/api/council-api'

const BASE: string = import.meta.env['VITE_API_URL'] ?? 'http://localhost:7080'

export interface CouncilAppMe {
  recognized: boolean
  unavailable: boolean
  fullName: string | null
}

export interface CouncilAppRow {
  id: string
  fullName: string
  position: string
  interviewAt: string
  hasMyOpinion: boolean
}

export interface CouncilMyOpinion {
  decision: CouncilDecision
  comment: string | null
  at: string
}

export interface CouncilAppCandidate {
  id: string
  fullName: string
  position: string
  birthDate: string | null
  phone: string | null
  address: string | null
  speciality: string | null
  interviewAt: string
  hasPhoto: boolean
  source: CandidateSource
  questionnaire: CandidateQuestionnaire | null
  forms: { id: string; number: number }[]
  myOpinions: CouncilMyOpinion[]
}

/** A request failed with this HTTP status. */
export class CouncilAppError extends Error {
  constructor(readonly status: number, readonly code: string | null) {
    super(code ?? String(status))
  }
}

/** The client for one launch: every request carries Telegram's signed launch data. */
export function councilAppClient(initData: string) {
  const request = async (path: string, init?: RequestInit): Promise<Response> => {
    const response = await fetch(`${BASE}/api/council-app${path}`, {
      ...init,
      headers: { 'X-Telegram-Init-Data': initData, ...init?.headers },
    })

    if (!response.ok) {
      const problem = await response.json().catch(() => null) as { title?: string } | null
      throw new CouncilAppError(response.status, problem?.title ?? null)
    }

    return response
  }

  return {
    me: async () => (await (await request('/me')).json()) as CouncilAppMe,
    list: async () => (await (await request('/candidates')).json()) as CouncilAppRow[],
    candidate: async (id: string) => (await (await request(`/candidates/${id}`)).json()) as CouncilAppCandidate,
    photo: async (id: string) => (await request(`/candidates/${id}/photo`)).blob(),
    pdf: async (id: string, formId: string) => (await request(`/candidates/${id}/forms/${formId}/pdf`)).blob(),
    saveOpinion: async (id: string, body: { decision: CouncilDecision; comment: string | null }) => {
      await request(`/candidates/${id}/opinions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })
    },
  }
}

export type CouncilAppClient = ReturnType<typeof councilAppClient>
