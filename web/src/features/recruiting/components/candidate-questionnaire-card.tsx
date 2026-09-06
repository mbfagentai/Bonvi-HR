import type { ReactNode } from 'react'

import { useLocale } from '@/shared/i18n/use-locale'
import { Card, CardContent, CardDivider, CardHeader } from '@/shared/ui'
import type { CandidateQuestionnaire } from '../api/recruiting-api'
import { lowerFirst } from '../lib/recruiting-presentation'

/**
 * ⭐ Nomzod ANKETASI — botda to'ldirilgan javoblar.
 *
 * <b>Butun blok yo'qolishi mumkin.</b> `questionnaire === null` odatiy
 * hol: HR qo'lda kiritgan nomzodda ham, bot ishga tushgunicha yozilgan
 * nomzodlarda ham anketa yo'q. Bunday paytda «ma'lumot yo'q» degan bo'sh
 * karta chizilmaydi — u har bir eski nomzod kartasida joy egallab,
 * hech narsa aytmasdan turardi.
 *
 * <b>Bo'sh maydon ham chizilmaydi</b> va bu yonidagi «Shaxsiy ma'lumot»
 * kartasidan ATAYLAB farq qiladi. U yerda bo'sh maydon chiziqcha bilan
 * ko'rsatiladi, chunki HR o'sha ma'lumotni O'ZI to'ldiradi va nima
 * yetishmayotganini ko'rishi kerak. Anketa esa NOMZODNING javobi:
 * botda beshta qadam o'tkazib yuborilishi mumkin
 * (`CandidateQuestionnaire` domen izohi) va o'n uchta maydonning
 * yarmi chiziqcha bo'lib turgan ro'yxat o'qilmaydigan bo'lib qolardi.
 *
 * <b>Sanoqlar RAQAM ham, inglizcha kalit ham emas.</b> Ular tarjima
 * orqali chiziladi; tanilmagan qiymat (backend yangi javob qo'shsa)
 * `null` bo'ladi va qatori umuman chizilmaydi — noto'g'ri javob
 * ko'rsatgandan ko'ra hech narsa ko'rsatmagan ma'qul.
 */
export function CandidateQuestionnaireCard({
  questionnaire,
}: {
  questionnaire: CandidateQuestionnaire | null
}) {
  const { t, dateTime } = useLocale()

  if (questionnaire === null) return null

  const {
    education,
    workExperience,
    maritalStatus,
    russianLevel,
    otherLanguages,
    officeSkills,
    lastSalary,
    extraNote,
    hasDriverLicense,
    driverLicenseCategories,
    expectedSalary,
    referralSource,
    offerAcceptedAt,
  } = questionnaire

  /*
    Haydovchilik guvohnomasi — BITTA qator, ikkita maydondan.
    «Yo'q» ham javobning o'zi (bot bu qadamni o'tkazib yubormaydi),
    shu sababli u bo'sh maydon deb hisoblanmaydi va doim chiziladi.
  */
  const license = hasDriverLicense
    ? driverLicenseCategories === null
      ? t('recruiting.driverLicenseYes')
      : t('recruiting.driverLicenseYesWith', { categories: driverLicenseCategories })
    : t('recruiting.driverLicenseNo')

  return (
    <Card>
      <CardHeader
        title={t('recruiting.questionnaire')}
        subtitle={t('recruiting.questionnaireHint')}
      />

      <CardDivider />

      <CardContent className="pt-4 pb-5">
        <dl className="grid gap-x-8 gap-y-4 sm:grid-cols-2">
          <Answer
            label={t('recruiting.educationLabel')}
            value={education === null ? null : t(`recruiting.education.${lowerFirst(education)}`)}
          />

          <Answer label={t('recruiting.workExperienceLabel')} value={workExperience} />

          <Answer
            label={t('recruiting.maritalLabel')}
            value={
              maritalStatus === null ? null : t(`recruiting.marital.${lowerFirst(maritalStatus)}`)
            }
          />

          {/*
            Rus tili darajasi xodim kartasidagi bilan AYNAN bir xil
            sanoq (server shuni qaytaradi), shu sababli tarjimasi ham
            o'sha yerdan olinadi — ikkinchi nusxa yozilmaydi.
          */}
          <Answer
            label={t('recruiting.russianLevelLabel')}
            value={russianLevel === null ? null : t(`competency.languageLevel.${russianLevel}`)}
          />

          <Answer label={t('recruiting.otherLanguagesLabel')} value={otherLanguages} />
          <Answer label={t('recruiting.officeSkillsLabel')} value={officeSkills} />
          <Answer label={t('recruiting.lastSalaryLabel')} value={lastSalary} />
          <Answer label={t('recruiting.driverLicenseLabel')} value={license} />

          <Answer
            label={t('recruiting.expectedSalaryLabel')}
            value={
              expectedSalary === null
                ? null
                : t(`recruiting.expectedSalary.${lowerFirst(expectedSalary)}`)
            }
          />

          <Answer
            label={t('recruiting.referralLabel')}
            value={
              referralSource === null
                ? null
                : t(`recruiting.referral.${lowerFirst(referralSource)}`)
            }
          />
        </dl>

        {/*
          «O'zim haqimda» — erkin va uzun matn. U ikki ustunli to'rga
          sig'maydi, shu sababli pastda alohida blokda turadi — nomzod
          kartasidagi ichki izoh bilan AYNAN bir xil ko'rinishda.
          `whitespace-pre-line`: botda yozilgan qatorlar saqlanadi.
        */}
        {extraNote !== null && (
          <p className="mt-5 rounded-md bg-[var(--bg-sunken)] px-3 py-2 text-footnote whitespace-pre-line text-[var(--fg-secondary)]">
            {extraNote}
          </p>
        )}

        {/*
          Ommaviy oferta bilan rozilik — HUQUQIY FAKT, bayroq emas:
          shu sababli «ha/yo'q» emas, aniq VAQTI bilan yoziladi.

          ⚠️ Bo'sh qiymat «rozilik yo'q» degani EMAS, «rozilik
          SO'RALMAGAN» degani: oferta havolasi (`bot.offerUrl`)
          sozlanmagan bo'lsa bot o'sha qadamni umuman chizmaydi va
          nomzod hech qanday oferta ko'rmaydi. Shunday holatda qator
          ham chizilmaydi — «rozilik berilmagan» deb yozish nomzodni
          rad etgandek ko'rsatardi, holbuki undan hech kim so'ramagan.
        */}
        {offerAcceptedAt !== null && (
          <p className="tabular mt-4 text-caption2 text-[var(--fg-tertiary)]">
            {t('recruiting.offerAccepted', { date: dateTime(offerAcceptedAt) })}
          </p>
        )}
      </CardContent>
    </Card>
  )
}

/**
 * Anketaning bitta javobi.
 *
 * `null` — qator UMUMAN chizilmaydi (`<dt>` ham, `<dd>` ham). Yonidagi
 * «Shaxsiy ma'lumot» kartasidagi `Field` esa chiziqcha ko'rsatadi —
 * farqning sababi kartaning yuqorisidagi izohda.
 */
function Answer({ label, value }: { label: string; value: string | null }): ReactNode {
  if (value === null) return null

  return (
    <div className="min-w-0">
      <dt className="text-caption1 text-[var(--fg-tertiary)]">{label}</dt>
      <dd className="mt-0.5 text-subhead break-words">{value}</dd>
    </div>
  )
}
