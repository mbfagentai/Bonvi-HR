import { useLocale } from '@/shared/i18n/use-locale'
import { Badge } from '@/shared/ui'
import type { CouncilCandidate, CouncilOpinion } from '../api/council-api'

const DECISION_TONE = { Accept: 'success', Conditional: 'warning', Reserve: 'neutral', Reject: 'danger' } as const

/** Each member's conclusions together, latest first; the rows arrive latest first. */
export function groupOpinions(opinions: readonly CouncilOpinion[]): CouncilOpinion[][] {
  const members = new Map<string, CouncilOpinion[]>()

  for (const opinion of opinions) {
    members.set(opinion.memberId, [...(members.get(opinion.memberId) ?? []), opinion])
  }

  return [...members.values()]
}

/** What the members wrote from the Mini App. Read only: the final decision is the form below it. */
export function CouncilOpinions({ row }: { row: CouncilCandidate }) {
  const { t, dateTime } = useLocale()
  const groups = groupOpinions(row.opinions ?? [])

  if (groups.length === 0) return null

  return (
    <div className="space-y-2">
      <h3 className="text-sm font-medium text-[var(--fg-secondary)]">{t('council.opinions.title', { count: groups.length })}</h3>
      <div className="grid gap-2 lg:grid-cols-2">
        {groups.map((items) => (
          <div key={items[0]!.memberId} className="rounded-xl border border-[var(--border)] p-3 text-sm">
            <p className="font-medium text-[var(--fg)]">{items[0]!.memberName}</p>
            {items.map((opinion, index) => (
              <div key={opinion.at} className={index === 0 ? 'mt-2' : 'mt-2 border-t border-[var(--border)] pt-2 opacity-70'}>
                <div className="flex items-center justify-between gap-2">
                  <Badge tone={DECISION_TONE[opinion.decision]}>{t(`council.decision.${opinion.decision}`)}</Badge>
                  <span className="text-xs text-[var(--fg-secondary)]">{dateTime(opinion.at)}</span>
                </div>
                {opinion.comment !== null && <p className="mt-1 whitespace-pre-line text-[var(--fg)]">{opinion.comment}</p>}
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  )
}
