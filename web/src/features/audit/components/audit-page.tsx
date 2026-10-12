import { getRouteApi } from '@tanstack/react-router'
import { FileSearch, History, X } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import type { ListAuditEntriesParams } from '@/shared/api/generated/model'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { todayIso } from '@/shared/lib/today'
import { Alert, Card, EmptyState, IconTile, PageHeader, Pagination } from '@/shared/ui'

import { useAuditActors, useAuditEntries, type AuditEntry } from '../api/audit-queries'
import { entityTypeLabel, useAuditFormatter } from '../lib/audit-format'
import {
  AUDIT_MODULE_KEYS,
  AUDIT_PAGE_SIZE,
  AUDIT_SECURITY_PERMISSION,
  AUDIT_VIEW,
  auditModuleOfEntity,
  canSeeAuditModule,
} from '../lib/audit-keys'
import {
  effectivePeriod,
  isAuditFiltered,
  type AuditKind,
  type AuditSearch,
} from '../lib/audit-search'
import { AuditEntryDialog } from './audit-entry-dialog'
import { AuditList } from './audit-list'
import { AuditToolbar, type AuditFilterPatch } from './audit-toolbar'

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`employments-page.tsx` dagi naqsh).
 */
const route = getRouteApi('/_app/audit')

/** Modul darajasida — har render'da yangi massiv ro'yxat modelini buzadi. */
const NO_ENTRIES: AuditEntry[] = []

/**
 * ⭐ Audit jurnali — kim, qachon, nimani o'zgartirdi (SPEC §9.5).
 *
 * Har bir filtr manzil satrida (`/buyruqlar` naqshi). Ro'yxat — jurnalning
 * o'zi, hech narsa o'zgartirilmaydi va o'chirilmaydi: sahifada birorta ham
 * yozuvchi tugma yo'q (jurnal o'zgarmas, SPEC §7.7).
 *
 * Ruxsatsiz odam: marshrutda `beforeLoad` yo'q (`rollar.tsx`,
 * `foydalanuvchilar.tsx` bilan bir xil naqsh) — sahifaning o'zi tushunarli
 * holat ko'rsatadi va hech qanday so'rov yubormaydi.
 */
