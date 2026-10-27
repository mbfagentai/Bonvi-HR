import clsx from 'clsx'
import { useEffect, useRef, useState, type ChangeEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'

import type { ApiError } from '@/api/errors'
import type { TeamBoardCountsDto, TeamBoardItemDto, TeamBoardStatus } from '@/api/types'
import { useTeamPhoto } from '@/data/photo'
import { useMe } from '@/data/queries'
import { TEAM_STATUS_LOOK } from '@/data/status-look'
import {
  TEAM_MIN_DATE,
  TEAM_REFRESH_MS,
  TEAM_STATUS_COUNT,
  departmentOptions,
  invalidateTeam,
  phoneHref,
  statusFromSlug,
  statusSlug,
  useTeamBoard,
  useTeamScope,
  visibleStatuses,
  type TeamDepartmentOption,
} from '@/data/team'
import { useFormat } from '@/lib/format'
import { addDays, parseIsoDate, todayIso } from '@/lib/time'
import { useErrorText } from '@/lib/use-error-message'
import { TabScreen } from '@/ui/app-shell'
import { Avatar } from '@/ui/avatar'
import { Button } from '@/ui/button'
import { Card } from '@/ui/card'
import { EmptyState, ErrorState, InlineMessage, Skeleton } from '@/ui/feedback'
import { TextInput } from '@/ui/form'
import { Icon } from '@/ui/icon'
import { Sheet } from '@/ui/sheet'
import { StatusChip } from '@/ui/status-chip'

/**
 * JAMOA — bo'lim boshlig'ining paneli (SPEC-jamoam §F).
 *
 * Savol bitta: «bugun kim keldi, kim kechikdi, kim kelmadi». Shu sababli
 * tartib: sana va bo'lim, keyin sanoq kartalari (bosilsa ro'yxat shu holatga
 * toraytiriladi), keyin odamlar. Har bir qatordan odamga qo'ng'iroq qilsa
 * bo'ladi — kelmaganni chaqirish boshliqning birinchi ishi.
 *
 * Filtrlar MANZILDA yashaydi (`?kun=&bolim=&holat=&qidiruv=`): a'zo ekranidan
 * «orqaga» qaytilganda tanlangan «Kelmadi» va qidiruv yo'qolmaydi, ro'yxat
 * esa keshdan avvalgi joyida chiziladi.
 */
const SEARCH_DELAY_MS = 350

export function TeamScreen() {
  const { t } = useTranslation()
  const format = useFormat()
  const errorText = useErrorText()
  const me = useMe()
  const [params, setParams] = useSearchParams()

  const isLead = me.data?.isTeamLead === true
  const today = todayIso()

  // Kelajak va noto'g'ri sana — xato emas, shunchaki bugun (taqvimdagi qoida)
  const rawDate = params.get('kun')
  const date = rawDate && parseIsoDate(rawDate) && rawDate >= TEAM_MIN_DATE && rawDate < today ? rawDate : null
  const departmentId = params.get('bolim') || null
  const status = statusFromSlug(params.get('holat'))
  const search = params.get('qidiruv') ?? ''

  const scope = useTeamScope(isLead)
  const board = useTeamBoard({ date, departmentId, status, search }, isLead)

  const update = (changes: Record<string, string | null>): void => {
    setParams(
      (previous) => {
        const next = new URLSearchParams(previous)
        Object.keys(changes).forEach((key) => {
          const value = changes[key]
          if (value === null || value === undefined || value === '') next.delete(key)
          else next.set(key, value)
        })
        return next
      },
      { replace: true },
    )
  }

  // Qidiruv: har harfda so'rov ketmaydi; manzil esa so'rov bilan bir vaqtda o'zgaradi
  const [draft, setDraft] = useState(search)
  useEffect(() => {
    if (draft.trim() === search.trim()) return undefined
    const timer = window.setTimeout(() => update({ qidiruv: draft.trim() || null }), SEARCH_DELAY_MS)
    return () => window.clearTimeout(timer)
    // `update` bog'liqlikda yo'q: u har renderda yangi, lekin manzilni FUNKSIONAL
    // yangilaydi — kutish paytida bosilgan karta filtri ustidan yozilmaydi
  }, [draft, search])

  /*
    Boshliqlik olib tashlandi (server 403 `jamoa.boshliq_emas`) — keshlar
    tozalanadi va `me` qayta o'qiladi: tab o'zi yo'qoladi, ekranda esa
    sababi yoziladi. Ma'lumot BIRORTA ham qolmaydi.
  */
  const revoked = isCode(board.error, 'jamoa.boshliq_emas') || isCode(scope.error, 'jamoa.boshliq_emas')
  const { refetch: refetchMe } = me
  useEffect(() => {
    if (!revoked) return
    invalidateTeam()
    refetchMe()
  }, [revoked, refetchMe])

  const view = board.data
  const isToday = view ? view.isToday : date === null
  const shownDate = date ?? (view && view.isToday ? view.date : today)

  // Avtomatik yangilash — faqat bugungi panelda va ekran ko'rinib turganda
  const { refetch } = board
  const fetchedAt = useRef<number | null>(null)
  fetchedAt.current = board.fetchedAt ? board.fetchedAt.getTime() : null

  useEffect(() => {
    if (!isLead || !isToday) return undefined

    const timer = window.setInterval(() => {
      if (document.visibilityState === 'visible') refetch()
    }, TEAM_REFRESH_MS)

    // Telefon cho'ntakdan olindi — eskirgan bo'lsa darhol, daqiqani kutmasdan
    const onVisible = (): void => {
      if (document.visibilityState !== 'visible') return
      const last = fetchedAt.current
      if (last === null || Date.now() - last >= TEAM_REFRESH_MS) refetch()
    }

    document.addEventListener('visibilitychange', onVisible)
    return () => {
      window.clearInterval(timer)
      document.removeEventListener('visibilitychange', onVisible)
    }
  }, [isLead, isToday, refetch])

  /*
    Sanoqlar holat va qidiruvga BOG'LIQ EMAS (server: qidiruv va holatdan
    oldingi to'plam) — karta bosilganda ro'yxat qayta yuklanadi, kartalar esa
    skeletga aylanmasdan joyida turadi.
  */
  const countsKey = `${date ?? 'bugun'}:${departmentId ?? 'hammasi'}`
  const lastCounts = useRef<{ key: string; counts: TeamBoardCountsDto } | null>(null)
  if (view) lastCounts.current = { key: countsKey, counts: view.counts }
  const counts = view
    ? view.counts
    : lastCounts.current && lastCounts.current.key === countsKey
      ? lastCounts.current.counts
      : null

  // «Ko'proq» — bosilgan lahzadan fon so'rovi tugaguncha
  const [more, setMore] = useState<'idle' | 'loading' | 'failed'>('idle')
  // Yiqilish sababi — 429 bo'lsa «N soniyadan keyin» matni (keyingi yangilash xatoni o'chirsa ham qoladi)
  const [moreError, setMoreError] = useState<ApiError | null>(null)
  const moreStarted = useRef(false)
  useEffect(() => {
    if (more !== 'loading') return
    if (board.isRefreshing) {
      moreStarted.current = true
      return
    }
    if (moreStarted.current) {
      moreStarted.current = false
      setMoreError(board.error)
      setMore(board.error ? 'failed' : 'idle')
    }
  }, [more, board.isRefreshing, board.error])

  const [dateOpen, setDateOpen] = useState(false)
  const [departmentOpen, setDepartmentOpen] = useState(false)

  const goDate = (next: string | null): void => {
    setMore('idle')
    update({ kun: next && next >= TEAM_MIN_DATE && next < today ? next : null })
  }

  const toggleStatus = (next: TeamBoardStatus): void => {
    setMore('idle')
    update({ holat: status === next ? null : statusSlug(next) })
  }

  const clearFilters = (): void => {
    setDraft('')
    update({ holat: null, qidiruv: null })
  }

  if (me.isLoading && !me.data) {
    return (
      <TabScreen title={t('team.title')}>
        <BoardSkeleton />
      </TabScreen>
    )
  }

  if (me.error && !me.data) {
    return (
      <TabScreen title={t('team.title')} staleSince={me.fetchedAt}>
        <Card>
          <ErrorState onRetry={me.refetch} />
        </Card>
      </TabScreen>
    )
  }

  if (!isLead || revoked) {
    return (
      <TabScreen title={t('team.title')}>
        <Card>
          <EmptyState icon="users" title={t('team.notLeadTitle')} text={t('team.notLeadText')} />
        </Card>
      </TabScreen>
    )
  }

  const departments = scope.data ? departmentOptions(scope.data.departments) : []
  const selectedDepartment = departments.filter((item) => item.id === departmentId)[0] ?? null
  const statuses = visibleStatuses(counts, status)
  const filtered = status !== null || search.trim() !== ''

  return (
    <TabScreen
      title={t('team.title')}
      staleSince={board.fetchedAt}
      subtitle={isToday && view ? t('team.updatedAt', { time: format.time(view.asOf) }) : undefined}
    >
      <Card>
        <div className="flex items-center">
          <button
            type="button"
            aria-label={t('team.prevDay')}
            disabled={shownDate <= TEAM_MIN_DATE}
            onClick={() => goDate(addDays(shownDate, -1))}
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-control text-accent-text active:bg-bg-pressed disabled:text-fg-tertiary"
          >
            <Icon name="chevron-left" />
          </button>

          <button
            type="button"
            onClick={() => setDateOpen(true)}
            aria-label={t('team.pickDateAria', { date: format.dateLong(shownDate) })}
            className="mx-1 min-w-0 flex-1 rounded-control px-2 py-1 text-center active:bg-bg-pressed"
          >
            <span className="block text-title3 font-semibold text-fg">
              {isToday ? t('team.today') : format.dateLong(shownDate)}
            </span>
            {isToday ? (
              <span className="mt-0.5 block text-footnote text-fg-secondary">{format.dateLong(shownDate)}</span>
            ) : null}
          </button>

          <button
            type="button"
            aria-label={t('team.nextDay')}
            disabled={date === null}
            onClick={() => goDate(addDays(shownDate, 1))}
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-control text-accent-text active:bg-bg-pressed disabled:text-fg-tertiary"
          >
            <Icon name="chevron-right" />
          </button>
        </div>

        {/* Bo'lim tanlagichi — qamrovda bittadan ko'p bo'lim bo'lsagina (SPEC §F) */}
        {departments.length > 1 ? (
          <div className="mt-3 border-t border-border pt-2">
            <button
              type="button"
              onClick={() => setDepartmentOpen(true)}
              className="flex min-h-[44px] w-full items-center rounded-control text-left active:bg-bg-pressed"
            >
              <span className="shrink-0 text-footnote text-fg-secondary">{t('team.department')}</span>
              <span className="ml-3 min-w-0 flex-1 text-right text-subhead text-fg server-text">
                {selectedDepartment ? selectedDepartment.name : t('team.allDepartments')}
              </span>
              <Icon name="chevron-right" className="ml-1 h-5 w-5 text-fg-tertiary" />
            </button>
          </div>
        ) : null}
      </Card>

      {counts === null ? (
        <TilesSkeleton />
      ) : (
        <div className="grid grid-cols-3 gap-2" role="group" aria-label={t('team.summaryAria')}>
          {statuses.map((item) => (
            <SummaryTile
              key={item}
              status={item}
              count={counts[TEAM_STATUS_COUNT[item]]}
              active={status === item}
              onClick={() => toggleStatus(item)}
            />
          ))}
        </div>
      )}

      {/* Yorliq ko'rinmaydi (joy tor), lekin ekran o'quvchi uni o'qiydi */}
      <label className="block">
        <span className="sr-only">{t('team.search')}</span>
        <TextInput
          value={draft}
          onChange={setDraft}
          placeholder={t('team.searchPlaceholder')}
          autoComplete="off"
        />
      </label>

      <Card title={view ? t('team.listTitle', { n: format.number(view.totalItems) }) : t('team.listTitleEmpty')}>
        {board.isLoading && !view ? (
          <div>
            <MemberRowSkeleton />
            <MemberRowSkeleton />
            <MemberRowSkeleton />
            <MemberRowSkeleton />
          </div>
        ) : board.error && !view ? (
          <BoardError
            error={board.error}
            onRetry={board.refetch}
            onAllDepartments={() => update({ bolim: null })}
            onToday={() => goDate(null)}
          />
        ) : view && view.items.length === 0 ? (
          filtered ? (
            <EmptyState
              icon="users"
              title={t('team.notFoundTitle')}
              text={t('team.notFoundText')}
              action={
                <Button variant="secondary" size="compact" onClick={clearFilters}>
                  {t('team.clearFilters')}
                </Button>
              }
            />
          ) : (
            <EmptyState icon="users" title={t('team.emptyTitle')} text={t('team.emptyText')} />
          )
        ) : view ? (
          <>
            <div>
              {view.items.map((item) => (
                <MemberRow key={item.employeeId} item={item} date={date} />
              ))}
            </div>

            {view.hasMore ? (
              <div className="mt-3">
                <Button
                  variant="secondary"
                  block
                  busy={more === 'loading'}
                  onClick={() => {
                    setMore('loading')
                    board.loadMore()
                  }}
                >
                  {t('team.loadMore')}
                </Button>
                {more === 'failed' ? (
                  <p className="mt-2 text-center text-footnote text-danger-text" role="status">
                    {moreError && moreError.kind === 'rejected' && moreError.code
                      ? errorText(moreError).body
                      : t('state.loadFailed')}
                  </p>
                ) : null}
              </div>
            ) : null}

            <p className="nums mt-3 text-center text-caption1 text-fg-tertiary">
              {t('team.shownOf', {
                shown: format.number(view.items.length),
                total: format.number(view.totalItems),
              })}
            </p>
          </>
        ) : null}
      </Card>

      <DateSheet
        open={dateOpen}
        value={shownDate}
        max={today}
        onPick={(next) => {
          setDateOpen(false)
          goDate(next)
        }}
        onClose={() => setDateOpen(false)}
      />

      <DepartmentSheet
        open={departmentOpen}
        options={departments}
        value={departmentId}
        onPick={(id) => {
          setDepartmentOpen(false)
          setMore('idle')
          update({ bolim: id })
        }}
        onClose={() => setDepartmentOpen(false)}
      />
    </TabScreen>
  )
}

function isCode(error: ApiError | null, code: string): boolean {
  return error !== null && error.code === code
}

/**
 * Sanoq kartasi — iOS «Eslatmalar» dagi aqlli ro'yxatlar kabi: son katta,
 * nomi ostida. Bosilsa — ro'yxat shu holatga toraytiriladi, yana bosilsa —
 * filtr olib tashlanadi. Tanlangani — taqvimdagi tanlangan katak kabi to'liq
 * aksent rangda.
 */
function SummaryTile({
  status,
  count,
  active,
  onClick,
}: {
  status: TeamBoardStatus
  count: number
  active: boolean
  onClick: () => void
}) {
  const { t } = useTranslation()
  const format = useFormat()
  const look = TEAM_STATUS_LOOK[status]
  const label = t(look.labelKey)

  return (
    <button
      type="button"
      aria-pressed={active}
      aria-label={t('team.tileAria', { status: label, n: count })}
      onClick={onClick}
      className={clsx(
        'flex min-h-[72px] min-w-0 flex-col items-start justify-between rounded-card px-3 py-2.5 text-left transition-colors duration-150 ease-ios',
        active ? 'bg-accent text-fg-on-accent' : 'bg-bg-elevated text-fg active:bg-bg-pressed',
      )}
    >
      <span className="flex w-full items-center">
        {look.dotClass ? (
          <span className={clsx('mr-1.5 h-2 w-2 shrink-0 rounded-pill', look.dotClass)} aria-hidden="true" />
        ) : null}
        <span className="nums text-title2 font-bold">{format.number(count)}</span>
      </span>
      <span
        className={clsx(
          // Uzun so'z (ru «Рабочий») 320 px dagi kartadan chiqib ketmasin
          'mt-1 w-full break-words text-footnote leading-tight hyphens-auto',
          active ? 'text-fg-on-accent' : 'text-fg-secondary',
        )}
      >
        {label}
      </span>
    </button>
  )
}

/**
 * Ro'yxat qatori: yuz, ism, lavozim · bo'lim, kelgan vaqti (yoki «—») va
 * holat. Qo'ng'iroq tugmasi qator havolasidan TASHQARIDA — ichma-ich
 * bosiladigan element telefonda noto'g'ri tegishga olib keladi.
 */
function MemberRow({ item, date }: { item: TeamBoardItemDto; date: string | null }) {
  const { t } = useTranslation()
  const format = useFormat()
  const photo = useTeamPhoto(item.employeeId, item.hasPhoto)
  const call = phoneHref(item.phone)

  const came = item.arrivedAt !== null
  const late = item.status === 'Late' && item.lateMinutes !== null && item.lateMinutes > 0
  // Kelganlar (vaqtida, jadvalsiz) — holat yozuvisiz; qolganlari so'z bilan
  const showStatus = item.status !== 'OnTime' && item.status !== 'Arrived' && item.status !== 'Late'
  const subtitle = [item.positionName, item.departmentName].filter((part) => Boolean(part)).join(' · ')

  // O'tgan kun paneli — a'zo ekrani o'sha kunni ochadi
  const to = `/jamoa/${encodeURIComponent(item.employeeId)}${date ? `?oy=${date.slice(0, 7)}&kun=${date}` : ''}`

  return (
    <div className="flex items-center border-t border-border first:border-t-0">
      <Link to={to} className="flex min-h-[64px] min-w-0 flex-1 items-center py-2 active:bg-bg-pressed">
        <Avatar photoUrl={photo} fullName={item.fullName} size="md" className="shrink-0" />

        <div className="ml-3 min-w-0 flex-1">
          <p className="text-body text-fg server-text">{item.fullName}</p>

          {/* Holat — ikkinchi qatorda, lavozimdan oldin: o'ng ustun faqat vaqt uchun.
              320 px ekranda chip o'ng ustunda turganda ism ikki-uch bo'lakka sinardi */}
          {late || showStatus || subtitle ? (
            <div className="mt-0.5 flex min-w-0 items-center">
              {late ? (
                <span className="mr-2 inline-flex shrink-0 items-center rounded-chip bg-tint-late px-2 py-0.5 text-caption1 text-fg">
                  <span className="mr-1.5 h-2 w-2 shrink-0 rounded-pill bg-hue-late" aria-hidden="true" />
                  <span className="sr-only">{format.late(item.lateMinutes ?? 0)}</span>
                  <span className="nums" aria-hidden="true">
                    {format.duration(item.lateMinutes)}
                  </span>
                </span>
              ) : showStatus ? (
                <span className="mr-2 flex min-w-0 max-w-full shrink-0">
                  <StatusChip look={TEAM_STATUS_LOOK[item.status]} size="sm" />
                </span>
              ) : null}
              {subtitle ? (
                <span className="min-w-0 flex-1 truncate text-footnote text-fg-secondary server-text">
                  {subtitle}
                </span>
              ) : null}
            </div>
          ) : null}
        </div>

        <span className={clsx('nums ml-2 shrink-0 text-subhead', came ? 'text-fg' : 'text-fg-tertiary')}>
          {came ? format.time(item.arrivedAt) : '—'}
        </span>
      </Link>

      {call ? (
        <a
          href={call}
          aria-label={t('team.callAria', { name: item.fullName })}
          className="ml-1 flex h-11 w-11 shrink-0 items-center justify-center rounded-pill text-accent-text active:bg-bg-pressed"
        >
          <Icon name="phone" className="h-5 w-5" />
        </a>
      ) : (
        // Raqami yo'q odamda bo'sh joy — vaqt ustuni qatorlar bo'ylab bir chiziqda tursin
        <span className="ml-1 h-11 w-11 shrink-0" aria-hidden="true" />
      )}
    </div>
  )
}

function MemberRowSkeleton() {
  return (
    <div className="flex min-h-[64px] items-center border-t border-border py-2 first:border-t-0">
      <Skeleton className="h-10 w-10 shrink-0 rounded-pill" />
      <div className="ml-3 flex-1 space-y-2">
        <Skeleton className="h-4 w-40" />
        <Skeleton className="h-3 w-28" />
      </div>
      <Skeleton className="ml-2 h-4 w-12" />
      <span className="ml-1 h-11 w-11 shrink-0" />
    </div>
  )
}

function TilesSkeleton() {
  return (
    <div className="grid grid-cols-3 gap-2">
      {[0, 1, 2, 3, 4, 5].map((index) => (
        <Skeleton key={index} className="h-[72px] rounded-card" />
      ))}
    </div>
  )
}

function BoardSkeleton() {
  return (
    <>
      <Card>
        <Skeleton className="mx-auto h-11 w-48" />
      </Card>
      <TilesSkeleton />
      <Card>
        <MemberRowSkeleton />
        <MemberRowSkeleton />
        <MemberRowSkeleton />
      </Card>
    </>
  )
}

/**
 * Ro'yxat o'rnidagi xato. Bo'lim qamrovdan chiqib ketgan bo'lsa
 * (`jamoa.bolim_topilmadi` — eski havola yoki HR tuzilmani o'zgartirgan)
 * — «qayta urinish» emas, butun jamoaga qaytish.
 */
function BoardError({
  error,
  onRetry,
  onAllDepartments,
  onToday,
}: {
  error: ApiError
  onRetry: () => void
  onAllDepartments: () => void
  onToday: () => void
}) {
  const { t } = useTranslation()
  const errorText = useErrorText()

  if (error.kind === 'network' || error.kind === 'server') return <ErrorState onRetry={onRetry} />

  const text = errorText(error)

  // Tezlik chegarasi (429, daqiqasiga ~30 so'rov) — nosozlik emas: «N soniyadan
  // keyin» matni va qayta urinish. Avtomatik yangilash o'z maromida davom etadi.
  if (error.code === 'rate_limit.oshib_ketdi') {
    return (
      <InlineMessage
        tone="warning"
        title={text.title ?? undefined}
        action={
          <Button variant="secondary" size="compact" onClick={onRetry}>
            {t('common.retry')}
          </Button>
        }
      >
        {text.body}
      </InlineMessage>
    )
  }

  // Sana serverning chegarasidan tashqarida (2000-yildan oldin yoki telefon
  // soati oldinda — server uchun «ertaga»): qayta urinish befoyda, bugunga qaytiladi
  if (error.code === 'jamoa.sana_notogri') {
    return (
      <InlineMessage
        tone="warning"
        title={text.title ?? undefined}
        action={
          <Button variant="secondary" size="compact" onClick={onToday}>
            {t('team.today')}
          </Button>
        }
      >
        {text.body}
      </InlineMessage>
    )
  }

  if (error.code === 'jamoa.bolim_topilmadi') {
    return (
      <InlineMessage
        tone="warning"
        title={text.title ?? undefined}
        action={
          <Button variant="secondary" size="compact" onClick={onAllDepartments}>
            {t('team.allDepartments')}
          </Button>
        }
      >
        {text.body}
      </InlineMessage>
    )
  }

  return (
    <InlineMessage
      tone="danger"
      title={text.title ?? undefined}
      action={
        <Button variant="secondary" size="compact" onClick={onRetry}>
          {t('common.retry')}
        </Button>
      }
    >
      {text.body}
    </InlineMessage>
  )
}

/**
 * Sana varag'i — eski kunlar uchun; kelajak sanani tanlab bo'lmaydi (`max`).
 * Native tanlagich `max` ni har doim ham ushlamaydi (qo'lda yozilgan sana),
 * shu sababli bugundan keyingisi baribir bugunga tushadi (`goDate`).
 */
function DateSheet({
  open,
  value,
  max,
  onPick,
  onClose,
}: {
  open: boolean
  value: string
  max: string
  onPick: (date: string | null) => void
  onClose: () => void
}) {
  const { t } = useTranslation()
  const [draft, setDraft] = useState(value)

  useEffect(() => {
    if (open) setDraft(value)
  }, [open, value])

  return (
    <Sheet open={open} title={t('team.pickDate')} onClose={onClose}>
      <input
        type="date"
        value={draft}
        max={max}
        onChange={(event: ChangeEvent<HTMLInputElement>) => setDraft(event.target.value)}
        className="h-12 w-full rounded-control border border-border-strong bg-bg-elevated px-3 text-callout text-fg"
      />

      <div className="mt-4 space-y-2">
        <Button block onClick={() => onPick(parseIsoDate(draft) ? draft : null)}>
          {t('common.confirm')}
        </Button>
        <Button variant="plain" block onClick={() => onPick(null)}>
          {t('team.today')}
        </Button>
        <Button variant="plain" block onClick={onClose}>
          {t('common.cancel')}
        </Button>
      </div>
    </Sheet>
  )
}

/** Chekinish darajalari — sinflar SANOQLI: runtime'da yasalgan nom tozalagichdan o'tmaydi. */
const DEPTH_PADDING = ['pl-1', 'pl-5', 'pl-9', 'pl-12']

/** Bo'lim varag'i: «Hammasi» birinchi, keyin qamrov daraxt tartibida, darajasiga qarab chekinib. */
function DepartmentSheet({
  open,
  options,
  value,
  onPick,
  onClose,
}: {
  open: boolean
  options: readonly TeamDepartmentOption[]
  value: string | null
  onPick: (id: string | null) => void
  onClose: () => void
}) {
  const { t } = useTranslation()

  const row = (id: string | null, name: string, depth: number) => (
    <button
      key={id ?? 'hammasi'}
      type="button"
      onClick={() => onPick(id)}
      className={clsx(
        'flex min-h-[52px] w-full items-center border-t border-border py-2 pr-1 text-left first:border-t-0 active:bg-bg-pressed',
        DEPTH_PADDING[Math.min(depth, DEPTH_PADDING.length - 1)],
      )}
    >
      <span className="min-w-0 flex-1 text-body text-fg server-text">{name}</span>
      {id === value ? <Icon name="check-circle" className="ml-2 h-5 w-5 text-accent-text" /> : null}
    </button>
  )

  return (
    <Sheet open={open} title={t('team.departmentSheet')} onClose={onClose}>
      <div className="overflow-hidden rounded-card bg-bg-elevated">
        {row(null, t('team.allDepartments'), 0)}
        {options.map((option) => row(option.id, option.name, option.depth))}
      </div>
    </Sheet>
  )
}
