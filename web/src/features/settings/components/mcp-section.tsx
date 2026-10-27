import { Link } from '@tanstack/react-router'
import { Ban, Check, Copy, History, LoaderCircle, RefreshCw, Sparkles, Users } from 'lucide-react'
import { useEffect, useId, useMemo, useRef, useState } from 'react'

import { useAuth } from '@/features/auth'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  Skeleton,
  Switch,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import { SETTINGS_MANAGE } from '../api/settings-api'
import {
  MCP_AUDIT_TAKE,
  MCP_TOOL_NAMES,
  type McpAuditEntry,
  type McpConnection,
  type McpStatus,
} from '../api/mcp-api'
import {
  useCheckMcp,
  useMcpAudit,
  useMcpConnections,
  useMcpStatus,
  useSetMcpEnabled,
} from '../api/mcp-queries'
import { McpRevokeDialog } from './mcp-revoke-dialog'

/**
 * «Rollar» sahifasining ruxsati — `nav-items.ts` dagi band bilan bir xil.
 *
 * `features/roles` dan import qilinmaydi: bo'limlar bir-birining ichki
 * fayllariga tegmaydi, bu esa bitta satr.
 */
const ROLES_MANAGE = 'roles.manage'

/** «Nusxa olindi» belgisi qancha turadi — xodim ilovasi parolidagi bilan bir xil. */
const COPIED_MS = 2000

const connectionHelper = dataColumnHelper<McpConnection>()
const auditHelper = dataColumnHelper<McpAuditEntry>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const NO_CONNECTIONS: McpConnection[] = []
const NO_AUDIT: McpAuditEntry[] = []

const KNOWN_TOOLS: ReadonlySet<string> = new Set(MCP_TOOL_NAMES)

/** Uzilgan va muddati o'tgan ulanish KULRANG (API kalitlaridagi `muted` izohi). */
const muted = 'text-[var(--fg-tertiary)]'

/**
 * <b>AI ulanish (MCP)</b> — Claude, ChatGPT kabi AI yordamchilarga tizim
 * ma'lumotini FAQAT O'QISH uchun ochish (`docs/SPEC-mcp.md` §8).
 *
 * <b>Nega sozlamalar sahifasida.</b> Egasining qarori (2026-10-01, SPEC §0):
 * ulanish va ulangan hisoblar ro'yxati shu yerda boshqariladi. Ruxsat ham
 * sahifaniki bilan bir xil — `settings.manage`.
 *
 * <b>Nega uchta karta, bitta emas.</b> Holat va ulanish yo'riqnomasi —
 * sozlama; ulanishlar va jurnal — YOZUVLAR ro'yxati, har biri o'z yuklanishi
 * va xatosi bilan. Bitta kartada ular uzun, bo'linmagan devor bo'lardi.
 * Kartalar orasidagi oraliq sahifadagi bo'limlar orasidagidan TORROQ —
 * uchalasi bitta bo'lim ekani ko'zga tashlanib turadi.
 *
 * <b>Ruxsat.</b> Bo'lim ruxsatsiz odamga umuman CHIZILMAYDI — sabab
 * `ApiKeysSection` dagi bilan bir xil: bo'sh jadval «hech kim ulanmagan»
 * degan YOLG'ON xabar bo'lardi.
 */
