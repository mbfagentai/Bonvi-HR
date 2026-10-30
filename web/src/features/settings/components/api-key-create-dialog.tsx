import { zodResolver } from '@hookform/resolvers/zod'
import { Check, Copy, KeyRound, LoaderCircle } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { useDepartmentTree } from '@/features/catalog/api/catalog-queries'
import type { DepartmentNode } from '@/features/catalog/api/catalog-api'
import { flattenTree } from '@/features/catalog/lib/tree'
import { Alert, Button, Dialog, SegmentedControl, Select, TextField, type SelectOption } from '@/shared/ui'

import { API_KEY_NAME_MAX_LENGTH, type ApiKeyScope, type CreatedApiKey } from '../api/api-keys-api'
import { useCreateApiKey } from '../api/api-keys-queries'

const FORM_ID = 'api-key-create-form'

const EMPTY_TREE: DepartmentNode[] = []

/**
 * Nom — yagona maydon.
 *
 * Xato XABARI emas, xato KODI saqlanadi (`user-schemas.ts` dagi bilan bir
 * xil naqsh): tarjima ekranga chizilganda tanlanadi, ya'ni til
 * almashtirilganda ochiq turgan xato ham darhol o'zgaradi. Kodlar
 * backend'nikilar bilan AYNAN bir xil — `CreateApiKeyCommandHandler` ham
 * shu ikkitasini qaytaradi.
 *
 * Eksport qilinmaydi: bitta modulda komponent bilan birga boshqa eksport
 * bo'lsa React Fast Refresh ishlamaydi (`data-table-columns.ts` izohi).
 */
const schema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'tashqi.kalit_nomi_bosh')
    .max(API_KEY_NAME_MAX_LENGTH, 'tashqi.kalit_nomi_uzun'),
})

type FormValues = z.infer<typeof schema>

/**
 * Yangi API kaliti — IKKI QADAMLI oyna.
 *
 * <b>1-qadam:</b> nom so'raladi. <b>2-qadam:</b> to'liq kalit ko'rsatiladi.
 * Ikkalasi BITTA oynada, ketma-ket: ikkinchi qadam birinchisining
 * natijasi va u boshqa hech qayerdan chiqmaydi — oyna yopilib, keyin
 * yangisi ochilsa, foydalanuvchi «nusxa olishga ulgurmadim» degan holatga
 * tushib qolardi.
 *
 * <b>⚠️ Kalit EKRANDA QOLMAYDI — uchta qulf.</b>
 *   1. Kalit faqat shu komponentning LOKAL holatida turadi; hech qanday
 *      so'rov keshiga, global holatga yoki `localStorage` ga yozilmaydi.
 *   2. Komponent oyna yopilganda butunlay YO'Q QILINADI (chaqiruvchi uni
 *      shartli chizadi) — holat bilan birga kalit ham ketadi.
 *   3. Yo'qolishdan oldin `create.reset()` chaqiriladi: React Query
 *      mutatsiya javobini O'Z keshida ham saqlaydi va u observer
 *      yo'qolgandan keyin ham `gcTime` davomida xotirada qolardi.
 *
 * Nusxa olish tugmasi bor, «yana ko'rsatish» esa YO'Q — ko'rsatadigan
 * narsa qolmaydi.
 */
