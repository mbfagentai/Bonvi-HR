import { ListFilter, X } from 'lucide-react'
import { useState } from 'react'

import { ListExportButton } from '@/features/reports/components/list-export'
import { useRoleLabels } from '@/features/roles/lib/role-presentation'
import type { AuditAction } from '@/shared/api/generated/model'
import { useLocale } from '@/shared/i18n/use-locale'
import { Button, DateRangeField, SegmentedControl, Select, type SelectOption } from '@/shared/ui'

import type { AuditActorOption } from '../api/audit-queries'
import { actionLabel, entityTypeLabel, useAuditFormatter } from '../lib/audit-format'
import {
  AUDIT_CHANGE_ACTIONS,
  AUDIT_MODULE_ENTITIES,
  AUDIT_SECURITY_ACTIONS,
  isAuditAction,
  isAuditEntity,
  isAuditModule,
  type AuditEntityName,
  type AuditModuleKey,
} from '../lib/audit-keys'
import { ACTOR_SYSTEM, type AuditKind } from '../lib/audit-search'

/** Toolbar o'zgartiradigan qiymatlar — bo'sh satr «filtr yo'q» degani. */
export interface AuditFilterPatch {
  from?: string
  to?: string
  actor?: string
  module?: AuditModuleKey | ''
  entity?: AuditEntityName | ''
  action?: AuditAction | ''
}

/**
 * Jurnal ustidagi boshqaruv — egasining «kim, nima, qachon, qaysi yozuv»i
 * (SPEC §9.5). `EmploymentsToolbar` naqshi: kundalik ishlatiladigan davr
 * doim ko'rinadi, qolgan to'rt filtr yig'iladigan panelda.
 *
 * Xodim filtri ATAYLAB yo'q: 2 400 kishi `Select` ga sig'maydi, bitta
 * xodimning tarixi esa uning kartasida («O'zgarishlar tarixi»).
 */
