import { KeyRound, Lock, UserCheck, UserCog, UserPlus, UserX, Users } from 'lucide-react'
import { useMemo, useState } from 'react'

import { formatFullPhone, useAuth } from '@/features/auth'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
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
import { LEADER_ROLE_CODES, USERS_MANAGE, canActOnRole, currentRoleCode } from '../lib/user-presentation'
import { DeactivateUserDialog } from './deactivate-user-dialog'
import { ResetPasswordDialog } from './reset-password-dialog'
import { UserFormDialog } from './user-form-dialog'
import { UserPhoto } from './user-photo'

const helper = dataColumnHelper<UserSummary>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: UserSummary[] = []

/**
 * Foydalanuvchilarni boshqarish — faqat rahbariyat.
 *
 * <b>Nega ro'yxatda qidiruv, filtr va sahifalash yo'q.</b> Tizimdan
 * atigi bir necha kishi (bir necha HR menejeri, bosh rahbar va super
 * administrator) foydalanadi — bu ekran ularning HAMMASINI bitta qarashda
 * ko'rsatadi.
 *
 * <b>Nega butun sahifa `users.manage` bilan o'ralgan.</b> `GET /api/users`
 * ning o'zi ham shu ruxsat bilan himoyalangan (`UserEndpoints.cs`): HR
 * menejeri bu ro'yxatni SERVERDAN OLA OLMAYDI, ya'ni bu yerda faqat
 * amallarni yashirish yetarli emas — butun kontent ruxsatga bog'liq.
 *
 * <b>Nega ro'yxat HAMMANI ko'rsatadi, hatto tegib bo'lmaydiganlarni ham.</b>
 * Bosh rahbar super administratorni ko'radi, lekin unga tegishli tugmalar
 * chizilmaydi. Yashirish yo'li yomonroq bo'lardi: «tizimda yana kim bor?»
 * degan savolga javob berib bo'lmay qolardi va bosh rahbar o'zi ko'rmagan
 * hisob bilan telefon raqami to'qnashganda sababni tushunmasdi.
 */
