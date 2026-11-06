import { api } from '@/shared/api/http-client'

export const COUNCIL_DECISIONS = ['Accept', 'Conditional', 'Reserve', 'Reject'] as const
export type CouncilDecision = (typeof COUNCIL_DECISIONS)[number]

export const COUNCIL_PRACTICALS = ['Passed', 'Failed', 'NotRequired'] as const
export type CouncilPractical = (typeof COUNCIL_PRACTICALS)[number]

export interface CouncilMember {
  fullName: string
  role: string | null
  present: boolean
}

export interface CouncilFollowUp {
  resultsDeliveredOn: string | null
  offersGivenOn: string | null
  documentsHandedOn: string | null
  registryOn: string | null
  reserveOn: string | null
}

export interface CouncilCandidate {
  candidateId: string
  fullName: string
  position: string
  formNumber: number | null
  isActive: boolean
  decided: boolean
  decision: CouncilDecision | null
  practical: CouncilPractical | null
  offeredSalary: number | null
  startDate: string | null
  condition: string | null
  conclusion: string | null
  opinions: CouncilOpinion[] | null
}

/** A member's own conclusion from Telegram. */
export interface CouncilOpinion {
  memberId: string
  memberName: string
  decision: CouncilDecision
  comment: string | null
  at: string
}

export interface CouncilGroupMember {
  id: string
  fullName: string
  phone: string
  isActive: boolean
  linked: boolean
  linkedAt: string | null
}

export interface CouncilGroup {
  groupBound: boolean
  groupTitle: string | null
  miniAppName: string | null
  botUsername: string | null
  members: CouncilGroupMember[]
}

const GROUP = '/api/recruiting/council-group'

export const fetchCouncilGroup = () => api.get<CouncilGroup>(GROUP).then((response) => response.data)
export const issueCouncilBindCode = () =>
  api.post<{ command: string; expiresAt: string }>(`${GROUP}/bind-code`).then((response) => response.data)
export const saveCouncilMiniApp = (name: string) => api.put(`${GROUP}/mini-app`, { name }).then(() => undefined)
export const addCouncilGroupMember = (body: { fullName: string; phone: string }) =>
  api.post(`${GROUP}/members`, body).then(() => undefined)
export const updateCouncilGroupMember = (id: string, body: { fullName: string; phone: string; isActive: boolean }) =>
  api.put(`${GROUP}/members/${id}`, body).then(() => undefined)
export const unlinkCouncilGroupMember = (id: string) =>
  api.post(`${GROUP}/members/${id}/unlink`).then(() => undefined)

export interface CouncilMeeting {
  date: string
  number: number | null
  members: CouncilMember[]
  followUp: CouncilFollowUp
  canSeeSalary: boolean
  candidates: CouncilCandidate[]
}

export interface CouncilDay {
  date: string
  invited: number
  decided: number
}

export interface CouncilDecisionInput {
  decision: CouncilDecision
  practical: CouncilPractical | null
  offeredSalary: number | null
  startDate: string | null
  condition: string | null
  conclusion: string | null
}

const BASE = '/api/recruiting/council-meetings'

export async function fetchCouncilDays(): Promise<CouncilDay[]> {
  const { data } = await api.get<CouncilDay[]>(BASE)
  return data
}

export async function fetchCouncilMeeting(date: string): Promise<CouncilMeeting> {
  const { data } = await api.get<CouncilMeeting>(`${BASE}/${date}`)
  return data
}

export async function saveCouncilMeeting(
  date: string,
  body: { members: CouncilMember[]; followUp: CouncilFollowUp },
): Promise<void> {
  await api.put(`${BASE}/${date}`, body)
}

export async function saveCouncilDecision(
  date: string,
  candidateId: string,
  body: CouncilDecisionInput,
): Promise<void> {
  await api.put(`${BASE}/${date}/candidates/${candidateId}`, body)
}
