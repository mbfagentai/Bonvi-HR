import { LoaderCircle, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'

import { useAuth } from '@/features/auth'
import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import {
  Alert,
  Badge,
  Button,
  Checkbox,
  DangerRow,
  Dialog,
  IconTile,
  SegmentedControl,
  Skeleton,
  Switch,
  TextField,
} from '@/shared/ui'

import {
  ROLE_NAME_MAX_LENGTH,
  type RoleCatalog,
  type RoleCatalogModule,
  type RoleSummary,
} from '../api/roles-api'
import { useRoleCatalog, useSaveRole } from '../api/roles-queries'
import {
  allHeld,
  droppedPermissions,
  formFromRole,
  offeredLevels,
  permissionsOf,
  type RoleForm,
} from '../lib/role-access'
import { ROLE_NAME_ERRORS } from '../lib/role-error'
import type { AccessLevel } from '../lib/role-keys'
import {
  LEVEL_TONE,
  SUPER_ADMIN_ROLE_CODE,
  moduleLook,
  roleDisplayName,
  useRoleLabels,
} from '../lib/role-presentation'

const FORM_ID = 'role-form'

/**
 * Ikki bo'lim orasidagi yagona bog'liqlik, ekranda IZOH sifatida
 * ko'rsatiladigan: «Ishga qabul» tugmasi xodim kartasini ham yaratadi,
 * ya'ni `employees.create` siz u hech qayerda chiqmaydi. Server ham,
 * muharrir ham buni rad ETMAYDI (rol baribir to'g'ri saqlanadi) — izoh
 * faqat «nega tugma ko'rinmayapti?» degan savolning oldini oladi.
 */
const HIRE_ACTION = 'employment.hire'
const EMPLOYEES_CREATE = 'employees.create'

/**
 * Pul qoidasi (shartnoma §1): ko'chirish, qo'shimcha vazifa va sinov
 * yakunidagi oklad maydonlari ham shu amalga bog'langan. Belgilanmagan
 * bo'lsa muharrir buni ochiq aytadi — aks holda «oklad maydoni qayoqqa
 * ketdi?» degan savol tug'ilardi.
 */
const SALARY_ACTION = 'employment.salary'

/** Muharrir nega faqat o'qish uchun ochilgani — har biriga o'z izohi. */
type ReadOnlyReason = 'protected' | 'own' | 'above'

/**
 * Rol muharriri — yaratish, tahrirlash va (faqat o'qish uchun) ko'rish.
 *
 * <b>Tuzilishi iOS «Sozlamalar» dagi kabi:</b> har bir bo'lim — bitta
 * qator, o'ng tomonda darajalar segmenti. Segmentda FAQAT shu bo'lim
 * taklif qiladigan darajalar turadi: «Foydalanuvchilar» da «Yo'q | To'liq»,
 * «Xodimlar» da to'rttasi. Segmentlar soni bo'limdan bo'limga farq qiladi
 * va bu ataylab — yo'q darajani ko'rsatib, uni o'chiq qilish ekranni
 * shovqinli qilardi.
 *
 * <b>Uch holatda faqat o'qish uchun</b> (server ham xuddi shularni rad
 * etadi, bu yerda esa tugmani bosishdan OLDIN aytiladi):
 *   • himoyalangan rol (super administrator) — `rol.himoyalangan`;
 *   • o'zining roli — `rol.oz_roli`: odam o'z huquqini o'zi kengaytira
 *     olmasligi kerak;
 *   • rolda so'rovchida YO'Q ruxsat bor — `rol.ortiqcha_ruxsat` ga
 *     yaqin qoida: kengroq rolni faqat undan ham kengroq odam o'zgartiradi
 *     («Foydalanuvchilar» dagi §5 qoidasining ko'zgusi).
 *
 * Tahrirlash rejimida ham so'rovchida yo'q daraja, amal va qo'shimcha
 * ruxsat TAKLIF qilinmaydi — server ularni baribir rad etardi.
 *
 * Komponent har ochilishda YANGIDAN o'rnatiladi (`roles-page.tsx` dagi
 * `key`), ya'ni forma holati oldingi roldan qolib ketmaydi.
 */
export function RoleEditorDialog({
  role,
  onClose,
  onDelete,
}: {
  /** `null` — yangi rol. */
  role: RoleSummary | null
  onClose: () => void
  onDelete: (role: RoleSummary) => void
}) {
  const { t, num } = useLocale()
  const errorMessage = useErrorMessage()
  const labels = useRoleLabels()
  const { user } = useAuth()
  const catalog = useRoleCatalog(true)
  const save = useSaveRole()

  /** So'rovchining o'z ruxsatlari — «nimani bera oladi» chegarasi. */
  const held = useMemo<ReadonlySet<string>>(() => new Set(user?.permissions ?? []), [user])

  const isOwn = role !== null && (user?.roles.includes(role.code) ?? false)

  /*
    Super administrator «kengroq rol» qoidasidan ozod (§5 ning 2-bandi
    bilan bir xil): u hamma narsani bera oladi, rolda eskidan qolgan
    notanish kod bo'lsa ham — saqlashda u baribir tushib qoladi
    (`droppedPermissions`), ya'ni rolni aynan u tozalay olishi kerak.
  */
  const isSuperAdmin = user?.roles.includes(SUPER_ADMIN_ROLE_CODE) ?? false

  const readOnly: ReadOnlyReason | null =
    role === null
      ? null
      : role.isProtected
        ? 'protected'
        : isOwn
          ? 'own'
          : isSuperAdmin || allHeld(role.permissions, held)
            ? null
            : 'above'

  const editable = readOnly === null

  /*
    Forma katalog kelgandan keyin BIR MARTA to'ldiriladi. Katalog odatda
    allaqachon keshda (sahifa uni oldindan so'raydi) va holat birinchi
    render'dayoq tayyor bo'ladi; kelmagan bo'lsa — effekt uni kutadi.

    Bog'liqlikda `role` YO'Q: ro'yxat fonda qayta o'qilganda u yangi
    obyekt bo'lib keladi va forma qayta to'ldirilsa, HR belgilab turgan
    katakchalar o'chib ketardi (`user-form-dialog.tsx` dagi bilan bir xil
    sabab). Boshqa rol ochilganda esa komponentning o'zi yangidan
    o'rnatiladi.
  */
  const [form, setForm] = useState<RoleForm | null>(() =>
    catalog.data === undefined ? null : formFromRole(catalog.data, role),
  )

  useEffect(() => {
    if (form === null && catalog.data !== undefined) setForm(formFromRole(catalog.data, role))
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [catalog.data, form])

  /** Nom maydoniga tegishli xato kodi (klient yoki server). */
  const [nameError, setNameError] = useState<string | null>(null)
  /** Maydonga bog'lanmagan server xatosi. */
  const [failure, setFailure] = useState<string | null>(null)

  /*
    Xato har doim formaning TEPASIDA chiqadi (nom maydoni yoki umumiy
    ogohlantirish), «Saqlash» esa pastda qotib turadi. O'n uch bo'limli
    ro'yxatning oxirida turgan HR xatoni ko'rmay, «tugma ishlamadi» deb
    o'ylardi — shu sababli xatoda oyna tepaga suriladi.
  */
  const formRef = useRef<HTMLFormElement>(null)
  const revealTop = () => formRef.current?.scrollIntoView({ block: 'start', behavior: 'smooth' })

  /** Saqlashda tushib qoladigan kodlar (`droppedPermissions` izohi). */
  const dropped = useMemo(
    () => (role === null || catalog.data === undefined ? [] : droppedPermissions(catalog.data, role)),
    [catalog.data, role],
  )

  /** Formadan yig'iladigan kodlar — saqlash ham, izohlar ham shundan. */
  const nextPermissions = useMemo(
    () => (form === null || catalog.data === undefined ? [] : permissionsOf(catalog.data, form)),
    [catalog.data, form],
  )

  /*
    O'zgarmagan rolda «Saqlash» o'chiq: bo'sh saqlash audit jurnalida
    «o'zgartirildi» degan ma'nosiz yozuv qoldirardi. Tushib qoladigan kod
    bo'lsa ro'yxat farq qiladi — ya'ni «tozalash» uchun saqlash mumkin.
  */
  const unchanged =
    role !== null &&
    form !== null &&
    form.name.trim() === role.name &&
    sameCodes(nextPermissions, role.permissions)

  const codeLabel = (code: string) => {
    const action = labels.action(code)
    return action === code ? labels.extra(code) : action
  }

  const setLevel = (module: RoleCatalogModule, level: AccessLevel) => {
    setForm((previous) => {
      if (previous === null) return previous

      /*
        «Yo'q» ga tushirilgan bo'limning amallari ham olib tashlanadi:
        ko'rish huquqisiz amal ma'nosiz (shartnoma §2) va o'chiq, lekin
        belgilangan katakcha «saqlanadimi yoki yo'qmi?» degan savol
        qoldirardi.
      */
      const actions = new Set(previous.actions)
      if (level === 'none') for (const code of module.actions) actions.delete(code)

      return { ...previous, levels: { ...previous.levels, [module.key]: level }, actions }
    })
  }

  const toggleIn = (field: 'actions' | 'extras', code: string, on: boolean) => {
    setForm((previous) => {
      if (previous === null) return previous
      const next = new Set(previous[field])
      if (on) next.add(code)
      else next.delete(code)
      return field === 'actions' ? { ...previous, actions: next } : { ...previous, extras: next }
    })
  }

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!editable || form === null || catalog.data === undefined) return

    setFailure(null)

    // Server bilan AYNAN bir xil tekshiruv va kodlar — xato qayerdan
    // kelishidan qat'i nazar bir xil ko'rinadi.
    const name = form.name.trim()
    if (name === '') {
      setNameError('rol.nom_bosh')
      revealTop()
      return
    }
    if (name.length > ROLE_NAME_MAX_LENGTH) {
      setNameError('rol.nom_uzun')
      revealTop()
      return
    }
    setNameError(null)

    save.mutate(
      { id: role?.id ?? null, body: { name, permissions: nextPermissions } },
      {
        onSuccess: onClose,
        onError: (error) => {
          const code = problemCode(error)
          if (ROLE_NAME_ERRORS.has(code)) setNameError(code)
          else setFailure(code)
          revealTop()
        },
      },
    )
  }

  const displayName = role === null ? '' : roleDisplayName(t, role)

  /*
    Faqat o'qish rejimida sarlavha — rolning NOMI: bu yerda hech narsa
    tahrirlanmaydi va «Rolni tahrirlash» degan sarlavha yolg'on bo'lardi.
  */
  const title =
    role === null
      ? t('roleEditor.createTitle')
      : editable
        ? t('roleEditor.editTitle')
        : displayName

  const description =
    role === null
      ? undefined
      : editable
        ? displayName
        : t('roleEditor.userCount', { n: num(role.userCount) })

  /*
    Ishga qabul belgilangan, lekin xodim kartasini yaratish huquqi yo'q —
    tugma baribir chiqmaydi (`employees-page.tsx`: `employees.create` +
    `employment.hire`). Rol noto'g'ri emas, shu sababli bu xato emas, izoh.
  */
  const hireWithoutCreate =
    nextPermissions.includes(HIRE_ACTION) && !nextPermissions.includes(EMPLOYEES_CREATE)

  return (
    <Dialog
      open
      onClose={onClose}
      size="lg"
      dismissible={!save.isPending}
      title={title}
      description={description}
      footer={
        editable ? (
          <>
            <Button type="button" onClick={onClose} disabled={save.isPending}>
              {t('common.cancel')}
            </Button>
            <Button
              type="submit"
              form={FORM_ID}
              variant="primary"
              disabled={save.isPending || form === null || unchanged}
            >
              {save.isPending && <LoaderCircle className="animate-spin" />}
              {save.isPending ? t('roleEditor.saving') : t('common.save')}
            </Button>
          </>
        ) : (
          <Button type="button" onClick={onClose}>
            {t('common.close')}
          </Button>
        )
      }
    >
      <form ref={formRef} id={FORM_ID} onSubmit={submit} noValidate className="space-y-5">
        {failure !== null && (
          <Alert tone="danger" className="[animation:pop-in_180ms_var(--ease-ios-out)]">
            {errorMessage(failure)}
          </Alert>
        )}

        {/*
          Uchta alohida qator, shartli bitta tarjima chaqiruvi emas: tarjima
          tekshiruvi chaqiruv ichidagi HAR BIR satrni kalit deb o'qiydi va
          solishtirishdagi «own» ham kalitga aylanib qolardi.
        */}
        {readOnly === 'protected' && <Alert tone="info">{t('roleEditor.readOnlyProtected')}</Alert>}
        {readOnly === 'own' && <Alert tone="info">{t('roleEditor.readOnlyOwn')}</Alert>}
        {readOnly === 'above' && <Alert tone="info">{t('roleEditor.readOnlyAbove')}</Alert>}

        {editable && dropped.length > 0 && (
          <Alert tone="warning">
            {t('roleEditor.dropped', { list: dropped.map(codeLabel).join(', ') })}
          </Alert>
        )}

        {editable && form !== null && (
          <TextField
            label={t('roleEditor.name')}
            hint={t('roleEditor.nameHint')}
            placeholder={t('roleEditor.namePlaceholder')}
            autoComplete="off"
            maxLength={ROLE_NAME_MAX_LENGTH}
            disabled={save.isPending}
            error={nameError === null ? undefined : errorMessage(nameError)}
            value={form.name}
            onChange={(event) => {
              const value = event.target.value
              setNameError(null)
              setForm((previous) => (previous === null ? previous : { ...previous, name: value }))
            }}
          />
        )}

        {catalog.isError ? (
          <Alert tone="danger" title={t('roleEditor.catalogFailed')}>
            {errorMessage(problemCode(catalog.error))}
          </Alert>
        ) : form === null || catalog.data === undefined ? (
          <div className="space-y-2.5" aria-busy>
            {[0, 1, 2, 3, 4, 5].map((index) => (
              <Skeleton key={index} className="h-14 w-full" />
            ))}
          </div>
        ) : (
          <AccessMatrix
            catalog={catalog.data}
            form={form}
            held={held}
            editable={editable}
            disabled={save.isPending}
            onLevel={setLevel}
            onToggle={toggleIn}
            notes={{
              salaryMissing: !form.actions.has(SALARY_ACTION),
              hireWithoutCreate,
            }}
          />
        )}

        {/*
          Rolda odam bo'lsa — o'zgarish ularga qachon yetib borishi. Server
          keyingi so'rovdayoq yangi ruxsatni oladi, ochiq brauzerdagi menyu
          esa davriy yangilanish bilan bir daqiqa ichida ergashadi.
        */}
        {editable && role !== null && role.userCount > 0 && (
          <p className="text-footnote text-[var(--fg-tertiary)]">
            {t('roleEditor.appliesSoon', { n: num(role.userCount) })}
          </p>
        )}

        {/*
          O'chirish — iOS «Sozlamalar» dagi kabi QIZIL QATOR, to'ldirilgan
          tugma emas (`DangerRow` izohi). Rolda odam bo'lsa u o'chiq va
          sababi ostida yozilgan: server baribir `rol.foydalanuvchisi_bor`
          bilan rad etardi.
        */}
        {editable && role !== null && (
          <div className="overflow-hidden rounded-lg border border-[var(--border)]">
            <DangerRow
              icon={Trash2}
              label={t('roleEditor.delete')}
              onClick={() => onDelete(role)}
              disabled={save.isPending || role.userCount > 0}
            />
            {role.userCount > 0 && (
              <p className="border-t border-[var(--border)] px-4 py-2.5 text-footnote text-[var(--fg-tertiary)]">
                {t('roleEditor.deleteBlocked', { n: num(role.userCount) })}
              </p>
            )}
          </div>
        )}
      </form>
    </Dialog>
  )
}

