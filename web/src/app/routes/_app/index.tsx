import { createFileRoute } from '@tanstack/react-router'
import {
  Building2,
  IdCard,
  Timer,
  TrendingDown,
  TrendingUp,
  Users,
} from 'lucide-react'
import { useState } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { useGetSystemInfo } from '@/shared/api/generated/system/system'
import {
  Badge,
  Button,
  Card,
  CardContent,
  CardHeader,
  PageHeader,
  SegmentedControl,
  StatCard,
} from '@/shared/ui'

export const Route = createFileRoute('/_app/')({
  component: Dashboard,
})

type Period = 'day' | 'week' | 'days15' | 'month'

function Dashboard() {
  const { t, num } = useLocale()
  const [period, setPeriod] = useState<Period>('month')

  // C# endpoint'idan generatsiya qilingan hook (qo'lda yozilmagan)
  const { data, isPending, isError } = useGetSystemInfo()

  return (
    <div className="space-y-6">
      <PageHeader
        title={t('dashboard.title')}
        subtitle={t('dashboard.subtitle', { company: t('app.company') })}
        actions={
          <SegmentedControl
            value={period}
            onChange={setPeriod}
            options={[
              { value: 'day', label: t('period.day') },
              { value: 'week', label: t('period.week') },
              { value: 'days15', label: t('period.days15') },
              { value: 'month', label: t('period.month') },
            ]}
          />
        }
      />

      {/* ---------- Asosiy ko'rsatkichlar ---------- */}
      <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label={t('dashboard.totalEmployees')}
          value={num(540)}
          icon={Users}
          tone="brand"
          hint={`${t('dashboard.departments')}: 73`}
        />
        <StatCard
          label={t('dashboard.onProbation')}
          value={num(146)}
          icon={Timer}
          tone="orange"
          hint={t('employee.probationUntil')}
        />
        <StatCard
          label={t('dashboard.hiredThisMonth')}
          value={num(23)}
          icon={TrendingUp}
          tone="green"
          delta={{ value: 12, label: t('period.month') }}
        />
        <StatCard
          label={t('dashboard.leftThisMonth')}
          value={num(17)}
          icon={TrendingDown}
          tone="red"
          delta={{ value: -8, label: t('period.month') }}
        />
      </section>

      {/* ---------- Tizim holati ---------- */}
      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader
            title={t('dashboard.backendStatus')}
            subtitle={t('dashboard.stageNote')}
          />
          <CardContent>
            {isPending && (
              <p className="text-subhead text-[var(--fg-secondary)]">
                {t('common.loading')}
              </p>
            )}

            {isError && (
              <div className="flex items-center gap-3">
                <Badge tone="danger" dot>
                  {t('common.error')}
                </Badge>
                <p className="text-footnote text-[var(--fg-secondary)]">
                  {t('dashboard.apiUnreachable')}
                </p>
              </div>
            )}

            {data && (
              <dl className="grid gap-x-8 gap-y-3 sm:grid-cols-2">
                <Row label={t('dashboard.service')} value={data.service} />
                <Row label={t('dashboard.version')} value={data.version} />
                <Row label={t('dashboard.environment')} value={data.environment} />
                <Row
                  label={t('dashboard.database')}
                  value={
                    <Badge tone={data.databaseConnected ? 'success' : 'danger'} dot>
                      {data.databaseConnected
                        ? t('dashboard.connected')
                        : t('dashboard.disconnected')}
                    </Badge>
                  }
                />
              </dl>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader
            title={t('nav.sectionCatalog')}
            action={
              <Button variant="ghost" size="sm">
                {t('common.edit')}
              </Button>
            }
          />
          <CardContent className="space-y-3">
            <CatalogRow
              icon={Building2}
              label={t('nav.departments')}
              value={num(73)}
              note="→ ~70"
            />
            <CatalogRow
              icon={IdCard}
              label={t('nav.positions')}
              value={num(228)}
              note="→ ~150"
            />
            <CatalogRow
              icon={Users}
              label={t('nav.directions')}
              value={num(26)}
              note="→ 8"
            />
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function Row({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 border-b border-[var(--border)] pb-2.5 last:border-0">
      <dt className="text-footnote text-[var(--fg-secondary)]">{label}</dt>
      <dd className="text-footnote font-medium">{value}</dd>
    </div>
  )
}

function CatalogRow({
  icon: Icon,
  label,
  value,
  note,
}: {
  icon: React.ComponentType<{ className?: string }>
  label: string
  value: string
  note: string
}) {
  return (
    <div className="flex items-center gap-3">
      <span className="grid size-9 shrink-0 place-items-center rounded-sm bg-[var(--bg-sunken)] text-[var(--fg-secondary)]">
        <Icon className="size-4" />
      </span>
      <span className="min-w-0 flex-1 truncate text-footnote">{label}</span>
      <span className="tabular text-footnote font-semibold">{value}</span>
      <span className="text-caption2 text-[var(--fg-tertiary)]">{note}</span>
    </div>
  )
}
