import { Ban, KeyRound, Plug } from 'lucide-react'
import { useMemo, useState } from 'react'

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
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import { SETTINGS_MANAGE } from '../api/settings-api'
import type { ApiKeySummary } from '../api/api-keys-api'
import { useApiKeys } from '../api/api-keys-queries'
import { ApiKeyCreateDialog } from './api-key-create-dialog'
import { ApiKeyRevokeDialog } from './api-key-revoke-dialog'

const helper = dataColumnHelper<ApiKeySummary>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: ApiKeySummary[] = []

/**
 * Bekor qilingan kalit KULRANG bo'lib ro'yxatda qoladi (SPEC §6).
 *
 * Nega qator emas, KATAKLAR bo'yaladi: `DataTable` qator uchun sinf
 * qabul qilmaydi va uni shu bo'lim uchun o'zgartirish umumiy komponentga
 * faqat bitta joyda kerak bo'lgan maydon qo'shardi. Yozuvning O'ZI
 * o'chirilmaydi, chunki «bu kalit kimga berilgan edi va qachon bekor
 * qilingan» degan savol xavfsizlik tekshiruvida birinchi so'raladi.
 */
const muted = 'text-[var(--fg-tertiary)]'

/**
 * Kalitning ekrandagi ko'rinishi: «bonvi_ab12cd34_…».
 *
 * Serverdan faqat sakkiz belgili OCHIQ prefiks keladi; qolgan qismi
 * (sir) hech qachon kelmaydi. To'liq formatga o'xshatib chizish ataylab:
 * tashqi loyihada turgan kalit bilan solishtirganda odam aynan shu
 * ko'rinishni ko'radi va qaysi qator qaysi kalit ekanini adashmaydi.
 */
function keyLabel(prefix: string): string {
  return `bonvi_${prefix}_…`
}

/**
 * <b>Tashqi API</b> — holdingning boshqa loyihalari uchun kalitlar
 * (SPEC `docs/SPEC-tashqi-api.md` §6).
 *
 * <b>Nega sozlamalar sahifasining BO'LIMI, alohida sahifa emas.</b> Kalit
 * berish — tizim sozlamasi va u aynan shu sahifadagi boshqa qiymatlar
 * bilan bir xil savolga tegishli: «tizimning o'zi qanday ishlaydi».
 * Ruxsati ham bir xil (`settings.manage`), ya'ni menyuga yangi band
 * qo'shish bitta odam uchun bitta bandni ikkiga bo'lardi.
 *
 * <b>Nega umumiy «Saqlash» tugmasidan MUSTAQIL.</b> Sahifaning qolgan
 * qismi — forma: qiymatlar yig'iladi va birga yuboriladi. Kalit esa
 * YOZUV: yaratilishi va bekor qilinishi darhol bajariladi va ikkalasi
 * ham o'z oynasi bilan tasdiqlanadi (logotip maydonidagi bilan bir xil
 * fikr).
 *
 * <b>Ruxsat.</b> Bo'lim ruxsatsiz odamga umuman CHIZILMAYDI: `GET
 * /api/api-keys` ning o'zi ham `settings.manage` bilan himoyalangan,
 * ya'ni ro'yxatni serverdan olishning yo'li yo'q — bo'sh jadval
 * ko'rsatish esa «kalit yo'q» degan YOLG'ON xabar bo'lardi.
 */