export function McpSection() {
  const { t, num, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const canManage = hasPermission(SETTINGS_MANAGE)

  const [revoking, setRevoking] = useState<McpConnection | null>(null)

  const status = useMcpStatus(canManage)
  const connections = useMcpConnections(canManage)
  const audit = useMcpAudit(canManage)

  const connectionRows = connections.data ?? NO_CONNECTIONS
  const auditRows = audit.data ?? NO_AUDIT

  const connectionColumns = useMemo<Array<DataColumn<McpConnection>>>(
    () => [
      connectionHelper.accessor('userName', {
        header: t('settings.mcp.columnPerson'),
        /*
          Yagona moslashuvchan ustun — qolganlari qat'iy `width`. Qator BIR
          QATORDA (egasi 2026-10-22): ism kesilmaydi, ustun o'zi kengayadi.
        */
        meta: { minWidth: '200px' },
        cell: ({ row }) => {
          const item = row.original
          const inactive = item.status !== 'active'

          return (
            <span className="flex items-baseline gap-2">
              <span className={cn('font-medium', inactive && muted)}>{item.userName}</span>

              {/*
                Telefon kengligida AI ilovasi va oxirgi so'rov ustunlari
                yashiriladi — ular shu qatorga, ismdan keyin qo'shiladi.
              */}
              <span className="text-footnote text-[var(--fg-tertiary)] md:hidden">
                {item.clientName}
                {' · '}
                {item.lastUsedAt === null
                  ? t('settings.mcp.neverUsed')
                  : dateTime(item.lastUsedAt)}
              </span>
            </span>
          )
        },
      }),

      connectionHelper.accessor('clientName', {
        header: t('settings.mcp.columnClient'),
        // «Claude», «ChatGPT» — qisqa; uzunrog'ida ustun o'zi kengayadi (katak `nowrap`).
        meta: { width: '160px', hideOnMobile: true },
        cell: ({ row }) => {
          const item = row.original

          return (
            <span className={item.status === 'active' ? 'text-[var(--fg-secondary)]' : muted}>
              {item.clientName}
            </span>
          )
        },
      }),

      connectionHelper.accessor('connectedAt', {
        header: t('settings.mcp.columnConnected'),
        // «01.10.2026 14:30» — 16 belgi tabular ≈ 130px + to'ldirish = 170px.
        meta: { width: '170px', hideOnMobile: true },
        cell: ({ row }) => {
          const item = row.original

          return (
            <span className={cn('tabular', item.status === 'active' ? 'text-[var(--fg-secondary)]' : muted)}>
              {dateTime(item.connectedAt)}
            </span>
          )
        },
      }),

      connectionHelper.accessor('lastUsedAt', {
        header: t('settings.mcp.columnLastUsed'),
        /*
          Vaqt TOSHKENT mintaqasida (`formatDateTime`) — «qachon ishlatilgan»
          degan savolga egasi o'z soati bilan javob beradi.
        */
        meta: { width: '170px', hideOnMobile: true },
        cell: ({ row }) => {
          const item = row.original

          if (item.lastUsedAt === null) {
            return <span className={muted}>{t('settings.mcp.neverUsed')}</span>
          }

          return (
            <span className={cn('tabular', item.status === 'active' ? 'text-[var(--fg-secondary)]' : muted)}>
              {dateTime(item.lastUsedAt)}
            </span>
          )
        },
      }),

      connectionHelper.display({
        id: 'status',
        header: t('settings.mcp.columnStatus'),
        // «Muddati tugagan» (15 belgi) yorliqda ≈ 115px + to'ldirish = 150px.
        meta: { width: '150px' },
        cell: ({ row }) => {
          const item = row.original

          if (item.status === 'active') {
            return (
              <Badge tone="success" size="sm">
                {t('settings.mcp.statusActive')}
              </Badge>
            )
          }

          if (item.status === 'expired') {
            return (
              <Badge tone="neutral" size="sm">
                {t('settings.mcp.statusExpired')}
              </Badge>
            )
          }

          return (
            <Badge
              tone="neutral"
              size="sm"
              title={
                item.revokedAt === null
                  ? undefined
                  : t('settings.mcp.revokedAt', { date: dateTime(item.revokedAt) })
              }
            >
              {t('settings.mcp.statusRevoked')}
            </Badge>
          )
        },
      }),

      connectionHelper.display({
        id: 'actions',
        header: '',
        // Bitta ikonkali tugma (`icon-sm` = 32px) + to'ldirish, zaxira bilan 90px.
        meta: { align: 'end', width: '90px' },
        cell: ({ row }) => {
          const item = row.original

          /*
            Faqat KUCHDAGI ulanishda: uzilgan yoki muddati o'tganida tugma
            hech narsani o'zgartirmasdi (server baribir 204 qaytaradi) va
            «bosildimi?» degan savol qoldirardi.
          */
          if (item.status !== 'active') return null

          return (
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              title={t('settings.mcp.revoke')}
              aria-label={t('settings.mcp.revoke')}
              onClick={() => setRevoking(item)}
            >
              <Ban className="text-[var(--danger-text)]" />
            </Button>
          )
        },
      }),
    ],
    [t, dateTime],
  )

  const auditColumns = useMemo<Array<DataColumn<McpAuditEntry>>>(() => {
    /*
      Jurnalda vosita nomi xom keladi (`search_employees`). Tanish nom
      tarjima qilinadi, notanishi (backend yangi vosita qo'shgan) xom
      holida chiqadi — `useRoleLabels` dagi bilan bir xil sabab: yashirilgan
      yozuv «bunday so'rov bo'lmagan» degan yolg'on taassurot berardi.
    */
    const toolLabel = (tool: string) =>
      KNOWN_TOOLS.has(tool) ? t(`settings.mcp.tool.${tool}`) : tool

    return [
      auditHelper.accessor('at', {
        header: t('settings.mcp.columnTime'),
        meta: { width: '170px', hideOnMobile: true },
        cell: ({ row }) => (
          <span className="tabular text-[var(--fg-secondary)]">{dateTime(row.original.at)}</span>
        ),
      }),

      auditHelper.accessor('userName', {
        header: t('settings.mcp.columnPerson'),
        meta: { minWidth: '180px' },
        cell: ({ row }) => {
          const item = row.original

          return (
            <span className="flex items-baseline gap-2">
              <span className="font-medium">{item.userName}</span>

              {/* Telefonda: nima so'ralgani va qachon — shu qatorda, ismdan keyin. */}
              <span className="text-footnote text-[var(--fg-tertiary)] md:hidden">
                {toolLabel(item.tool)}
                {' · '}
                <span className="tabular">{dateTime(item.at)}</span>
              </span>
            </span>
          )
        },
      }),

      auditHelper.accessor('clientName', {
        header: t('settings.mcp.columnClient'),
        meta: { width: '140px', hideOnMobile: true },
        cell: ({ row }) => (
          <span className="text-[var(--fg-secondary)]">{row.original.clientName}</span>
        ),
      }),

      auditHelper.accessor('tool', {
        header: t('settings.mcp.columnTool'),
        // «Yetishmayotgan hujjatlar» — eng uzun yorliq, ≈ 190px + to'ldirish.
        meta: { width: '230px', hideOnMobile: true },
        cell: ({ row }) => {
          const tool = row.original.tool

          return (
            // Xom nom `title` da — backend loglari bilan solishtirish uchun.
            <span title={tool}>{toolLabel(tool)}</span>
          )
        },
      }),

      auditHelper.accessor('outcome', {
        header: t('settings.mcp.columnOutcome'),
        meta: { width: '130px' },
        cell: ({ row }) => {
          const outcome = row.original.outcome

          if (outcome === 'ok') {
            return (
              <Badge tone="success" size="sm">
                {t('settings.mcp.outcomeOk')}
              </Badge>
            )
          }

          if (outcome === 'denied') {
            return (
              <Badge tone="warning" size="sm">
                {t('settings.mcp.outcomeDenied')}
              </Badge>
            )
          }

          return (
            <Badge tone="danger" size="sm">
              {t('settings.mcp.outcomeError')}
            </Badge>
          )
        },
      }),

      auditHelper.accessor('rowCount', {
        header: t('settings.mcp.columnRows'),
        meta: { align: 'end', width: '110px', hideOnMobile: true },
        cell: ({ row }) => {
          const item = row.original

          return (
            <span className="tabular inline-flex items-baseline gap-2">
              <span className="text-[var(--fg-secondary)]">
                {item.rowCount === null ? '—' : num(item.rowCount)}
              </span>
              {/* Qancha vaqt olgani — ikkinchi darajali, sekin so'rovni topish uchun; yonida, bir qatorda. */}
              <span className="text-caption1 text-[var(--fg-tertiary)]">
                {t('settings.mcp.durationMs', { ms: num(item.durationMs) })}
              </span>
            </span>
          )
        },
      }),
    ]
  }, [t, num, dateTime])

  // Ruxsatsiz odamga bo'lim umuman ko'rinmaydi (yuqoridagi izoh).
  if (!canManage) return null

  return (
    <div className="space-y-3">
      <Card className="overflow-hidden">
        {/* Sarlavha sahifadagi boshqa guruhlar bilan BIR XIL (`Group`, `ApiKeysSection`). */}
        <div className="flex items-start gap-3 border-b border-[var(--border)] px-4 py-4 sm:px-5">
          <IconTile icon={Sparkles} tone="purple" />
          <div className="min-w-0">
            <h2 className="text-subhead font-semibold">{t('settings.mcp.title')}</h2>
            <p className="mt-0.5 text-caption1 text-[var(--fg-tertiary)]">{t('settings.mcp.hint')}</p>
          </div>
        </div>

        {status.isPending ? (
          <div className="space-y-3 px-4 py-4 sm:px-5">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
        ) : status.isError ? (
          <div className="px-4 py-4 sm:px-5">
            <Alert tone="danger" title={t('settings.mcp.statusLoadFailed')}>
              {errorMessage(problemCode(status.error))}
            </Alert>
          </div>
        ) : (
          <div className="divide-y divide-[var(--border)]">
            <StatusBlock status={status.data} />
            <ConnectBlock serverUrl={status.data.serverUrl} />
          </div>
        )}
      </Card>

      <Card className="overflow-hidden">
        <TableHeader
          title={t('settings.mcp.connectionsTitle')}
          hint={t('settings.mcp.connectionsHint')}
        />

        {connections.isError ? (
          <div className="px-4 py-4 sm:px-5">
            <Alert tone="danger" title={t('settings.mcp.connectionsLoadFailed')}>
              {errorMessage(problemCode(connections.error))}
            </Alert>
          </div>
        ) : (
          <DataTable
            columns={connectionColumns}
            data={connectionRows}
            getRowId={(item) => item.id}
            loading={connections.isPending}
            skeletonRows={3}
            empty={
              <EmptyState
                icon={Users}
                title={t('settings.mcp.connectionsEmptyTitle')}
                description={t('settings.mcp.connectionsEmptyText')}
              />
            }
          />
        )}
      </Card>

      <Card className="overflow-hidden">
        <TableHeader
          title={t('settings.mcp.auditTitle')}
          hint={t('settings.mcp.auditHint', { n: MCP_AUDIT_TAKE })}
        />

        {audit.isError ? (
          <div className="px-4 py-4 sm:px-5">
            <Alert tone="danger" title={t('settings.mcp.auditLoadFailed')}>
              {errorMessage(problemCode(audit.error))}
            </Alert>
          </div>
        ) : (
          <DataTable
            columns={auditColumns}
            data={auditRows}
            getRowId={(item) => String(item.id)}
            loading={audit.isPending}
            skeletonRows={3}
            empty={
              <EmptyState
                icon={History}
                title={t('settings.mcp.auditEmptyTitle')}
                description={t('settings.mcp.auditEmptyText')}
              />
            }
          />
        )}
      </Card>

      <McpRevokeDialog item={revoking} onClose={() => setRevoking(null)} />
    </div>
  )
}

/**
 * Jadval kartasining sarlavhasi — asosiy kartadagidan BIR POG'ONA past:
 * ikonasiz. Ikona bo'limning boshida bitta, qolgan kartalar uning davomi.
 */
function TableHeader({ title, hint }: { title: string; hint: string }) {
  return (
    <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
      <h3 className="text-subhead font-semibold">{title}</h3>
      <p className="mt-0.5 text-caption1 text-[var(--fg-tertiary)]">{hint}</p>
    </div>
  )
}

/**
 * Holat yorlig'i, uning izohi va o'chirgich.
 *
 * <b>O'chirgich nega ba'zan NOFAOL, lekin qiymatini ko'rsatib turadi.</b>
 * Server sozlanmagan yoki nosoz bo'lsa yoqish hech narsani o'zgartirmaydi —
 * ulanish baribir yopiq qoladi (fail-closed, SPEC §2.2). Lekin egasining
 * tanlovi SAQLANGAN va server tuzatilgach o'zi kuchga kiradi — shu sababli
 * u yashirilmaydi, faqat qulflanadi.
 */
function StatusBlock({ status }: { status: McpStatus }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const toggle = useSetMcpEnabled()
  const check = useCheckMcp()

  const locked = status.state === 'NotConfigured' || status.state === 'Faulty'

  /*
    So'rov ketayotganda o'chirgich BOSILGAN holatda turadi — javobni
    kutib orqaga sakramaydi (iOS'dagi kabi darhol javob). Xato bo'lsa
    `variables` bo'shaydi va saqlangan qiymat qaytadi.
  */
  const checked = toggle.isPending && toggle.variables !== undefined ? toggle.variables : status.enabled

  return (
    <>
      <div className="space-y-2 px-4 py-4 sm:px-5">
        <div className="flex flex-wrap items-center justify-between gap-x-3 gap-y-2">
          <div className="flex min-w-0 items-center gap-2">
            <span className="text-subhead font-medium">{t('settings.mcp.statusLabel')}</span>
            <StateBadge state={status.state} />
          </div>

          {/*
            «Qayta tekshirish» faqat NOSOZ holatda: sozlanmagan serverda u
            hech narsa bermaydi (sozlama ishga tushishda o'qiladi), yoqilgan
            yoki o'chirilganda esa tekshiradigan narsa yo'q.
          */}
          {status.state === 'Faulty' && (
            <Button
              type="button"
              size="sm"
              variant="secondary"
              onClick={() => check.mutate()}
              disabled={check.isPending}
            >
              {check.isPending ? <LoaderCircle className="animate-spin" /> : <RefreshCw />}
              {check.isPending ? t('settings.mcp.rechecking') : t('settings.mcp.recheck')}
            </Button>
          )}
        </div>

        <StateHint state={status.state} />

        {status.reason !== null && (
          // Sabab serverdan o'zbekcha keladi va sirsiz (backend izohi).
          <p className="text-footnote text-[var(--fg-tertiary)]">
            {t('settings.mcp.reason', { reason: status.reason })}
          </p>
        )}

        {check.isError && <Alert tone="danger">{errorMessage(problemCode(check.error))}</Alert>}
      </div>

      <div className="px-4 py-4 sm:px-5">
        <Switch
          checked={checked}
          disabled={locked || toggle.isPending}
          onChange={(next) => toggle.mutate(next)}
          label={t('settings.mcp.switchLabel')}
          hint={locked ? t('settings.mcp.switchLockedHint') : t('settings.mcp.switchHint')}
        />

        {toggle.isError && (
          <div className="mt-2">
            <Alert tone="danger">{errorMessage(problemCode(toggle.error))}</Alert>
          </div>
        )}
      </div>
    </>
  )
}

/**
 * Holat yorlig'i. Kalitlar har tarmoqda ALOHIDA yozilgan (shablon emas) —
 * `check-locales.mjs` ularni shunday ko'radi va tarjimasi unutilsa yiqiladi.
 */
function StateBadge({ state }: { state: McpStatus['state'] }) {
  const { t } = useLocale()

  switch (state) {
    case 'On':
      return <Badge tone="success" dot>{t('settings.mcp.stateOn')}</Badge>
    case 'Off':
      return <Badge tone="neutral" dot>{t('settings.mcp.stateOff')}</Badge>
    case 'NotConfigured':
      return <Badge tone="warning" dot>{t('settings.mcp.stateNotConfigured')}</Badge>
    case 'Faulty':
      return <Badge tone="danger" dot>{t('settings.mcp.stateFaulty')}</Badge>
    default:
      return <Badge tone="neutral" dot>{t('settings.mcp.statePending')}</Badge>
  }
}

/** Holatning bir jumlalik izohi — «bu nimani anglatadi va nima qilish kerak». */
function StateHint({ state }: { state: McpStatus['state'] }) {
  const { t } = useLocale()

  const text = (() => {
    switch (state) {
      case 'On':
        return t('settings.mcp.stateOnHint')
      case 'Off':
        return t('settings.mcp.stateOffHint')
      case 'NotConfigured':
        return t('settings.mcp.stateNotConfiguredHint')
      case 'Faulty':
        return t('settings.mcp.stateFaultyHint')
      default:
        return t('settings.mcp.statePendingHint')
    }
  })()

  return <p className="text-footnote text-[var(--fg-secondary)]">{text}</p>
}

/**
 * Ulanish manzili, uch qadamli yo'riqnoma va kim ulana olishi.
 *
 * Manzil holatdan qat'i nazar KO'RSATILADI: egasi uni oldindan rahbarlarga
 * yuborib qo'yishi mumkin, server esa yoqilgandan keyin javob beradi.
 */
function ConnectBlock({ serverUrl }: { serverUrl: string }) {
  const { t } = useLocale()
  const { hasPermission } = useAuth()

  const steps = [t('settings.mcp.step1'), t('settings.mcp.step2'), t('settings.mcp.step3')]

  return (
    <div className="space-y-4 px-4 py-4 sm:px-5">
      <UrlField url={serverUrl} />

      <div className="space-y-2.5">
        <p className="text-footnote font-medium text-[var(--fg-secondary)]">
          {t('settings.mcp.howTitle')}
        </p>

        <ol className="space-y-2">
          {steps.map((text, index) => (
            <li key={index} className="flex items-start gap-3">
              <span
                aria-hidden
                className="tabular grid size-6 shrink-0 place-items-center rounded-full bg-[var(--accent-soft)] text-caption1 font-semibold text-[var(--accent-on-soft)]"
              >
                {index + 1}
              </span>
              <span className="pt-0.5 text-subhead">{text}</span>
            </li>
          ))}
        </ol>
      </div>

      {/*
        Kim ulana oladi — ruxsat nomi «Rollar» muharriridagi yorliqning
        O'ZI (`roles.extra.mcp.connect`): ikki joyda ikki xil nom bo'lsa,
        egasi uni ro'yxatda topa olmasdi. Havola faqat «Rollar» sahifasiga
        kira oladigan odamga.
      */}
      <p className="text-footnote text-[var(--fg-secondary)]">
        {t('settings.mcp.who', { permission: t('roles.extra.mcp.connect') })}
        {hasPermission(ROLES_MANAGE) && (
          <>
            {' '}
            <Link
              to="/rollar"
              className="font-medium text-[var(--accent-text)] underline-offset-2 hover:underline"
            >
              {t('settings.mcp.whoLink')}
            </Link>
          </>
        )}
      </p>
    </div>
  )
}

/**
 * Faqat o'qiladigan manzil va nusxa olish tugmasi.
 *
 * `navigator.clipboard` faqat xavfsiz kontekstda ishlaydi — tizim oddiy
 * HTTP da ochilsa nusxa olinmaydi; u holda matn o'zi BELGILANADI va egasi
 * uni Ctrl+C bilan oladi (`employee-app-access-tab.tsx` dagi bilan bir xil).
 */
function UrlField({ url }: { url: string }) {
  const { t } = useLocale()
  const id = useId()
  const inputRef = useRef<HTMLInputElement>(null)
  const [copy, setCopy] = useState<'idle' | 'done' | 'failed'>('idle')

  useEffect(() => {
    if (copy !== 'done') return
    const timer = window.setTimeout(() => setCopy('idle'), COPIED_MS)
    return () => window.clearTimeout(timer)
  }, [copy])

  const onCopy = async () => {
    try {
      if (navigator.clipboard === undefined) throw new Error('clipboard')
      await navigator.clipboard.writeText(url)
      setCopy('done')
    } catch {
      inputRef.current?.focus()
      inputRef.current?.select()
      setCopy('failed')
    }
  }

  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-footnote font-medium text-[var(--fg-secondary)]">
        {t('settings.mcp.urlLabel')}
      </label>

      <div className="flex items-center gap-2">
        <input
          id={id}
          ref={inputRef}
          readOnly
          value={url}
          spellCheck={false}
          onFocus={(event) => event.currentTarget.select()}
          className={cn(
            'tabular h-10 w-full min-w-0 rounded-md border px-3 text-footnote',
            'border-[var(--border)] bg-[var(--bg-sunken)] text-[var(--fg)]',
            'focus:border-[var(--accent)] focus:ring-4 focus:ring-[var(--accent)]/10 focus:outline-none',
          )}
        />

        <Button type="button" variant="secondary" onClick={() => void onCopy()} className="shrink-0">
          {copy === 'done' ? <Check className="text-ios-green" /> : <Copy />}
          <span className="hidden sm:inline">
            {copy === 'done' ? t('settings.mcp.copied') : t('settings.mcp.copy')}
          </span>
          <span className="sr-only sm:hidden">{t('settings.mcp.copy')}</span>
        </Button>
      </div>

      {copy === 'failed' && (
        <p role="alert" className="text-footnote text-ios-orange">
          {t('settings.mcp.copyFailed')}
        </p>
      )}
    </div>
  )
}
