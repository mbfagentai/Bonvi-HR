import { Check, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Card, CardContent, Checkbox, DateField, Input } from '@/shared/ui'
import type { CouncilFollowUp, CouncilMeeting, CouncilMember } from '../api/council-api'
import { useSaveCouncilMeeting } from '../api/council-queries'
import { recruitingErrorCode } from '../lib/recruiting-error'

const FOLLOW_UP_KEYS = ['resultsDeliveredOn', 'offersGivenOn', 'documentsHandedOn', 'registryOn', 'reserveOn'] as const

const EMPTY_MEMBER: CouncilMember = { fullName: '', role: null, present: false }

/** Attendance and the follow-up dates. A new sitting opens with the previous one's names, unticked. */
export function CouncilMembersCard({ meeting, canManage }: { meeting: CouncilMeeting; canManage: boolean }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const save = useSaveCouncilMeeting(meeting.date)

  const [members, setMembers] = useState<CouncilMember[]>(
    meeting.members.length > 0 ? meeting.members : [EMPTY_MEMBER, EMPTY_MEMBER, EMPTY_MEMBER, EMPTY_MEMBER],
  )
  const [followUp, setFollowUp] = useState<CouncilFollowUp>(meeting.followUp)
  const [dirty, setDirty] = useState(false)

  const change = (index: number, patch: Partial<CouncilMember>) => {
    setMembers((rows) => rows.map((row, at) => (at === index ? { ...row, ...patch } : row)))
    setDirty(true)
  }

  const present = members.filter((member) => member.present && member.fullName.trim() !== '').length

  return (
    <Card>
      <CardContent className="space-y-5 p-4 sm:p-5">
        <div>
          <h2 className="text-base font-semibold text-ink">{t('council.members')}</h2>
          <p className="mt-0.5 text-sm text-ink-secondary">{t('council.presentCount', { count: present })}</p>
        </div>

        <div className="space-y-2">
          {members.map((member, index) => (
            <div key={index} className="flex flex-wrap items-center gap-2">
              <Checkbox
                checked={member.present}
                disabled={!canManage}
                aria-label={t('council.present')}
                onChange={(checked) => change(index, { present: checked })}
              />
              <Input
                className="min-w-40 flex-1"
                value={member.fullName}
                disabled={!canManage}
                placeholder={t('council.memberName')}
                aria-label={t('council.memberName')}
                onChange={(event) => change(index, { fullName: event.target.value })}
              />
              <Input
                className="min-w-32 flex-1"
                value={member.role ?? ''}
                disabled={!canManage}
                placeholder={t('council.memberRole')}
                aria-label={t('council.memberRole')}
                onChange={(event) => change(index, { role: event.target.value })}
              />
              {canManage && (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  aria-label={t('council.removeMember')}
                  onClick={() => {
                    setMembers((rows) => rows.filter((_, at) => at !== index))
                    setDirty(true)
                  }}
                >
                  <Trash2 />
                </Button>
              )}
            </div>
          ))}
          {canManage && (
            <Button type="button" variant="ghost" size="sm" onClick={() => setMembers((rows) => [...rows, EMPTY_MEMBER])}>
              <Plus />
              {t('council.addMember')}
            </Button>
          )}
        </div>

        <div>
          <h2 className="text-base font-semibold text-ink">{t('council.followUpTitle')}</h2>
          <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {FOLLOW_UP_KEYS.map((key) => (
              <DateField
                key={key}
                label={t(`council.followUp.${key}`)}
                value={followUp[key] ?? ''}
                disabled={!canManage}
                onChange={(value) => {
                  setFollowUp((current) => ({ ...current, [key]: value === '' ? null : value }))
                  setDirty(true)
                }}
              />
            ))}
          </div>
        </div>

        {save.isError && <Alert tone="danger">{errorMessage(recruitingErrorCode(save.error))}</Alert>}

        {canManage && (
          <div className="flex items-center justify-end gap-3">
            {save.isSuccess && !dirty && (
              <span className="inline-flex items-center gap-1 text-sm text-ios-green">
                <Check className="size-4" />
                {t('council.saved')}
              </span>
            )}
            <Button
              size="sm"
              disabled={save.isPending}
              onClick={() => save.mutate({ members, followUp }, { onSuccess: () => setDirty(false) })}
            >
              {t('council.save')}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
