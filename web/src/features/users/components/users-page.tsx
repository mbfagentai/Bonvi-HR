import { getRouteApi } from '@tanstack/react-router'
import { KeyRound, Lock, Search, UserCheck, UserCog, UserPlus, UserX, Users } from 'lucide-react'
import { useMemo, useState } from 'react'

import { formatFullPhone, useAuth } from '@/features/auth'
import type { RoleSummary } from '@/features/roles/api/roles-api'
import { useRoles } from '@/features/roles/api/roles-queries'
import { roleDisplayName } from '@/features/roles/lib/role-presentation'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { toSearchParam, useSearchDraft } from '@/shared/lib/list-search'
import {
  Alert,
  Badge,
  Button,
  Card,
  DataTable,
  EmptyState,
  IconTile,
  Input,
  PageHeader,
  dataColumnHelper,
  type DataColumn,
} from '@/shared/ui'

import type { UserSummary } from '../api/users-api'
import { useActivateUser, useUsers } from '../api/users-queries'
import { userErrorCode } from '../lib/user-error'
import {
  LEADER_ROLE_CODES,
  USERS_MANAGE,
  actingRoleOf,
  canActOnUser,
} from '../lib/user-presentation'
import { DeactivateUserDialog } from './deactivate-user-dialog'
import { ResetPasswordDialog } from './reset-password-dialog'
import { UserFormDialog } from './user-form-dialog'
import { UserPhoto } from './user-photo'

const helper = dataColumnHelper<UserSummary>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: UserSummary[] = []
const NO_ROLES: RoleSummary[] = []

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`employees-page.tsx` dagi izohga qarang).
 */
const route = getRouteApi('/_app/foydalanuvchilar')

