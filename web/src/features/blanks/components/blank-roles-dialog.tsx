import { LoaderCircle, Search } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useUsers } from '@/features/users/api/users-queries'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Checkbox, Dialog, Input, Select, Skeleton } from '@/shared/ui'

import { BLANK_OFFICE_ROLES } from '../api/blanks-api'
import { useBlankRoles, useSetBlankRoleUsers } from '../api/blanks-queries'
import { blankErrorCode } from '../lib/blank-error'

const MAX_USERS = 20

/**
 * Office-role assignments (#4/#5, `blanks.manage`). An empty set means «any
 * `blanks.approve` holder may sign this role's steps» (SPEC §6.2) — said in the hint.
 * One role is edited at a time; saving replaces that role's whole set.
 */
export function BlankRolesDialog({ onClose }: { onClose: () => void }) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()

  const roles = useBlankRoles()
  const users = useUsers('')
  const save = useSetBlankRoleUsers()

  const [roleCode, setRoleCode] = useState<string>(BLANK_OFFICE_ROLES[0])
  const [edits, setEdits] = useState<Record<string, string[]>>({})
  const [filter, setFilter] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const serverIds = useMemo(
    () => roles.data?.find((role) => role.roleCode === roleCode)?.users.map((user) => user.id) ?? [],
    [roles.data, roleCode],
  )
  const selected = edits[roleCode] ?? serverIds

  const roleOptions = useMemo(
    () =>
      BLANK_OFFICE_ROLES.map((code) => {
        const count = roles.data?.find((role) => role.roleCode === code)?.users.length ?? 0
        const label = t(`blanks.roles.codes.${code}`)
        return { value: code, label: count > 0 ? `${label} (${num(count)})` : label }
      }),
    [num, roles.data, t],
  )

  const visibleUsers = useMemo(() => {
    const needle = filter.trim().toLocaleLowerCase()
    return (users.data ?? [])
      .filter((user) => user.isActive || selected.includes(user.id))
      .filter((user) => needle === '' || user.fullName.toLocaleLowerCase().includes(needle))
  }, [filter, selected, users.data])

  function toggle(id: string, checked: boolean) {
    setSaved(false)
    setEdits((previous) => {
      const current = previous[roleCode] ?? serverIds
      const next = checked ? [...current, id] : current.filter((item) => item !== id)
      return { ...previous, [roleCode]: next }
    })
  }

  function submit() {
    setFailure(null)
    save.mutate(
      { roleCode, userIds: selected },
      {
        onSuccess: () => {
          setSaved(true)
          setEdits(({ [roleCode]: _done, ...rest }) => rest)
        },
        onError: (error) => setFailure(blankErrorCode(error)),
      },
    )
  }

  const loading = roles.isPending || users.isPending
  const dirty = edits[roleCode] !== undefined
  const tooMany = selected.length > MAX_USERS

  return (
    <Dialog
      open
      onClose={onClose}
      dismissible={!save.isPending}
      title={t('blanks.roles.title')}
      description={t('blanks.roles.text')}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={save.isPending}>
            {t('common.close')}
          </Button>
          <Button variant="primary" onClick={submit} disabled={save.isPending || !dirty || tooMany}>
            {save.isPending && <LoaderCircle className="animate-spin" />}
            {t('common.save')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        {failure !== null && <Alert tone="danger">{errorMessage(failure)}</Alert>}
        {roles.isError && <Alert tone="danger">{errorMessage(blankErrorCode(roles.error))}</Alert>}
        {users.isError && <Alert tone="danger">{errorMessage(blankErrorCode(users.error))}</Alert>}
        {saved && !dirty && <Alert tone="success">{t('blanks.roles.saved')}</Alert>}
        {tooMany && <Alert tone="warning">{t('blanks.roles.tooMany', { max: num(MAX_USERS) })}</Alert>}

        <Select
          label={t('blanks.roles.role')}
          options={roleOptions}
          value={roleCode}
          disabled={save.isPending}
          onChange={(event) => {
            setRoleCode(event.target.value)
            setSaved(false)
            setFailure(null)
          }}
        />
        <p className="text-footnote text-[var(--fg-secondary)]">
          {selected.length === 0 ? t('blanks.roles.anyApprover') : t('blanks.roles.selectedN', { n: num(selected.length) })}
        </p>

        <Input
          icon={Search}
          shape="pill"
          type="search"
          value={filter}
          onChange={(event) => setFilter(event.target.value)}
          placeholder={t('blanks.roles.searchUser')}
          aria-label={t('common.search')}
        />

        <div className="max-h-72 space-y-2 overflow-y-auto">
          {loading ? (
            <>
              <Skeleton className="h-6 w-2/3" />
              <Skeleton className="h-6 w-1/2" />
              <Skeleton className="h-6 w-3/5" />
            </>
          ) : visibleUsers.length === 0 ? (
            <p className="py-4 text-center text-footnote text-[var(--fg-secondary)]">{t('blanks.roles.noUsers')}</p>
          ) : (
            visibleUsers.map((user) => (
              <Checkbox
                key={user.id}
                checked={selected.includes(user.id)}
                onChange={(checked) => toggle(user.id, checked)}
                disabled={save.isPending}
                label={user.fullName}
              />
            ))
          )}
        </div>
      </div>
    </Dialog>
  )
}
