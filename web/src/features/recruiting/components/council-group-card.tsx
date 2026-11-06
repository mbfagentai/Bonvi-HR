import { useMutation, useQuery } from '@tanstack/react-query'
import { Copy } from 'lucide-react'
import { useState } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Badge, Button, Card, CardContent, CardHeader, TextField } from '@/shared/ui'
import {
  fetchCouncilGroup,
  issueCouncilBindCode,
  saveCouncilMiniApp,
  updateCouncilGroupMember,
  type CouncilGroupMember,
} from '../api/council-api'
import { recruitingErrorCode } from '../lib/recruiting-error'

const QUERY_KEY = ['recruiting', 'council', 'group'] as const

/** Who may give conclusions from Telegram, and which group the bot posts to. Settings holders only. */
export function CouncilGroupCard() {
  const { t } = useLocale()
  const group = useQuery({ queryKey: QUERY_KEY, queryFn: fetchCouncilGroup })

  if (group.isError) return <Alert tone="danger">{t('council.loadError')}</Alert>
  if (group.isPending) return null

  return (
    <Card>
      <CardHeader title={t('council.group.title')} subtitle={t('council.group.subtitle')} />
      <CardContent className="space-y-5">
        <BindGroup bound={group.data.groupBound} title={group.data.groupTitle} />
        <MiniAppName key={group.data.miniAppName ?? ''} saved={group.data.miniAppName ?? ''} />

        <div className="space-y-2 border-t border-[var(--border)] pt-5">
          <h3 className="text-subhead font-semibold text-[var(--fg)]">{t('council.group.members')}</h3>
          <p className="text-footnote text-[var(--fg-tertiary)]">{t('council.group.membersHint')}</p>
          {group.data.members.map((member) => (
            <MemberRow key={member.id} member={member} />
          ))}
        </div>
      </CardContent>
    </Card>
  )
}

function BindGroup({ bound, title }: { bound: boolean; title: string | null }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const issue = useMutation({ mutationFn: issueCouncilBindCode })

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap items-center gap-3">
        <Badge tone={bound ? 'success' : 'warning'}>{bound ? t('council.group.bound', { title: title ?? '' }) : t('council.group.notBound')}</Badge>
        <Button type="button" size="sm" variant="secondary" disabled={issue.isPending} onClick={() => issue.mutate()}>
          {t('council.group.bind')}
        </Button>
      </div>
      {issue.isError && <Alert tone="danger">{errorMessage(recruitingErrorCode(issue.error))}</Alert>}
      {issue.data !== undefined && (
        <div className="rounded-xl border border-[var(--border)] p-3 text-sm">
          <p className="text-[var(--fg-secondary)]">{t('council.group.bindHint')}</p>
          <div className="mt-2 flex items-center gap-2">
            <code className="min-w-0 flex-1 break-all text-[var(--fg)]">{issue.data.command}</code>
            <Button
              type="button"
              size="sm"
              variant="secondary"
              aria-label={t('council.group.copy')}
              onClick={() => void navigator.clipboard.writeText(issue.data.command)}
            >
              <Copy />
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}

function MiniAppName({ saved }: { saved: string }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const [name, setName] = useState(saved)
  const save = useMutation({ mutationFn: () => saveCouncilMiniApp(name.trim()) })

  return (
    <div className="space-y-2">
      <div className="flex items-end gap-2 sm:max-w-lg">
        <TextField
          className="min-w-0 flex-1"
          label={t('council.group.miniApp')}
          value={name}
          maxLength={30}
          onChange={(event) => setName(event.target.value)}
        />
        <Button type="button" size="lg" disabled={save.isPending || name.trim() === saved} onClick={() => save.mutate()}>
          {t('council.save')}
        </Button>
      </div>
      {save.isError ? (
        <p className="text-footnote text-ios-red">{errorMessage(recruitingErrorCode(save.error))}</p>
      ) : (
        <p className="text-footnote text-[var(--fg-tertiary)]">{t('council.group.miniAppHint')}</p>
      )}
    </div>
  )
}

function MemberRow({ member }: { member: CouncilGroupMember }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const [name, setName] = useState(member.fullName)
  const save = useMutation({
    mutationFn: (isActive: boolean) => updateCouncilGroupMember(member.id, { fullName: name.trim(), phone: member.phone, isActive }),
  })
  const telegram = [member.telegramName, member.telegramUsername !== null && member.telegramUsername !== '' ? `@${member.telegramUsername}` : null]
    .filter(Boolean)
    .join(' · ')
  const changed = name.trim() !== member.fullName

  return (
    <div className="space-y-2 rounded-xl border border-[var(--border)] p-3 text-sm">
      <div className="flex flex-wrap items-center gap-2">
        <TextField
          className="min-w-48 flex-1"
          aria-label={t('council.group.name')}
          value={name}
          maxLength={200}
          onChange={(event) => setName(event.target.value)}
        />
        {changed && (
          <Button type="button" size="lg" variant="primary" disabled={save.isPending || name.trim() === ''} onClick={() => save.mutate(member.isActive)}>
            {t('council.save')}
          </Button>
        )}
        {!member.isActive ? (
          <Badge tone="neutral">{t('council.group.inactive')}</Badge>
        ) : (
          !member.nameConfirmed && <Badge tone="warning">{t('council.group.nameNeeded')}</Badge>
        )}
        <Button type="button" size="lg" disabled={save.isPending} onClick={() => save.mutate(!member.isActive)}>
          {t(member.isActive ? 'council.group.deactivate' : 'council.group.activate')}
        </Button>
      </div>
      {telegram !== '' && <p className="text-footnote text-[var(--fg-secondary)]">Telegram: {telegram}</p>}
      {save.isError && <Alert tone="danger">{errorMessage(recruitingErrorCode(save.error))}</Alert>}
    </div>
  )
}