export function ApiKeyCreateDialog({ onClose }: { onClose: () => void }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const create = useCreateApiKey()

  /**
   * Doira. Sukut — `Basic`: kengroq doira ONGLI tanlanadi, tasodifan emas.
   * Forma maydoni emas, chunki uni tekshiradigan narsa yo'q (ikki qiymatdan biri).
   */
  const [scope, setScope] = useState<ApiKeyScope>('Basic')

  /*
    Bo'lim davomati kaliti (2026-10-30) — bo'lim rahbarining boti: kalit faqat shu bo'lim va
    uning ichidagilarining davomati va telefonlarini ko'radi. Bo'limsiz yuborilmaydi.
  */
  const [departmentId, setDepartmentId] = useState('')
  const [departmentMissing, setDepartmentMissing] = useState(false)
  const departments = useDepartmentTree(false)
  const departmentOptions = useMemo<SelectOption[]>(
    () =>
      flattenTree(departments.data ?? EMPTY_TREE).map((row) => ({
        value: row.node.id,
        label: row.node.name,
        depth: row.depth,
      })),
    [departments.data],
  )

  /** Yaratilgan kalit. `null` — birinchi qadam (nom formasi) ko'rinadi. */
  const [created, setCreated] = useState<CreatedApiKey | null>(null)

  /** Nusxa olish natijasi. `failed` — brauzer buferga yozishga ruxsat bermadi. */
  const [copy, setCopy] = useState<'idle' | 'done' | 'failed'>('idle')

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '' },
    mode: 'onSubmit',
    reValidateMode: 'onChange',
  })

  /*
    Komponent yo'qolishidan oldin mutatsiya holati tozalanadi — yuqoridagi
    3-qulf. Bog'liqliklar ro'yxati bo'sh: chaqiruv faqat bir marta,
    umrning oxirida bajariladi.
  */
  useEffect(
    () => () => {
      create.reset()
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  )

  const onSubmit = handleSubmit(async (values) => {
    if (scope === 'DepartmentAttendance' && departmentId === '') {
      setDepartmentMissing(true)
      return
    }

    try {
      setCreated(
        await create.mutateAsync({
          name: values.name.trim(),
          scope,
          ...(scope === 'DepartmentAttendance' ? { departmentId } : {}),
        }),
      )
    } catch {
      // Xato `create.error` da — formaning tepasida server kodi bilan chiqadi.
    }
  })

  const onCopy = async () => {
    if (created === null) return

    try {
      await navigator.clipboard.writeText(created.key)
      setCopy('done')
    } catch {
      /*
        Buferga yozish taqiqlanishi mumkin (xavfsiz bo'lmagan ulanish yoki
        brauzer sozlamasi). Bunda kalit YO'QOLMAYDI — u ekranda belgilanadigan
        matn bo'lib turadi va xabar aynan shuni aytadi.
      */
      setCopy('failed')
    }
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      /*
        Kalit ko'rinib turganda oyna fonni bosish yoki Escape bilan
        YOPILMAYDI: tasodifiy yopish kalitni qaytarib bo'lmaydigan darajada
        yo'qotardi. Yopishning yagona yo'li — pastdagi tugma, ya'ni ongli
        harakat. Birinchi qadamda esa faqat so'rov ketayotganda qulflanadi.
      */
      dismissible={created === null ? !isSubmitting : false}
      title={
        created === null
          ? t('settings.apiKeys.createTitle')
          : t('settings.apiKeys.createdTitle')
      }
      description={created === null ? t('settings.apiKeys.createSubtitle') : created.name}
      footer={
        created === null ? (
          <>
            <Button type="button" onClick={onClose} disabled={isSubmitting}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" form={FORM_ID} variant="primary" disabled={isSubmitting}>
              {isSubmitting && <LoaderCircle className="animate-spin" />}
              {isSubmitting ? t('settings.apiKeys.creating') : t('settings.apiKeys.createSubmit')}
            </Button>
          </>
        ) : (
          <Button type="button" variant="primary" onClick={onClose}>
            {t('settings.apiKeys.createdDone')}
          </Button>
        )
      }
    >
      {created === null ? (
        <form id={FORM_ID} onSubmit={onSubmit} noValidate className="space-y-4">
          {create.isError && (
            <Alert tone="danger">{errorMessage(problemCode(create.error))}</Alert>
          )}

          <TextField
            label={t('settings.apiKeys.name')}
            hint={t('settings.apiKeys.nameHint')}
            placeholder={t('settings.apiKeys.namePlaceholder')}
            autoComplete="off"
            autoFocus
            maxLength={API_KEY_NAME_MAX_LENGTH}
            disabled={isSubmitting}
            error={errors.name ? errorMessage(errors.name.message) : undefined}
            {...register('name')}
          />

          <div className="space-y-2">
            <span className="block text-footnote font-medium text-[var(--fg-secondary)]">
              {t('settings.apiKeys.scopeLabel')}
            </span>
            <SegmentedControl
              options={[
                { value: 'Basic', label: t('settings.apiKeys.scopeBasic') },
                { value: 'FullRead', label: t('settings.apiKeys.scopeFull') },
                { value: 'DepartmentAttendance', label: t('settings.apiKeys.scopeDepartment') },
              ]}
              value={scope}
              onChange={setScope}
              className="w-full"
              aria-label={t('settings.apiKeys.scopeLabel')}
            />
            <p className="text-footnote text-[var(--fg-tertiary)]">
              {scope === 'Basic'
                ? t('settings.apiKeys.scopeBasicHint')
                : scope === 'FullRead'
                  ? t('settings.apiKeys.scopeFullHint')
                  : t('settings.apiKeys.scopeDepartmentHint')}
            </p>
          </div>

          {scope === 'DepartmentAttendance' && (
            <Select
              label={t('settings.apiKeys.department')}
              placeholder={t('settings.apiKeys.departmentPlaceholder')}
              hint={t('settings.apiKeys.departmentHint')}
              options={departmentOptions}
              disabled={isSubmitting}
              value={departmentId}
              onChange={(event) => {
                setDepartmentId(event.target.value)
                setDepartmentMissing(false)
              }}
              error={departmentMissing ? t('settings.apiKeys.departmentRequired') : undefined}
            />
          )}

          {scope === 'FullRead' && (
            <Alert tone="warning" title={t('settings.apiKeys.scopeFullWarning')}>
              {t('settings.apiKeys.scopeFullWarningText')}
            </Alert>
          )}
        </form>
      ) : (
        <div className="space-y-4">
          <Alert tone="warning" title={t('settings.apiKeys.createdWarning')}>
            {t('settings.apiKeys.createdWarningText')}
          </Alert>

          {/*
            Kalit BUTUNICHA ko'rinadi — qisqartirilmaydi va yulduzcha bilan
            yashirilmaydi: uni nusxa olish kerak, ya'ni ko'z bilan tekshirish
            ham kerak. `break-all` — 47 belgili satr telefon kengligida ham
            o'ramga sig'adi, gorizontal aylantirish kerak bo'lmaydi.
          */}
          <code className="tabular block rounded-md border border-[var(--border)] bg-[var(--bg-sunken)] px-3.5 py-3 text-footnote break-all select-all">
            {created.key}
          </code>

          <div className="flex flex-wrap items-center gap-3">
            <Button type="button" variant="secondary" onClick={() => void onCopy()}>
              {copy === 'done' ? <Check className="text-ios-green" /> : <Copy />}
              {copy === 'done' ? t('settings.apiKeys.copied') : t('settings.apiKeys.copy')}
            </Button>

            <span className="tabular flex items-center gap-1.5 text-caption1 text-[var(--fg-tertiary)]">
              <KeyRound className="size-3.5" aria-hidden />
              {created.prefix}
            </span>
          </div>

          {copy === 'failed' && <Alert tone="danger">{t('settings.apiKeys.copyFailed')}</Alert>}
        </div>
      )}
    </Dialog>
  )
}