/**
 * Foydalanuvchilarni boshqarish — faqat rahbariyat.
 *
 * <b>Nega filtr va sahifalash yo'q, qidiruv esa BOR.</b> Tizimdan atigi
 * bir necha kishi (bir necha HR menejeri, bosh rahbar va super
 * administrator) foydalanadi — bu ekran ularning HAMMASINI bitta qarashda
 * ko'rsatadi va filtrga ehtiyoj yo'q. Qidiruv esa boshqa sabab bilan
 * qo'shildi: tizimning BARCHA ro'yxatlarida qidiruv maydoni bir xil
 * turadi va bittasida uning yo'qligi «bu yerda ishlamaydimi yoki men
 * topolmayapmanmi?» degan savol tug'dirardi.
 *
 * <b>Qidiruv bu yerda TOR:</b> F.I.Sh. va login (telefon). Hisobda
 * JSHSHIR ham, pasport ham yo'q — shu sababli maydonning matni ham
 * boshqacha (`users.searchPlaceholder`).
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

  /*
    Rollar ro'yxati — «kim kimga tega oladi» qoidasi endi rol KODLARI
    bilan emas, ruxsatlar TO'PLAMI bilan ishlaydi (`canActOnRole`), ya'ni
    ikkala tomonning ham ruxsatlari kerak. Server bu ro'yxatni
    `users.manage` bilan ham beradi — alohida «Rollar» huquqi shart emas.
  */
  const roles = useRoles(canManage)
  const roleList = roles.data ?? NO_ROLES

  /** So'rovchining o'z roli — «kim kimga tega oladi» qoidasining birinchi yarmi. */
  const myRole = useMemo(
    () => actingRoleOf(roleList, currentUser?.roles),
    [roleList, currentUser],
  )

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

  const searchParams = route.useSearch()
  const navigate = route.useNavigate()

  const search = searchParams.q ?? ''

  /*
   * Qidiruv har harfda so'rov yubormaydi (350 ms) va matn manzilga
   * yoziladi. `replace` — qidiruvni aniqlashtirish alohida tarix qadami
   * emas: aks holda «orqaga» tugmasi har bir harfni birma-bir qaytarardi.
   */
  const [draft, setDraft] = useSearchDraft(search, (value) => {
    void navigate({ search: { q: toSearchParam(value) }, replace: true })
  })

  const list = useUsers(search)
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
          Rasm va to'liq ism BIR QATORDA (egasi 2026-10-22 — jadval qatori
          ikkiga bo'linmaydi): telefon endi ism ostida emas, o'z ustunida.
          Ism kesilmaydi — ustun o'zi kengayadi (katak `nowrap`).
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
                size="md"
              />
              <span className="font-medium">{item.fullName}</span>
            </div>
          )
        },
      }),

      helper.accessor('login', {
        header: t('employee.phone'),
        enableSorting: false,
        // «+998 90 508 88 88» — 17 belgi, tabular; kenglik mazmundan
        cell: ({ getValue }) => (
          <span className="tabular text-[var(--fg-secondary)]">{formatFullPhone(getValue())}</span>
        ),
      }),

      helper.display({
        id: 'role',
        header: t('users.columnRole'),
        /*
          190px — pastki chegara. Tizim rollari shunga sig'adi: «HR
          menejeri», «Bosh rahbar», «Super administrator» (19 belgi). Egasi
          yozgan uzunroq nomda («Kadr bo'limi boshlig'i») ustun o'zi
          kengayadi — nom kesilmaydi (katak `nowrap`).
        */
        meta: { width: '190px', hideOnMobile: true },
        cell: ({ row }) => {
          const code = row.original.roleCode
          if (code === null) return <span className="text-[var(--fg-tertiary)]">—</span>

          /*
            Serverdagi NOM — egasi rolni qayta nomlasa, shu yerda ham
            darhol yangisi chiqadi. Tarjima faqat super administratorga
            (`roleDisplayName` izohi).
          */
          const label = roleDisplayName(t, { code, name: row.original.roleName ?? code })
          return (
            <span className="text-[var(--fg-secondary)]">{label}</span>
          )
        },
      }),

      helper.display({
        id: 'status',
        header: t('users.columnStatus'),
        // «Faolsiz» + ixtiyoriy «Bloklangan» yorlig'i YONMA-YON, bir qatorda.
        // 120px — pastki chegara: ikkala yorliqli qatorda ustun o'zi kengayadi.
        meta: { width: '120px' },
        cell: ({ row }) => {
          const item = row.original
          const lockedUntil = item.lockedUntil
          const locked = lockedUntil !== null && new Date(lockedUntil).getTime() > Date.now()

          return (
            <div className="flex items-center gap-1.5">
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
          if (!canActOnUser(myRole, roleList, item.roleCode)) return null

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
    [t, dateTime, errorMessage, currentUser, myRole, roleList, activeLeaderCount, activate],
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

          {/*
            Rollarsiz ro'yxat ko'rinadi, lekin amallar tugmalari chizilmaydi:
            kimga tegish mumkinligini rollarning ruxsatlarisiz bilib bo'lmaydi.
          */}
          {roles.isError && (
            <Alert tone="danger" title={t('roleEditor.loadFailed')}>
              {errorMessage(userErrorCode(roles.error))}
            </Alert>
          )}

          <Card className="overflow-hidden">
            {/*
              Qidiruv maydoni jadvaldan yuqorida — boshqa ro'yxatlardagi
              bilan bir xil joyda. Matn esa boshqacha: bu yerda JSHSHIR
              va pasport yo'q va bo'lmagan narsani va'da qilish HR ni
              behuda urinishga majbur qilardi.
            */}
            <div className="border-b border-[var(--border)] px-4 py-3.5 sm:px-5">
              <Input
                icon={Search}
                shape="pill"
                type="search"
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                placeholder={t('users.searchPlaceholder')}
                aria-label={t('common.search')}
              />
            </div>

            <DataTable
              columns={columns}
              data={rows}
              getRowId={(item) => item.id}
              // Rollar kelguncha ham skelet: aks holda amallar tugmalari
              // bir zumdan keyin «sakrab» paydo bo'lardi.
              loading={list.isPending || roles.isPending}
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
