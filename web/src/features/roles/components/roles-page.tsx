import { getRouteApi } from '@tanstack/react-router'
import { ChevronRight, Lock, Plus, ShieldCheck } from 'lucide-react'
import { useMemo, useState } from 'react'

import { useAuth } from '@/features/auth'
import { problemCode } from '@/shared/api/problem-details'
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

import type { RoleSummary } from '../api/roles-api'
import { useRoleCatalog, useRoles } from '../api/roles-queries'
import { ROLES_MANAGE, roleDisplayName } from '../lib/role-presentation'
import { DeleteRoleDialog } from './delete-role-dialog'
import { RoleEditorDialog } from './role-editor-dialog'

const helper = dataColumnHelper<RoleSummary>()

/** Modul darajasida — har render'da yangi massiv jadval modelini buzadi. */
const EMPTY: RoleSummary[] = []

/** Manzildagi «yangi rol» belgisi — identifikator o'rnida. */
const NEW_ROLE = 'new'

/**
 * Marshrut «apisi» — aylanma bog'liqlikdan qochish uchun
 * (`employees-page.tsx` dagi izohga qarang).
 */
const route = getRouteApi('/_app/rollar')

/**
 * Rollar — egasining o'z qo'li bilan.
 *
 * <b>Kim foydalanadi.</b> Rollar faqat «Foydalanuvchilar» dagi bir necha
 * tizim hisobi uchun (3–4 HR va rahbar) — xodimlar uchun emas. Shu sababli
 * ro'yxatda filtr ham, sahifalash ham, qidiruv ham yo'q: rollar bir
 * qarashda sig'adi.
 *
 * <b>Nega muharrir OYNA, alohida sahifa emas.</b> «Foydalanuvchilar» bilan
 * bir xil naqsh (ro'yxat + oyna): ikkala ekran bir-birining yonida turadi
 * va bir xil ishlashi kerak. Ochiq rol esa manzilda (`?role=`) — sahifa
 * yangilansa ham, havola yuborilsa ham o'sha rol ochiladi.
 *
 * <b>Har bir o'zgarish darhol kuchga kiradi</b> — deploy va qayta ishga
 * tushirishsiz: server keyingi so'rovdayoq yangi ruxsatlarni oladi,
 * brauzerdagi menyular esa bir daqiqa ichida (`AuthProvider` dagi
 * davriy yangilash).
 */