export function UsersPage() {
  const { t, num, dateTime } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission, user: currentUser } = useAuth()

  const canManage = hasPermission(USERS_MANAGE)

  /** So'rovchining o'z roli — «kim kimga tega oladi» qoidasining birinchi yarmi. */
  const myRole = currentRoleCode(currentUser?.roles)

  /**
   * Tahrirlanayotgan hisob — nusxasi emas, IDENTIFIKATORI saqlanadi.
   *
   * NEGA: oynadagi rasm bloki ro'yxatni bekor qiladi (`useUploadUserPhoto`),
   * ya'ni rasm yuklangach ro'yxat qayta keladi. Agar bu yerda qatorning
   * NUSXASI turganida, oynadagi `photoKey` eski qiymatda qotib qolardi va
   * yangi surat faqat oyna yopilib qayta ochilgandan keyin ko'rinardi.
   */
  const [editingId, setEditingId] = useState<string | null>(null)
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
   * Tizimdagi faol RAHBARLAR soni (super administrator + bosh rahbar).
   *
   * NEGA klientda hisoblanadi: butun ro'yxat allaqachon xotirada, server
   * bilan yana bir so'rov almashish shart emas. Faqat OXIRGI faol rahbarni
   * himoya qilish uchun kerak — pastdagi `isLastActiveLeader` ga qarang.
   * Backend baribir o'zi ham tekshiradi (`foydalanuvchi.oxirgi_administrator`),
   * bu yerdagi hisob faqat tugmani oldindan o'chirib, foydasiz so'rov-javobning
   * oldini oladi.
   */
  const activeLeaderCount = useMemo(
    () =>
      rows.filter((row) => row.isActive && row.roleCode !== null && LEADER_ROLE_CODES.includes(row.roleCode))
        .length,
    [rows],
  )

  /** Ro'yxatdagi JORIY holat — oyna doim eng so'nggi ma'lumotni ko'radi. */
  const editing = useMemo(
    () => rows.find((row) => row.id === editingId) ?? null,
    [rows, editingId],
  )

  const openCreate = () => {
    setEditingId(null)
    setFormOpen(true)
  }

  const openEdit = (row: UserSummary) => {
    setEditingId(row.id)
    setFormOpen(true)
  }

  const columns = useMemo<Array<DataColumn<UserSummary>>>(
    () => [
      helper.accessor('fullName', {
        header: t('users.columnUser'),
        /*
          Yagona moslashuvchan ustun (`minWidth`, boshqalari qat'iy `width`).
          Xrom: avatar (size="lg" = 48px) + oraliq (gap-3 = 12px) + katak
          to'ldirishi (px-4 × 2 = 32px) = 92px. Qolgan 208px matnga: F.I.Sh.
          bitta qatorda (`truncate-1`, uzuni kesiladi — to'lig'i `title`
          atributida), ostida telefon («+998 90 508 88 88», 17 belgi,
          text-footnote 13px) — bemalol sig'adi. 300px — xodimlar
          ro'yxatidagi xuddi shu konstruksiya bilan bir xil: u yerda ham
          rasm HAQIQIY va ism yonida ko'proq joy talab qiladi.
        */
        meta: { minWidth: '300px' },
        cell: ({ row }) => {
          const item = row.original
          return (
            <div className="flex min-w-0 items-center gap-3">
              {/*
                Bosh harflar EMAS, haqiqiy rasm: `photoKey` bo'lsa u
                baytlar bilan olinadi (`UserPhoto`). Kaliti yo'q hisob
                uchun so'rov umuman yuborilmaydi va `Avatar` bosh harflarni
                chizadi — ro'yxat behuda 404 so'rovlar yubormaydi.
              */}
              <UserPhoto
                userId={item.id}
                photoKey={item.photoKey}
                name={item.fullName}
                size="lg"
              />
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
          190px − katak to'ldirishi (32px) = 158px matnga. Uchala rol nomi
          ham shunga sig'adi: «HR menejeri» (11 belgi), «Bosh rahbar»
          (11 belgi), «Super administrator» (19 belgi, ~162px 15px shriftda
          ~8.5px/belgi hisobida) — oxirgisi chegaraga tegib turadi va
          zarurat bo'lsa kesiladi, to'lig'i esa `title` atributida.
        */
        meta: { width: '190px', hideOnMobile: true },
        cell: ({ row }) => {
          const code = row.original.roleCode
          if (code === null) return <span className="text-[var(--fg-tertiary)]">—</span>

          const label = roleLabel(t, code)
          return (
            <span className="truncate-1 block text-[var(--fg-secondary)]" title={label}>
              {label}
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
          kichik zaxira bilan 110px. Tegib bo'lmaydigan qatorda tugmalar
          umuman chizilmaydi — bo'sh joy qoladi, ustun kengligi o'zgarmaydi.
        */
        meta: { align: 'end', width: '110px' },
        cell: ({ row }) => {
          const item = row.original
          const isSelf = currentUser !== null && currentUser.id === item.id

          /*
            «Kim kimga tega oladi» — backend qoidasining ko'zgusi
            (`canActOnRole`). Bosh rahbar super administratorning qatorida
            hech qanday tugma ko'rmaydi: ularni ko'rsatib, keyin 403
            qaytarish foydalanuvchini «nimadir buzilgan» degan xulosaga
            olib borardi.
          */
          if (!canActOnRole(myRole, item.roleCode)) return null

          const isLastActiveLeader =
            item.isActive &&
            item.roleCode !== null &&
            LEADER_ROLE_CODES.includes(item.roleCode) &&
            activeLeaderCount <= 1

          return (
            <span className="flex justify-end gap-1.5">
              {/*
                O'ZINI FAOLSIZLANTIRISH tugmasi shu qatorda umuman
                ko'rsatilmaydi — backend buni 403 bilan baribir to'xtatadi
                (`foydalanuvchi.ozini_blokash`), lekin tugmani ko'rsatib,
                keyin xato qaytarish yomon tajriba.
              */}
              {!isSelf && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  title={
                    item.isActive
                      ? isLastActiveLeader
                        ? errorMessage('foydalanuvchi.oxirgi_administrator')
                        : t('users.deactivate')
                      : t('users.activate')
                  }
                  aria-label={item.isActive ? t('users.deactivate') : t('users.activate')}
                  disabled={item.isActive && isLastActiveLeader}
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
    [t, dateTime, errorMessage, currentUser, myRole, activeLeaderCount, activate],
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
 * Server `roleName` ni ham qaytaradi, lekin u FAQAT o'zbek tilida
 * (seed vaqtida yozilgan matn) — 4 tilli interfeysda ishlatib bo'lmaydi.
 * Shu sababli kod orqali `roles.<code>` tarjimasi ishlatiladi
 * (`settings-dialog.tsx` dagi bilan bir xil naqsh). Tarjimasi topilmasa —
 * kodning o'zi ko'rsatiladi (yashirilmaydi: bu holat kod xatosini bildiradi).
 */
function roleLabel(t: (key: string) => string, code: string): string {
  const translated = t(`roles.${code}`)
  return translated === `roles.${code}` ? code : translated
}
