import { KeyRound, Lock, UserCheck, UserCog, UserPlus, UserX, Users } from 'lucide-react'
import { useMemo, useState } from 'react'

import { formatFullPhone, useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Avatar,
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  PageHeader,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import type { UserSummary } from '../api/users-api'
import { useActivateUser, useUsers } from '../api/users-queries'
import { userErrorCode } from '../lib/user-error'
import { ADMIN_ROLE_CODE, USERS_MANAGE } from '../lib/user-presentation'
import { DeactivateUserDialog } from './deactivate-user-dialog'
import { ResetPasswordDialog } from './reset-password-dialog'
import { UserFormDialog } from './user-form-dialog'

const helper = dataColumnHelper<UserSummary>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: UserSummary[] = []

/**
 * Foydalanuvchilarni boshqarish — faqat administrator.
 *
 * <b>Nega ro'yxatda qidiruv, filtr va sahifalash yo'q.</b> Tizimdan
 * atigi bir necha kishi (bir necha HR menejeri va bosh rahbar)
 * foydalanadi — bu ekran ularning HAMMASINI bitta qarashda ko'rsatadi.
 *
 * <b>Nega butun sahifa `users.manage` bilan o'ralgan.</b> `GET /api/users`
 * ning o'zi ham shu ruxsat bilan himoyalangan (`UserEndpoints.cs`): HR
 * menejeri bu ro'yxatni SERVERDAN OLA OLMAYDI, ya'ni bu yerda faqat
 * amallarni yashirish yetarli emas — butun kontent ruxsatga bog'liq.
 */
export function UsersPage() {
  const { t, num, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission, user: currentUser } = useAuth()

  const canManage = hasPermission(USERS_MANAGE)

  const [editing, setEditing] = useState<UserSummary | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [deactivating, setDeactivating] = useState<UserSummary | null>(null)
  const [resettingPassword, setResettingPassword] = useState<UserSummary | null>(null)

  const list = useUsers()
  const rows = list.data ?? EMPTY

  /*
   * Faollashtirish TASDIQSIZ, darhol bajariladi — backend hujjatida ham
   * shunday izohlangan (`ActivateUserCommandHandler`): bu amal hech kimni
   * bloklamaydi, faqat kirishni qaytaradi. Xavfli amal — faqat
   * faolsizlantirish va parol tiklash (topshiriqning o'zi shunday ajratadi).
   */
  const activate = useActivateUser()

  /**
   * Tizimdagi faol administratorlar soni.
   *
   * NEGA klientda hisoblanadi: butun ro'yxat allaqachon xotirada, server
   * bilan yana bir so'rov almashish shart emas. Faqat OXIRGI faol
   * administratorni himoya qilish uchun kerak — pastdagi `canDeactivate`ga
   * qarang. Backend baribir o'zi ham tekshiradi (`foydalanuvchi.
   * oxirgi_administrator`), bu yerdagi hisob faqat tugmani oldindan
   * o'chirib, foydasiz so'rov-javobning oldini oladi.
   */
  const activeAdminCount = useMemo(
    () => rows.filter((row) => row.isActive && row.roleCodes.includes(ADMIN_ROLE_CODE)).length,
    [rows],
  )

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }

  const openEdit = (row: UserSummary) => {
    setEditing(row)
    setFormOpen(true)
  }

  const columns = useMemo<Array<DataColumn<UserSummary>>>(
    () => [
      helper.accessor('fullName', {
        header: t('users.columnUser'),
        /*
          Yagona moslashuvchan ustun (`minWidth`, boshqalari qat'iy `width`).
          Xrom: avatar (size="lg" = 48px) + oraliq (gap-3 = 12px) + katak
          to'ldirishi (px-4 × 2 = 32px) = 92px. Qolgan 168px matnga: F.I.Sh.
          bitta qatorda (`truncate-1`, uzuni kesiladi — to'lig'i `title`
          atributida), ostida telefon («+998 90 508 88 88», 17 belgi,
          text-footnote 13px) — bemalol sig'adi. 260px — flags/xodimlar
          ro'yxatidagi xuddi shu konstruksiya (rasm+ism, 300px) dan ozroq:
          u yerda rasm HAQIQIY va kattaroq maydon talab qiladi, bu yerda
          esa bosh harflar doim ikki belgi.
        */
        meta: { minWidth: '260px' },
        cell: ({ row }) => {
          const item = row.original
          return (
            <div className="flex min-w-0 items-center gap-3">
              <Avatar name={item.fullName} size="lg" />
              <div className="min-w-0">
                <p className="truncate-1 font-medium" title={item.fullName}>
                  {item.fullName}
                </p>
                <p className="tabular truncate-1 text-footnote text-[var(--fg-secondary)]">
                  {formatFullPhone(item.login)}
                </p>
              </div>
            </div>
          )
        },
      }),

      helper.display({
        id: 'role',
        header: t('users.columnRole'),
        /*
          190px − katak to'ldirishi (32px) = 158px matnga. Ikkita rol bor
          (`user-presentation.ts`): «HR menejeri» (11 belgi, ~94px 15px
          shriftda ~8.5px/belgi hisobida) TO'LIQ sig'adi. «Asosiy rahbar /
          Administrator» (30 belgi, ~255px) kesiladi — bu qabul qilingan:
          bu rolni FAQAT bitta odam (bosh rahbar) oladi, to'lig'i `title`
          atributida turadi. Ustunni shu yagona uzun qatorga moslab
          kengaytirish qolgan qatorlarning aksariyatida bo'sh joy
          qoldirardi.
        */
        meta: { width: '190px', hideOnMobile: true },
        cell: ({ row }) => {
          const labels = row.original.roleCodes.map((code) => roleLabel(t, code))
          if (labels.length === 0) return <span className="text-[var(--fg-tertiary)]">—</span>

          const joined = labels.join(', ')
          return (
            <span className="truncate-1 block text-[var(--fg-secondary)]" title={joined}>
              {joined}
            </span>
          )
        },
      }),

      helper.display({
        id: 'status',
        header: t('users.columnStatus'),
        // «Faolsiz» (7 belgi) + ixtiyoriy «Bloklangan» yorlig'i (10 belgi,
        // pastda, kichikroq) — ikkalasi ham 77px dan tor. 120px kifoya.
        meta: { width: '120px' },
        cell: ({ row }) => {
          const item = row.original
          const lockedUntil = item.lockedUntil
          const locked = lockedUntil !== null && new Date(lockedUntil).getTime() > Date.now()

          return (
            <div className="flex flex-col items-start gap-1">
              <Badge tone={item.isActive ? 'success' : 'neutral'} size="sm">
                {item.isActive ? t('users.active') : t('users.inactive')}
              </Badge>

              {locked && (
                <Badge
                  tone="warning"
                  size="sm"
                  title={t('users.lockedUntil', { date: dateTime(lockedUntil) })}
                >
                  <Lock className="size-2.5" strokeWidth={2.5} />
                  {t('users.lockedBadge')}
                </Badge>
              )}
            </div>
          )
        },
      }),

      helper.accessor('lastLoginAt', {
        header: t('users.columnLastLogin'),
        // «25.08.2026 14:30» — 17 belgi, tabular. 17 × 8.5px ≈ 145px +
        // katak to'ldirishi (32px) ≈ 177px, kichik zaxira bilan 180px.
        meta: { width: '180px', hideOnMobile: true },
        cell: ({ getValue }) => {
          const value = getValue()
          if (value === null) return <span className="text-[var(--fg-tertiary)]">—</span>
          return <span className="tabular text-[var(--fg-secondary)]">{dateTime(value)}</span>
        },
      }),

      helper.display({
        id: 'actions',
        header: '',
        /*
          Ikkita ikonkali tugma (`icon-sm` = 32px har biri) + oraliq
          (gap-1.5 = 6px) = 70px + katak to'ldirishi (32px) = 102px,
          kichik zaxira bilan 110px. Joriy foydalanuvchining o'z qatorida
          birinchi tugma (faolsizlantirish) umuman chizilmaydi — bo'sh
          joy qoladi, ustun kengligi o'zgarmaydi.
        */
        meta: { align: 'end', width: '110px' },
        cell: ({ row }) => {
          const item = row.original
          const isSelf = currentUser !== null && currentUser.id === item.id

          const isLastActiveAdmin =
            item.isActive && item.roleCodes.includes(ADMIN_ROLE_CODE) && activeAdminCount <= 1

          return (
            <span className="flex justify-end gap-1.5">
              {/*
                O'ZINI FAOLSIZLANTIRISH tugmasi shu qatorda umuman
                ko'rsatilmaydi — backend buni 403 bilan baribir to'xtatadi
                (`foydalanuvchi.ozini_blokash`), lekin tugmani ko'rsatib,
                keyin xato qaytarish yomon tajriba (topshiriq shunday
                talab qiladi).
              */}
              {!isSelf && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  title={
                    item.isActive
                      ? isLastActiveAdmin
                        ? errorMessage('foydalanuvchi.oxirgi_administrator')
                        : t('users.deactivate')
                      : t('users.activate')
                  }
                  aria-label={item.isActive ? t('users.deactivate') : t('users.activate')}
                  disabled={item.isActive && isLastActiveAdmin}
                  onClick={(event) => {
                    // Qator bosilganda tahrirlash oynasi ochiladi — tugma buni to'xtatadi.
                    event.stopPropagation()
                    if (item.isActive) setDeactivating(item)
                    else activate.mutate(item.id)
                  }}
                >
                  {item.isActive ? (
                    <UserX className="text-[var(--danger-text)]" />
                  ) : (
                    <UserCheck className="text-ios-green" />
                  )}
                </Button>
              )}

              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                title={t('users.resetPassword')}
                aria-label={t('users.resetPassword')}
                onClick={(event) => {
                  event.stopPropagation()
                  setResettingPassword(item)
                }}
              >
                <KeyRound />
              </Button>
            </span>
          )
        },
      }),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, dateTime, errorMessage, currentUser, activeAdminCount, activate],
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={UserCog} tone="brand" size="lg" />
            {t('nav.users')}
          </span>
        }
        subtitle={list.isPending ? t('common.loading') : t('users.subtitle', { n: num(rows.length) })}
        actions={
          canManage ? (
            <Button variant="create" onClick={openCreate}>
              <UserPlus />
              {t('users.create')}
            </Button>
          ) : undefined
        }
      />

      {!canManage ? (
        <Card>
          <EmptyState
            icon={UserCog}
            title={t('users.noAccessTitle')}
            description={t('users.noAccessText')}
          />
        </Card>
      ) : (
        <>
          {list.isError && (
            <Alert tone="danger" title={t('users.loadFailed')}>
              {errorMessage(userErrorCode(list.error))}
            </Alert>
          )}

          <Card className="overflow-hidden">
            <DataTable
              columns={columns}
              data={rows}
              getRowId={(item) => item.id}
              loading={list.isPending}
              onRowClick={openEdit}
              rowLabel={(item) => item.fullName}
              empty={
                <EmptyState
                  icon={Users}
                  title={t('users.emptyTitle')}
                  description={t('users.emptyText')}
                  action={
                    <Button variant="create" onClick={openCreate}>
                      <UserPlus />
                      {t('users.create')}
                    </Button>
                  }
                />
              }
            />
          </Card>
        </>
      )}

      <UserFormDialog open={formOpen} onClose={() => setFormOpen(false)} item={editing} />
      <DeactivateUserDialog item={deactivating} onClose={() => setDeactivating(null)} />
      <ResetPasswordDialog item={resettingPassword} onClose={() => setResettingPassword(null)} />
    </div>
  )
}

/**
 * Rol kodini joriy tilga o'giradi.
 *
 * Server `roleNames` ni ham qaytaradi, lekin u FAQAT o'zbek tilida
 * (seed vaqtida yozilgan matn) — 4 tilli interfeysda ishlatib bo'lmaydi.
 * Shu sababli kod orqali `roles.<code>` tarjimasi ishlatiladi
 * (`settings-dialog.tsx` dagi bilan bir xil naqsh). Tarjimasi topilmasa —
 * kodning o'zi ko'rsatiladi (yashirilmaydi: bu holat kod xatosini bildiradi).
 */
function roleLabel(t: (key: string) => string, code: string): string {
  const translated = t(`roles.${code}`)
  return translated === `roles.${code}` ? code : translated
}