export function ApiKeysSection() {
  const { t, date, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission } = useAuth()

  const canManage = hasPermission(SETTINGS_MANAGE)

  const [creating, setCreating] = useState(false)
  const [revoking, setRevoking] = useState<ApiKeySummary | null>(null)

  const list = useApiKeys(canManage)
  const rows = list.data ?? EMPTY

  const columns = useMemo<Array<DataColumn<ApiKeySummary>>>(
    () => [
      helper.accessor('name', {
        header: t('settings.apiKeys.columnName'),
        /*
          Yagona moslashuvchan ustun (qolganlari qat'iy `width`). Nom va
          yorliq BIR QATORDA (egasi 2026-10-22 — jadval qatori ikkiga
          bo'linmaydi); uzun nomda ustun o'zi kengayadi (katak `nowrap`).
        */
        meta: { minWidth: '220px' },
        cell: ({ row }) => {
          const item = row.original

          return (
            <div className="flex items-center gap-2">
              <span className={cn('font-medium', !item.isActive && muted)}>{item.name}</span>
              {/* Kengroq doira ko'zga tashlansin — u tizimdagi deyarli hamma narsani o'qiydi. */}
              {item.scope === 'FullRead' && (
                <Badge tone="warning" size="sm" className="shrink-0">
                  {t('settings.apiKeys.badgeFull')}
                </Badge>
              )}

              {/*
                Telefon kengligida prefiks ustuni yashiriladi — u shu
                qatorga, nomdan keyin qo'shiladi. Kalitni tanib olishning
                yagona yo'li shu prefiks, ya'ni tor ekranda ham ko'rinishi kerak.
              */}
              <span className="tabular text-footnote text-[var(--fg-tertiary)] md:hidden">
                {keyLabel(item.prefix)}
              </span>
            </div>
          )
        },
      }),

      helper.accessor('prefix', {
        header: t('settings.apiKeys.columnPrefix'),
        // «bonvi_ab12cd34_…» — 17 belgi, tabular ≈ 145px + katak
        // to'ldirishi (32px) ≈ 177px, kichik zaxira bilan 190px.
        meta: { width: '190px', hideOnMobile: true },
        cell: ({ row }) => {
          const item = row.original

          return (
            <span className={cn('tabular', item.isActive ? 'text-[var(--fg-secondary)]' : muted)}>
              {keyLabel(item.prefix)}
            </span>
          )
        },
      }),

      helper.accessor('createdAt', {
        header: t('settings.apiKeys.columnCreated'),
        // «18.09.2026» — 10 belgi, 140px kifoya.
        meta: { width: '140px', hideOnMobile: true },
        cell: ({ row }) => {
          const item = row.original

          return (
            <span className={cn('tabular', item.isActive ? 'text-[var(--fg-secondary)]' : muted)}>
              {date(item.createdAt)}
            </span>
          )
        },
      }),

      helper.accessor('lastUsedAt', {
        header: t('settings.apiKeys.columnLastUsed'),
        /*
          «18.09.2026 14:30» va yonida IP — bir qatorda; 180px pastki
          chegara, IP bilan ustun o'zi kengayadi. Vaqt TOSHKENT mintaqasida
          (`formatDateTime`), brauzernikida emas: xavfsizlik tekshiruvida
          «kalit qachon ishlatilgan» degan savolga HR o'zining soati bilan
          javob beradi.
        */
        meta: { width: '180px', hideOnMobile: true },
        cell: ({ row }) => {
          const item = row.original

          if (item.lastUsedAt === null) {
            return (
              <span className="text-[var(--fg-tertiary)]">
                {t('settings.apiKeys.neverUsed')}
              </span>
            )
          }

          return (
            <span className="flex items-baseline gap-2">
              <span className={cn('tabular', item.isActive ? 'text-[var(--fg-secondary)]' : muted)}>
                {dateTime(item.lastUsedAt)}
              </span>

              {/*
                So'rov kelgan IP — kalit sizib chiqqanda birinchi
                so'raladigan ma'lumot. Ikkinchi darajali, shu sababli
                kichikroq va xiraroq.
              */}
              {item.lastUsedIp !== null && (
                <span className="tabular text-caption1 text-[var(--fg-tertiary)]">{item.lastUsedIp}</span>
              )}
            </span>
          )
        },
      }),

      helper.display({
        id: 'status',
        header: t('settings.apiKeys.columnStatus'),
        // «Bekor qilingan» (13 belgi) yorliqda ≈ 105px + to'ldirish = 150px.
        meta: { width: '150px' },
        cell: ({ row }) => {
          const item = row.original

          return item.isActive ? (
            <Badge tone="success" size="sm">
              {t('settings.apiKeys.active')}
            </Badge>
          ) : (
            <Badge
              tone="neutral"
              size="sm"
              title={
                item.revokedAt === null
                  ? undefined
                  : t('settings.apiKeys.revokedAt', { date: dateTime(item.revokedAt) })
              }
            >
              {t('settings.apiKeys.revoked')}
            </Badge>
          )
        },
      }),

      helper.display({
        id: 'actions',
        header: '',
        // Bitta ikonkali tugma (`icon-sm` = 32px) + katak to'ldirishi
        // (32px) = 64px, zaxira bilan 90px.
        meta: { align: 'end', width: '90px' },
        cell: ({ row }) => {
          const item = row.original

          /*
            Bekor qilingan kalitda tugma UMUMAN chizilmaydi: amal
            idempotent (server 200 qaytaradi), lekin tugmani ko'rsatib
            hech narsa o'zgartirmaslik foydalanuvchini «bosildimi?»
            degan savol bilan qoldirardi.
          */
          if (!item.isActive) return null

          return (
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              title={t('settings.apiKeys.revoke')}
              aria-label={t('settings.apiKeys.revoke')}
              onClick={() => setRevoking(item)}
            >
              <Ban className="text-[var(--danger-text)]" />
            </Button>
          )
        },
      }),
    ],
    [t, date, dateTime],
  )

  // Ruxsatsiz odamga bo'lim umuman ko'rinmaydi (yuqoridagi izoh).
  if (!canManage) return null

  const create = (
    <Button variant="create" onClick={() => setCreating(true)}>
      <KeyRound />
      {t('settings.apiKeys.create')}
    </Button>
  )

  return (
    <>
      <Card className="overflow-hidden">
        {/*
          Sarlavha sahifadagi boshqa guruhlar bilan BIR XIL chiziladi
          (`Group`): rangli ikona, sarlavha va bir jumlalik izoh. Farqi
          faqat o'ng chetdagi harakat tugmasi — `Group` uni qabul
          qilmaydi, chunki qolgan guruhlarda bitta ham harakat yo'q.

          Tor ekranda sarlavha va tugma USTMA-UST turadi: bitta qatorda
          qolganda tugma izohni bir so'zlik ustunga qisib qo'yardi
          (telefon kengligida tekshirilgan).
        */}
        <div className="flex flex-col gap-3 border-b border-[var(--border)] px-4 py-4 sm:flex-row sm:items-start sm:justify-between sm:gap-4 sm:px-5">
          <div className="flex min-w-0 items-start gap-3">
            <IconTile icon={Plug} tone="teal" />
            <div className="min-w-0">
              <h2 className="text-subhead font-semibold">{t('settings.apiKeys.title')}</h2>
              <p className="mt-0.5 text-caption1 text-[var(--fg-tertiary)]">
                {t('settings.apiKeys.hint')}
              </p>
            </div>
          </div>

          {/* Bo'sh ro'yxatda tugma faqat bo'sh holat ichida turadi —
              ikkitasi bir vaqtda ko'rinmaydi. */}
          {rows.length > 0 && <div className="shrink-0">{create}</div>}
        </div>

        {list.isError ? (
          <div className="px-4 py-4 sm:px-5">
            <Alert tone="danger" title={t('settings.apiKeys.loadFailed')}>
              {errorMessage(problemCode(list.error))}
            </Alert>
          </div>
        ) : (
          <DataTable
            columns={columns}
            data={rows}
            getRowId={(item) => item.id}
            loading={list.isPending}
            skeletonRows={3}
            empty={
              <EmptyState
                icon={Plug}
                title={t('settings.apiKeys.emptyTitle')}
                description={t('settings.apiKeys.emptyText')}
                action={create}
              />
            }
          />
        )}
      </Card>

      {/*
        Oyna SHARTLI chiziladi — yopilganda komponent butunlay yo'q
        qilinadi. To'liq kalit uning lokal holatida turadi va boshqa hech
        qayerda yo'q (`api-key-create-dialog.tsx` dagi «uchta qulf»).
      */}
      {creating && <ApiKeyCreateDialog onClose={() => setCreating(false)} />}

      <ApiKeyRevokeDialog item={revoking} onClose={() => setRevoking(null)} />
    </>
  )
}
