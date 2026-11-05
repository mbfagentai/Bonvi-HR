import { useMutation, useQuery } from '@tanstack/react-query'

import {
  fetchCouncilDays,
  fetchCouncilMeeting,
  saveCouncilDecision,
  saveCouncilMeeting,
  type CouncilDecisionInput,
  type CouncilFollowUp,
  type CouncilMember,
} from './council-api'

/** Under `['recruiting', …]`: `providers.tsx` invalidates it after every successful write. */
export const councilKeys = {
  days: ['recruiting', 'council', 'days'] as const,
  meeting: (date: string) => ['recruiting', 'council', 'meeting', date] as const,
}

export function useCouncilDays() {
  return useQuery({ queryKey: councilKeys.days, queryFn: fetchCouncilDays })
}

export function useCouncilMeeting(date: string) {
  return useQuery({ queryKey: councilKeys.meeting(date), queryFn: () => fetchCouncilMeeting(date) })
}

export function useSaveCouncilMeeting(date: string) {
  return useMutation({
    mutationFn: (body: { members: CouncilMember[]; followUp: CouncilFollowUp }) => saveCouncilMeeting(date, body),
  })
}

export function useSaveCouncilDecision(date: string, candidateId: string) {
  return useMutation({
    mutationFn: (body: CouncilDecisionInput) => saveCouncilDecision(date, candidateId, body),
  })
}