/**
 * Bo'limlar va qo'shimcha ruxsatlar — muharrirning asosiy qismi.
 *
 * Katalogdagi TARTIB saqlanadi va hech narsa qo'lda yozilmagan: bo'limlar,
 * ularning darajalari va amallari faqat serverdan keladi. Yangi bo'lim
 * qo'shilsa u shu yerda o'zi paydo bo'ladi.
 */
function AccessMatrix({
  catalog,
  form,
  held,
  editable,
  disabled,
  onLevel,
  onToggle,
  notes,
}: {
  catalog: RoleCatalog
  form: RoleForm
  held: ReadonlySet<string>
  editable: boolean
  disabled: boolean
  onLevel: (module: RoleCatalogModule, level: AccessLevel) => void
  onToggle: (field: 'actions' | 'extras', code: string, on: boolean) => void
  notes: { salaryMissing: boolean; hireWithoutCreate: boolean }
}) {
  const { t } = useLocale()
  const labels = useRoleLabels()

  return (
    <div className="space-y-5">
      <section className="space-y-2.5">
        {/* «Tanlang» degan chaqiriq faqat tanlash mumkin bo'lganda. */}
        <SectionTitle
          title={t('roleEditor.accessTitle')}
          hint={editable ? t('roleEditor.accessHint') : undefined}
        />

        <ul className="divide-y divide-[var(--border)] rounded-lg border border-[var(--border)]">
          {catalog.modules.map((module) => (
            <ModuleRow
              key={module.key}
              module={module}
              level={form.levels[module.key] ?? 'none'}
              actions={form.actions}
              held={held}
              editable={editable}
              disabled={disabled}
              onLevel={(level) => onLevel(module, level)}
              onAction={(code, on) => onToggle('actions', code, on)}
              notes={notes}
            />
          ))}
        </ul>
      </section>

      {catalog.extras.length > 0 && (
        <section className="space-y-2.5">
          <SectionTitle title={t('roleEditor.extrasTitle')} />

          <ul className="divide-y divide-[var(--border)] rounded-lg border border-[var(--border)]">
            {catalog.extras.map((code) => (
              <li key={code} className="px-4 py-3">
                <Switch
                  label={labels.extra(code)}
                  checked={form.extras.has(code)}
                  // So'rovchida yo'q ruxsatni berib bo'lmaydi (`rol.ortiqcha_ruxsat`).
                  disabled={!editable || disabled || !held.has(code)}
                  onChange={(on) => onToggle('extras', code, on)}
                />
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}

/**
 * Bitta bo'lim qatori: ikona, nom, tanlangan darajaning izohi va o'ngda
 * darajalar segmenti. Amallari bor bo'limda (mehnat munosabatlari) ular
 * katakchalar bo'lib shu qatorning ostida turadi.
 */
function ModuleRow({
  module,
  level,
  actions,
  held,
  editable,
  disabled,
  onLevel,
  onAction,
  notes,
}: {
  module: RoleCatalogModule
  level: AccessLevel
  actions: ReadonlySet<string>
  held: ReadonlySet<string>
  editable: boolean
  disabled: boolean
  onLevel: (level: AccessLevel) => void
  onAction: (code: string, on: boolean) => void
  notes: { salaryMissing: boolean; hireWithoutCreate: boolean }
}) {
  const { t, num } = useLocale()
  const labels = useRoleLabels()
  const { icon, tone } = moduleLook(module.key)

  /*
    Segmentlar: «Yo'q» + bo'lim taklif qiladigan darajalar, lekin faqat
    so'rovchi BERA OLADIGANLARI. Joriy daraja har doim qoladi — aks holda
    segment o'zi turgan qiymatni ko'rsata olmay, birinchisiga sakrab
    ketardi (`SegmentedControl` topilmagan qiymatda birinchini yoqadi).
  */
  const options: AccessLevel[] = [
    'none',
    ...offeredLevels(module).filter(
      (candidate) => candidate === level || allHeld(module.levels[candidate] ?? [], held),
    ),
  ]

  const moduleName = labels.module(module.key)
  const ticked = module.actions.filter((code) => actions.has(code)).length

  /*
    Mehnat munosabatlarida «Ko'rish» o'zi kam narsa aytadi — asosiy savol
    «qaysi amallar?». Shu sababli belgilangan amal bo'lsa izoh ularning
    sonini aytadi.
  */
  const hint =
    level !== 'none' && ticked > 0
      ? t('roleEditor.levelWithActions', { n: num(ticked) })
      : labels.levelHint(level)

  const hasSalaryAction = module.actions.includes(SALARY_ACTION)
  const hasHireAction = module.actions.includes(HIRE_ACTION)

  return (
    <li className="px-4 py-3.5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex min-w-0 items-center gap-3">
          <IconTile icon={icon} tone={tone} size="sm" />
          <div className="min-w-0">
            <p className="truncate-1 text-subhead font-medium" title={moduleName}>
              {moduleName}
            </p>
            <p className="truncate-1 text-caption1 text-[var(--fg-tertiary)]">{hint}</p>
          </div>
        </div>

        {/*
          Faqat o'qish rejimida — segment emas, YORLIQ: `SegmentedControl`
          ning o'chiq holati yo'q va bosiladigandek ko'rinib, bosilmaydigan
          boshqaruv foydalanuvchini aldardi. Bitta variantli segment
          (so'rovchi bu bo'limdan hech narsa bera olmaydi) ham shunday
          chiziladi — yolg'iz «Yo'q» tugmasi tanlov emas.
        */}
        {editable && options.length > 1 ? (
          <SegmentedControl<AccessLevel>
            size="sm"
            aria-label={moduleName}
            options={options.map((value) => ({ value, label: labels.level(value) }))}
            value={level}
            onChange={(next) => {
              if (!disabled) onLevel(next)
            }}
            /*
              Telefonda segment qatorning butun kengligini oladi va to'rtta
              segmentda uzun yorliq («O'zgartirish») sig'maydi — u uch nuqta
              bilan qisqaradi, to'lig'i `title` da (segmentning o'zi beradi).
            */
            className="w-full shrink-0 sm:w-auto [&>button]:truncate [&>button]:px-2.5"
          />
        ) : (
          <Badge tone={LEVEL_TONE[level]} className="self-start sm:self-auto">
            {labels.level(level)}
          </Badge>
        )}
      </div>

      {module.actions.length > 0 && (
        <div className="mt-3.5 space-y-2.5 border-t border-[var(--border)] pt-3 sm:ml-11">
          <p className="text-caption2 font-semibold tracking-wider text-[var(--fg-secondary)] uppercase">
            {t('roleEditor.actionsTitle')}
          </p>

          <div className="grid gap-x-4 gap-y-2.5 sm:grid-cols-2">
            {module.actions.map((code) => (
              <Checkbox
                key={code}
                label={labels.action(code)}
                // «Yo'q» da amallar formadan olib tashlanadi (`setLevel`),
                // ya'ni bu yerda belgi ham, saqlanadigan kod ham yo'q.
                checked={actions.has(code)}
                disabled={!editable || disabled || level === 'none' || !held.has(code)}
                onChange={(on) => onAction(code, on)}
              />
            ))}
          </div>

          {editable && level === 'none' && (
            <p className="text-footnote text-[var(--fg-tertiary)]">
              {t('roleEditor.actionsNeedView')}
            </p>
          )}

          {editable && level !== 'none' && hasSalaryAction && notes.salaryMissing && (
            <p className="text-footnote text-[var(--fg-tertiary)]">
              {t('roleEditor.salaryActionHint')}
            </p>
          )}

          {editable && hasHireAction && notes.hireWithoutCreate && (
            <p className="text-footnote text-ios-orange">{t('roleEditor.hireHint')}</p>
          )}
        </div>
      )}
    </li>
  )
}

/** iOS guruhlangan ro'yxatining sarlavhasi — kichik, bosh harflarda, kulrang. */
function SectionTitle({ title, hint }: { title: string; hint?: string }) {
  return (
    <div className="px-1">
      <h3 className="text-caption2 font-semibold tracking-wider text-[var(--fg-secondary)] uppercase">
        {title}
      </h3>
      {hint !== undefined && (
        <p className="mt-0.5 text-caption1 text-[var(--fg-tertiary)]">{hint}</p>
      )}
    </div>
  )
}

/** Ikki kodlar ro'yxati tartibidan qat'i nazar tengmi. */
function sameCodes(a: readonly string[], b: readonly string[]): boolean {
  if (a.length !== b.length) return false
  const set = new Set(a)
  return b.every((code) => set.has(code))
}