export function AuditToolbar({
  kind,
  canSeeSecurity,
  from,
  to,
  today,
  periodError,
  actor,
  module,
  entity,
  action,
  modules,
  actors,
  filtered,
  onKind,
  onChange,
  onClear,
  onExport,
  exporting = false,
  exportDisabled = false,
}: {
  kind: AuditKind
  canSeeSecurity: boolean
  from: string
  to: string
  today: string
  /** Davr noto'g'ri bo'lsa — tarjima qilingan xabar. */
  periodError: string | undefined
  actor: string
  module: AuditModuleKey | ''
  entity: AuditEntityName | ''
  action: AuditAction | ''
  /** So'rovchi ko'ra oladigan bo'limlar. */
  modules: readonly AuditModuleKey[]
  actors: readonly AuditActorOption[]
  /** Standartdan farqli biror narsa bormi — «Tozalash» tugmasi uchun. */
  filtered: boolean
  onKind: (kind: AuditKind) => void
  onChange: (patch: AuditFilterPatch) => void
  onClear: () => void
  /**
   * «Excelga yuklash». `undefined` — tugma umuman chizilmaydi (ruxsat yo'q).
   * So'rov sahifada: fayl ro'yxat bilan BITTA parametr obyektidan quriladi.
   */
  onExport?: () => void
  exporting?: boolean
  /** Davr noto'g'ri — ro'yxat ham so'ralmaydi, server 400 qaytarardi. */
  exportDisabled?: boolean
}) {
  const { t } = useLocale()
  const fmt = useAuditFormatter()
  const labels = useRoleLabels()

  const security = kind === 'security'
  const count = [actor, security ? '' : module, security ? '' : entity, action].filter(
    (value) => value !== '',
  ).length
  const [open, setOpen] = useState(count > 0)

  const actorOptions: SelectOption[] = [
    { value: ACTOR_SYSTEM, label: t('audit.system') },
    ...[...actors]
      .sort((a, b) => a.fullName.localeCompare(b.fullName))
      .map((item) => ({
        value: item.userId,
        label: item.isActive ? item.fullName : t('audit.actorInactive', { name: item.fullName }),
      })),
  ]

  const moduleOptions: SelectOption[] = modules.map((key) => ({ value: key, label: labels.module(key) }))

  // Yozuv turlari — faqat tanlangan bo'limniki (SPEC §9.5: bo'limsiz o'chiq)
  const entityOptions: SelectOption[] =
    module === ''
      ? []
      : AUDIT_MODULE_ENTITIES[module].map((name) => ({ value: name, label: entityTypeLabel(fmt, name) }))

  const actionOptions: SelectOption[] = (security ? AUDIT_SECURITY_ACTIONS : AUDIT_CHANGE_ACTIONS).map(
    (value) => ({ value, label: actionLabel(fmt, value) }),
  )

  return (
    <div className="border-b border-[var(--border)]">
      <div className="flex flex-wrap items-end gap-3 px-4 py-3.5 sm:px-5">
        {canSeeSecurity && (
          <SegmentedControl<AuditKind>
            value={kind}
            onChange={onKind}
            aria-label={t('audit.title')}
            className="w-full sm:w-auto"
            options={[
              { value: 'changes', label: t('audit.kindChanges') },
              { value: 'security', label: t('audit.kindSecurity') },
            ]}
          />
        )}

        {/*
          Davr — ikkala chegara doim TO'LA: berilmagani server qoidasi bilan
          hisoblangan qiymat (`effectivePeriod`), ya'ni maydonda ko'ringan
          sana va kelgan ro'yxat hech qachon farq qilmaydi.
        */}
        <DateRangeField
          label={t('audit.filterPeriod')}
          fromLabel={t('common.dateFrom')}
          toLabel={t('common.dateTo')}
          from={from}
          to={to}
          max={today}
          // Chala yozilgan sana — bo'sh qiymat: uni manzilga yozmaymiz,
          // aks holda maydon har harfda standart sanaga sakrardi.
          onFromChange={(value) => {
            if (value !== '') onChange({ from: value })
          }}
          onToChange={(value) => {
            if (value !== '') onChange({ to: value })
          }}
          className="min-w-0 flex-1 sm:max-w-[340px]"
        />

        <Button
          type="button"
          pill
          variant={open ? 'soft' : 'secondary'}
          aria-expanded={open}
          onClick={() => setOpen((previous) => !previous)}
        >
          <ListFilter />
          <span className="hidden sm:inline">{t('common.filters')}</span>
          {count > 0 && (
            <span className="tabular grid size-5 place-items-center rounded-full bg-[var(--accent)] text-caption2 font-semibold text-[var(--fg-on-accent)]">
              {count}
            </span>
          )}
        </Button>

        {/*
          Tozalash — davr ham, «Bitta yozuv» ham standartga qaytadi. Panel
          yopiq bo'lsa ham ko'rinadi: faqat davr o'zgartirilgan holat ham
          bir bosishda tiklanishi kerak.
        */}
        {filtered && (
          <Button type="button" size="sm" variant="ghost" onClick={onClear} className="mb-1">
            <X />
            {t('employees.clearFilters')}
          </Button>
        )}

        {/* Eksport — «Filtrlar» yonida: fayl aynan shu davr, ko'rinish va filtrga tushadi */}
        {onExport !== undefined && (
          <ListExportButton onClick={onExport} pending={exporting} disabled={exportDisabled} />
        )}
      </div>

      {/*
        Davr xatosi maydon ostida emas, qatorning ostida: maydon ichida
        chizilsa, qatordagi tugmalar pastki chiziq bo'yicha siljib ketardi.
      */}
      {periodError !== undefined && (
        <p role="alert" className="-mt-1.5 px-4 pb-3 text-footnote text-ios-red sm:px-5">
          {periodError}
        </p>
      )}

      {open && (
        <div className="border-t border-[var(--border)] bg-[var(--bg-sunken)] px-4 py-4 sm:px-5">
          <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <Select
              size="sm"
              label={t('audit.filterActor')}
              placeholder={t('audit.anyActor')}
              options={actorOptions}
              value={actor}
              onChange={(event) => onChange({ actor: event.target.value })}
            />

            {!security && (
              <>
                <Select
                  size="sm"
                  label={t('audit.filterModule')}
                  placeholder={t('audit.anyModule')}
                  options={moduleOptions}
                  value={module}
                  onChange={(event) => {
                    const value = event.target.value
                    // Bo'lim almashsa, oldingi bo'limning yozuv turi ma'nosiz
                    onChange({ module: isAuditModule(value) ? value : '', entity: '' })
                  }}
                />
                <Select
                  size="sm"
                  label={t('audit.filterEntity')}
                  placeholder={t('audit.anyEntity')}
                  options={entityOptions}
                  value={entity}
                  disabled={entityOptions.length === 0}
                  hint={module === '' ? t('audit.entityNeedsModule') : undefined}
                  onChange={(event) => {
                    const value = event.target.value
                    onChange({ entity: isAuditEntity(value) ? value : '' })
                  }}
                />
              </>
            )}

            <Select
              size="sm"
              label={t('audit.filterAction')}
              placeholder={t('audit.anyAction')}
              options={actionOptions}
              value={action}
              onChange={(event) => {
                const value = event.target.value
                onChange({ action: isAuditAction(value) ? value : '' })
              }}
            />
          </div>
        </div>
      )}
    </div>
  )
}
