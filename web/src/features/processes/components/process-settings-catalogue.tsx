import { Users, Workflow } from 'lucide-react'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { useBlankRoles } from '@/features/blanks/api/blanks-queries'
import { BlankRolesDialog } from '@/features/blanks/components/blank-roles-dialog'
import { BLANKS_MANAGE, BLANKS_VIEW } from '@/features/blanks/lib/blank-permissions'
import { USERS_MANAGE } from '@/features/users/lib/user-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import { Badge, Button, Card, CardContent, CardHeader, EmptyState } from '@/shared/ui'

import type { ProcessTemplate } from '../api/processes-api'
import { useProcessTemplates } from '../api/processes-queries'
import { officeRolesOf } from '../lib/process-settings'
import { useProcessRoleLabel } from './process-chips'
import { ProcessSectionError } from './process-section-error'
import { ProcessListSkeleton, ProcessLoadError } from './process-states'

/**
 * Who holds each role the processes ask for. Assignment is the blanks' own (`blank_role_assignments`, §7.7), so editing
 * opens their dialog rather than a second one. It reads `/api/users`, hence the extra permission for the button.
 */
function RolesCard({ templates }: { templates: readonly ProcessTemplate[] }) {
  const { t } = useLocale()
  const { hasPermission } = useAuth()
  const roleLabel = useProcessRoleLabel()
  const assigned = useBlankRoles()
  const [editing, setEditing] = useState(false)

  const canEdit = hasPermission(BLANKS_MANAGE) && hasPermission(USERS_MANAGE)
  const roles = officeRolesOf(templates)

  if (roles.length === 0) return null

  return (
    <Card>
      <CardHeader
        title={t('processes.settings.roles.title')}
        subtitle={t('processes.settings.roles.hint')}
        action={
          canEdit ? (
            <Button size="sm" onClick={() => setEditing(true)}>
              <Users />
              {t('processes.settings.roles.edit')}
            </Button>
          ) : undefined
        }
      />
      <CardContent>
        {assigned.isPending ? (
          <ProcessListSkeleton rows={3} />
        ) : assigned.isError ? (
          <ProcessSectionError error={assigned.error} onRetry={() => void assigned.refetch()} />
        ) : (
          <dl className="divide-y divide-[var(--border)]">
            {roles.map((role) => {
              const users = assigned.data.find((row) => row.roleCode === role)?.users ?? []

              return (
                <div key={role} className="flex items-start justify-between gap-4 py-2.5 text-subhead">
                  <dt className="text-[var(--fg-secondary)]">{roleLabel(role)}</dt>
                  <dd className="min-w-0 text-right">
                    {users.length === 0 ? (
                      <span className="text-ios-orange">{t('processes.holder.unassigned')}</span>
                    ) : (
                      users.map((user) => user.fullName).join(', ')
                    )}
                  </dd>
                </div>
              )
            })}
          </dl>
        )}
      </CardContent>
      {editing && <BlankRolesDialog onClose={() => setEditing(false)} />}
    </Card>
  )
}

function TemplatesCard({ templates }: { templates: readonly ProcessTemplate[] }) {
  const { t, num } = useLocale()

  return (
    <Card>
      <CardHeader title={t('processes.settings.templates.title')} subtitle={t('processes.settings.templates.hint')} />
      <CardContent>
        {templates.length === 0 ? (
          <EmptyState icon={Workflow} title={t('processes.settings.templates.empty')} />
        ) : (
          <ul className="divide-y divide-[var(--border)]">
            {templates.map((template) => (
              <li key={template.code} className="flex items-center justify-between gap-3 py-2.5">
                <div className="min-w-0">
                  <p className="text-subhead">{template.title}</p>
                  <p className="text-footnote break-words text-[var(--fg-secondary)]">{template.source}</p>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <Badge tone="neutral">
                    {t('processes.settings.templates.steps', { n: num(template.steps.length) })}
                  </Badge>
                  <Badge tone="neutral">v{template.version}</Badge>
                </div>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

/** What the engine runs on, from `GET /templates`: the published processes and the roles they need filled. */
export function ProcessCatalogueSection() {
  const { hasPermission } = useAuth()
  const templates = useProcessTemplates()

  if (templates.isPending) return <ProcessListSkeleton rows={3} />
  if (templates.isError) {
    return <ProcessLoadError error={templates.error} onRetry={() => void templates.refetch()} />
  }

  return (
    <>
      {hasPermission(BLANKS_VIEW) && <RolesCard templates={templates.data} />}
      <TemplatesCard templates={templates.data} />
    </>
  )
}