export function RolesPage() {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const { hasPermission, user } = useAuth()

  const canManage = hasPermission(ROLES_MANAGE)

  const search = route.useSearch()
  const navigate = route.useNavigate()

  const list = useRoles(canManage)
  const rows = list.data ?? EMPTY

  /*
    Katalog OLDINDAN so'raladi, garchi faqat muharrirda kerak bo'lsa ham:
    u kichik va keshda cheksiz turadi (`useRoleCatalog`), muharrir esa
    shunda birinchi ochilishdayoq to'ldirilgan holda chiqadi — skelet
    bir zum chaqnab o'tmaydi.
  */
  useRoleCatalog(canManage)

  /** O'chirish tasdig'i kutilayotgan rol. */
  const [deleting, setDeleting] = useState<RoleSummary | null>(null)

  /*
   * Oyna holati MANZILDAN o'qiladi. `replace` — ochish va yopish alohida
   * tarix qadami emas: aks holda «orqaga» tugmasi har bir ochilgan rolni
   * birma-bir qaytarardi.
   */
  const openRole = (id: string) => {
    void navigate({ search: { role: id }, replace: true })
  }

  const closeEditor = () => {
    void navigate({ search: {}, replace: true })
  }

  const isCreating = search.role === NEW_ROLE

  /**
   * Tahrirlanayotgan rol — ro'yxatdagi JORIY nusxa (`users-page.tsx`
   * dagi `editing` bilan bir xil sabab: oyna doim eng so'nggi ma'lumotni
   * ko'radi).
   */
  const editing = useMemo(
    () =>
      search.role === undefined || isCreating
        ? null
        : (rows.find((role) => role.id === search.role) ?? null),
    [rows, search.role, isCreating],
  )

  /*
   * Manzildagi rol ro'yxatda yo'q — o'chirilgan yoki havola eskirgan.
   * Oyna ochilmaydi, sahifa buni ochiq aytadi: jim qolish «tugma
   * ishlamayapti» degan taassurot berardi.
   */
  const missing =
    search.role !== undefined && !isCreating && list.isSuccess && editing === null

  const columns = useMemo<Array<DataColumn<RoleSummary>>>(
    () => [
      helper.accessor('name', {
        header: t('roleEditor.columnRole'),
        /*
          Yagona moslashuvchan ustun. Ikona (40px) + oraliq (12px) +
          katak to'ldirishi (32px) = 84px; qolgan joy nomga. Egasi yozgan
          nom 100 belgigacha bo'lishi mumkin — uzuni kesiladi, to'lig'i
          `title` atributida.
        */
        meta: { minWidth: '280px' },
        cell: ({ row }) => {
          const role = row.original
          const name = roleDisplayName(t, role)

          return (
            <div className="flex min-w-0 items-center gap-3">
              <IconTile
                icon={role.isProtected ? Lock : ShieldCheck}
                tone={role.isProtected ? 'gray' : 'brand'}
              />
              <div className="min-w-0">
                <p className="truncate-1 font-medium" title={name}>
                  {name}
                </p>
                <p className="tabular truncate-1 text-footnote text-[var(--fg-secondary)]">
                  {t('roleEditor.permissionCount', { n: num(role.permissions.length) })}
                  {/*
                    Telefonda «Foydalanuvchilar» ustuni yashiriladi —
                    sanoq nom ostiga tushadi. Rolni o'chirish mumkinmi
                    degan savolga javob aynan shu raqamda.
                  */}
                  <span className="md:hidden">
                    {' · '}
                    {t('roleEditor.userCount', { n: num(role.userCount) })}
                  </span>
                </p>
              </div>
            </div>
          )
        },
      }),

      helper.accessor('userCount', {
        header: t('roleEditor.columnUsers'),
        // «12 kishi» / «Человек: 12» — 12 belgigacha, 160px bemalol.
        meta: { width: '160px', hideOnMobile: true },
        cell: ({ getValue }) => (
          <span className="tabular text-[var(--fg-secondary)]">
            {t('roleEditor.userCount', { n: num(getValue()) })}
          </span>
        ),
      }),

      helper.display({
        id: 'badges',
        header: '',
        /*
          Ikkita yorliq ustma-ust sig'ishi kerak: «Himoyalangan» va
          «Sizning rolingiz» (eng uzuni ruscha «Ваша роль» emas,
          o'zbekchasi — 16 belgi ≈ 120px). Oxirida strelka (16px).
        */
        meta: { width: '200px', align: 'end' },
        cell: ({ row }) => {
          const role = row.original
          const isOwn = user?.roles.includes(role.code) ?? false

          return (
            <span className="flex items-center justify-end gap-2">
              <span className="flex flex-col items-end gap-1">
                {role.isProtected && (
                  <Badge tone="neutral" size="sm">
                    <Lock className="size-2.5" strokeWidth={2.5} />
                    {t('roleEditor.protected')}
                  </Badge>
                )}
                {isOwn && (
                  <Badge tone="brand" size="sm">
                    {t('roleEditor.yours')}
                  </Badge>
                )}
              </span>
              <ChevronRight className="size-4 shrink-0 text-[var(--fg-tertiary)]" aria-hidden />
            </span>
          )
        },
      }),
    ],
    [t, num, user],
  )

  const create = (
    <Button variant="create" onClick={() => openRole(NEW_ROLE)}>
      <Plus />
      {t('roleEditor.create')}
    </Button>
  )

  return (
    <div className="space-y-6">
      <PageHeader
        title={
          <span className="flex items-center gap-3">
            <IconTile icon={ShieldCheck} tone="brand" size="lg" />
            {t('nav.roles')}
          </span>
        }
        subtitle={
          // Xatoda sanoq YO'Q: «0 ta rol» yolg'on bo'lardi — rollar bor,
          // faqat yuklanmadi (sababi pastdagi ogohlantirishda).
          !canManage || list.isError
            ? undefined
            : list.isPending
              ? t('common.loading')
              : t('roleEditor.subtitle', { n: num(rows.length) })
        }
        actions={canManage ? create : undefined}
      />

      {!canManage ? (
        <Card>
          <EmptyState
            icon={ShieldCheck}
            title={t('users.noAccessTitle')}
            description={t('roleEditor.noAccessText')}
          />
        </Card>
      ) : (
        <>
          {list.isError && (
            <Alert tone="danger" title={t('roleEditor.loadFailed')}>
              {errorMessage(problemCode(list.error))}
            </Alert>
          )}

          {missing && <Alert tone="warning">{errorMessage('rol.topilmadi')}</Alert>}

          <Card className="overflow-hidden">
            <DataTable
              columns={columns}
              data={rows}
              getRowId={(role) => role.id}
              loading={list.isPending}
              skeletonRows={4}
              onRowClick={(role) => openRole(role.id)}
              rowLabel={(role) => roleDisplayName(t, role)}
              empty={
                <EmptyState
                  icon={ShieldCheck}
                  title={t('roleEditor.emptyTitle')}
                  description={t('roleEditor.emptyText')}
                  action={create}
                />
              }
            />
          </Card>
        </>
      )}

      {/*
        Oyna SHARTLI chiziladi va har bir rol uchun o'z `key` i bilan —
        ochilganda forma doim toza holatdan boshlanadi, oldingi rolning
        belgilari qolib ketmaydi (`api-keys-section.tsx` dagi naqsh).
      */}
      {canManage && (isCreating || editing !== null) && (
        <RoleEditorDialog
          key={editing?.id ?? NEW_ROLE}
          role={editing}
          onClose={closeEditor}
          onDelete={(role) => {
            // Tasdiq oynasi muharrirning USTIDA emas, uning O'RNIDA
            // ochiladi: ikki qavat oyna telefonda chalkash ko'rinadi.
            closeEditor()
            setDeleting(role)
          }}
        />
      )}

      <DeleteRoleDialog item={deleting} onClose={() => setDeleting(null)} />
    </div>
  )
}