export function AuditPage() {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const fmt = useAuditFormatter()
  const { hasPermission } = useAuth()

  const canView = hasPermission(AUDIT_VIEW)
  const canSeeSecurity = hasPermission(AUDIT_SECURITY_PERMISSION)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const today = todayIso()
  const page = search.page ?? 1
  // `security` segmenti ruxsatsiz yashirin — eski havola ham «O'zgarishlar» ochadi (server ham shunday)
  const kind: AuditKind = search.kind === 'security' && canSeeSecurity ? 'security' : 'changes'
  const security = kind === 'security'
  const { from, to } = effectivePeriod(search, today)
  const periodInvalid = from > to

  /*
    «Bo'lim» URL'da bo'lmasa, lekin yozuv turi bor bo'lsa (kartadagi
    «Tuzatishlarni ko'rish» havolasi: `entity=EmploymentEvent`) — bo'lim
    turdan tiklanadi: filtr panelida tanlov ko'rinib tursin.
  */
  const module = search.module ?? (search.entity === undefined ? undefined : auditModuleOfEntity(search.entity) ?? undefined)

  const modules = useMemo(
    () => AUDIT_MODULE_KEYS.filter((key) => canSeeAuditModule(key, hasPermission)),
    [hasPermission],
  )

  const params = useMemo<ListAuditEntriesParams>(
    () => ({
      from,
      to,
      kind,
      actor: search.actor,
      // Xavfsizlik hodisalari bitta bo'limda (`users`) — bo'lim va tur filtri u yerda ma'nosiz
      module: security ? undefined : search.module,
      entity: security ? undefined : search.entity,
      entityId: search.entityId,
      action: search.action,
      page,
      pageSize: AUDIT_PAGE_SIZE,
    }),
    [from, to, kind, security, search.actor, search.module, search.entity, search.entityId, search.action, page],
  )

  const list = useAuditEntries(params, canView && !periodInvalid)
  const actors = useAuditActors(canView)

  /** Ochiq yozuv — `null` bo'lsa oyna yopiq. */
  const [opened, setOpened] = useState<AuditEntry | null>(null)

  /** Standart holatda `replace` — filtr aniqlashtirish, alohida qadam emas. */
  const setSearch = useCallback(
    (next: (previous: AuditSearch) => AuditSearch, history: 'push' | 'replace' = 'replace') => {
      void navigate({ to: '/audit', search: next, replace: history === 'replace' })
    },
    [navigate],
  )

  /** Filtr o'zgarganda 7-sahifada qolib ketmaslik kerak — u endi bo'sh bo'lishi mumkin. */
  const changeFilters = useCallback(
    (patch: AuditFilterPatch) => {
      setSearch((previous) => {
        const next: AuditSearch = { ...previous, page: undefined }

        if (patch.from !== undefined) next.from = patch.from === '' ? undefined : patch.from
        if (patch.to !== undefined) next.to = patch.to === '' ? undefined : patch.to
        if (patch.actor !== undefined) next.actor = patch.actor === '' ? undefined : patch.actor
        if (patch.module !== undefined) next.module = patch.module === '' ? undefined : patch.module
        if (patch.entity !== undefined) next.entity = patch.entity === '' ? undefined : patch.entity
        if (patch.action !== undefined) next.action = patch.action === '' ? undefined : patch.action

        return next
      })
    },
    [setSearch],
  )

  /**
   * Ko'rinish almashsa — amal, bo'lim va tur filtrlari tozalanadi: ikki
   * ko'rinishning amallari boshqa-boshqa («Kirdi» o'zgarishlarda yo'q).
   */
  const changeKind = useCallback(
    (next: AuditKind) => {
      setSearch((previous) => ({
        ...previous,
        kind: next === 'security' ? 'security' : undefined,
        action: undefined,
        module: undefined,
        entity: undefined,
        entityId: undefined,
        page: undefined,
      }))
    },
    [setSearch],
  )

  const clearFilters = useCallback(() => {
    setSearch((previous) => ({ kind: previous.kind }))
  }, [setSearch])

  const removeSingleRecord = useCallback(() => {
    setSearch((previous) => ({ ...previous, entityId: undefined, page: undefined }))
  }, [setSearch])

  const changePage = useCallback(
    (next: number) => {
      setSearch((previous) => ({ ...previous, page: next <= 1 ? undefined : next }), 'push')
    },
    [setSearch],
  )

  const header = (
    <PageHeader
      title={
        <span className="flex items-center gap-3">
          <IconTile icon={History} tone="brand" size="lg" />
          {t('audit.title')}
        </span>
      }
      subtitle={t('audit.subtitle')}
    />
  )

  if (!canView) {
    return (
      <div className="space-y-6">
        {header}
        <Card>
          <EmptyState
            icon={History}
            title={t('users.noAccessTitle')}
            description={t('audit.noAccessText')}
          />
        </Card>
      </div>
    )
  }

  const entries = list.data?.items ?? NO_ENTRIES
  const total = list.data?.total ?? 0
  const firstLoad = list.isPending && !periodInvalid
  const filtered = isAuditFiltered(search)

  const empty = filtered ? (
    <EmptyState
      icon={FileSearch}
      title={t('audit.notFoundTitle')}
      description={t('audit.notFoundText')}
    />
  ) : (
    <EmptyState icon={History} title={t('audit.emptyTitle')} description={t('audit.emptyText')} />
  )

  return (
    <div className="space-y-6">
      {header}

      <Card className="overflow-hidden">
        <AuditToolbar
          kind={kind}
          canSeeSecurity={canSeeSecurity}
          from={from}
          to={to}
          today={today}
          periodError={periodInvalid ? errorMessage('audit.davr_notogri') : undefined}
          actor={search.actor ?? ''}
          module={module ?? ''}
          entity={search.entity ?? ''}
          action={search.action ?? ''}
          modules={modules}
          actors={actors.data ?? []}
          filtered={filtered}
          onKind={changeKind}
          onChange={changeFilters}
          onClear={clearFilters}
        />

        {/*
          «Bitta yozuv» — kartadagi havoladan kelgan identifikator. Uni olib
          tashlash mumkin bo'lishi shart: aks holda HR o'sha yozuvdan boshqa
          hech narsani ko'rolmay, manzilni qo'lda tahrirlardi.
        */}
        {search.entityId !== undefined && !security && (
          <div className="flex flex-wrap items-center gap-2 border-b border-[var(--border)] px-4 py-2.5 sm:px-5">
            <span className="flex h-7 items-center gap-1.5 rounded-full bg-[var(--accent-soft)] pr-1 pl-2.5 text-caption1 font-medium text-[var(--accent-on-soft)]">
              {t('audit.singleRecord')}
              {search.entity !== undefined && (
                <span className="font-normal">{` · ${entityTypeLabel(fmt, search.entity)}`}</span>
              )}
              <button
                type="button"
                onClick={removeSingleRecord}
                aria-label={t('audit.removeSingleRecord')}
                title={t('audit.removeSingleRecord')}
                className="press grid size-5 place-items-center rounded-full hover:bg-[var(--bg-hover)]"
              >
                <X className="size-3.5" strokeWidth={2.5} />
              </button>
            </span>
          </div>
        )}

        {list.isError ? (
          <div className="px-4 py-4 sm:px-5">
            <Alert tone="danger" title={t('audit.loadFailed')}>
              {errorMessage(problemCode(list.error))}
            </Alert>
          </div>
        ) : periodInvalid ? (
          // Noto'g'ri davr — so'rov yuborilmaydi, sabab maydon ostida yozilgan
          <EmptyState
            icon={FileSearch}
            title={errorMessage('audit.davr_notogri')}
            description={t('audit.periodHint')}
          />
        ) : (
          <AuditList entries={entries} loading={firstLoad} empty={empty} onOpen={setOpened} />
        )}

        {!list.isError && !periodInvalid && total > 0 && (
          <Pagination
            page={page}
            pageSize={AUDIT_PAGE_SIZE}
            total={total}
            onPageChange={changePage}
            disabled={list.isFetching}
            className="border-t border-[var(--border)]"
          />
        )}
      </Card>

      <AuditEntryDialog entry={opened} onClose={() => setOpened(null)} onOpen={setOpened} />
    </div>
  )
}
