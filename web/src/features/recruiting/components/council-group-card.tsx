import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, Link2Off, Plus } from 'lucide-react'
import { useState, type FormEvent } from 'react'

import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Badge, Button, Card, CardContent, CardHeader, TextField } from '@/shared/ui'
import {
  addCouncilGroupMember,
  fetchCouncilGroup,
  issueCouncilBindCode,
  saveCouncilMiniApp,
  unlinkCouncilGroupMember,
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
          {group.data.members.map((member) => (
            <MemberRow key={member.id} member={member} />
          ))}
          <AddMember />
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
  const unlink = useMutation({ mutationFn: () => unlinkCouncilGroupMember(member.id) })
  const toggle = useMutation({
    mutationFn: () => updateCouncilGroupMember(member.id, { fullName: member.fullName, phone: member.phone, isActive: !member.isActive }),
  })

  return (
    <div className="flex flex-wrap items-center gap-2 rounded-xl border border-[var(--border)] p-3 text-sm">
      <span className="min-w-0 flex-1">
        <span className="block font-medium text-[var(--fg)]">{member.fullName}</span>
        <span className="block text-[var(--fg-secondary)]">{member.phone}</span>
      </span>
      {!member.isActive ? (
        <Badge tone="neutral">{t('council.group.inactive')}</Badge>
      ) : (
        <Badge tone={member.linked ? 'success' : 'warning'}>{t(member.linked ? 'council.group.linked' : 'council.group.notLinked')}</Badge>
      )}
      {member.linked && (
        <Button type="button" size="sm" variant="secondary" aria-label={t('council.group.unlink')} disabled={unlink.isPending} onClick={() => unlink.mutate()}>
          <Link2Off />
        </Button>
      )}
      <Button type="button" size="sm" variant="secondary" disabled={toggle.isPending} onClick={() => toggle.mutate()}>
        {t(member.isActive ? 'council.group.deactivate' : 'council.group.activate')}
      </Button>
    </div>
  )
}

function AddMember() {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const queries = useQueryClient()
  const [fullName, setFullName] = useState('')
  const [phone, setPhone] = useState('')
  const add = useMutation({
    mutationFn: () => addCouncilGroupMember({ fullName: fullName.trim(), phone: phone.trim() }),
    onSuccess: async () => {
      setFullName('')
      setPhone('')
      await queries.invalidateQueries({ queryKey: QUERY_KEY })
    },
  })

  const submit = (event: FormEvent) => {
    event.preventDefault()
    add.mutate()
  }

  return (
    <form className="space-y-2 pt-2" onSubmit={submit}>
      <div className="grid gap-2 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
        <TextField label={t('council.group.name')} value={fullName} maxLength={200} onChange={(event) => setFullName(event.target.value)} />
        <TextField
          label={t('council.group.phone')}
          inputMode="tel"
          value={phone}
          onChange={(event) => setPhone(event.target.value)}
        />
        <Button type="submit" variant="primary" size="lg" disabled={add.isPending || fullName.trim() === '' || phone.trim() === ''}>
          <Plus />
          {t('council.group.add')}
        </Button>
      </div>
      <p className="text-footnote text-[var(--fg-tertiary)]">{t('council.group.phoneHint')}</p>
      {add.isError && <Alert tone="danger">{errorMessage(recruitingErrorCode(add.error))}</Alert>}
    </form>
  )
}
