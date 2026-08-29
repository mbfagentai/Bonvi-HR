import { useNavigate } from '@tanstack/react-router'
import { Plus } from 'lucide-react'
import { useState } from 'react'

import { useAuth } from '@/features/auth'
import { useEmployeeExitInterviews } from '@/features/feedback/api/exit-interviews-queries'
import { ExitInterviewFormDialog } from '@/features/feedback/components/exit-interview-form-dialog'
import { FEEDBACK_VIEW } from '@/features/feedback/lib/feedback-presentation'
import { CloseFlagDialog } from '@/features/flags/components/close-flag-dialog'
import { useEmployeeIncidents } from '@/features/safety/api/incidents-queries'
import { IncidentFormDialog } from '@/features/safety/components/incident-form-dialog'
import { SAFETY_VIEW, SEVERITY_LOOK } from '@/features/safety/lib/safety-presentation'
import { useLocale } from '@/shared/i18n/use-locale'
import { Badge, Button, Card, CardContent, Skeleton } from '@/shared/ui'

import { useEmployeeFlags } from '../api/employees-queries'
import {
  LISTS_MANAGE,
  type EmployeeStage,
  type RoutedMark,
} from '../lib/employee-presentation'
import { FlagBadge } from './employee-badges'
import { RaiseFlagDialog } from './raise-flag-dialog'

/**
 * Ro'yxatdagi bitta yozuv — uch xil manbadan bir xil shaklga keltirilgan.
 *
 * <b>Nega umumiy shakl.</b> HR uchun bu uchtasi bitta savolga javob
 * beradi: «bu odam yo'lida nima bo'lgan?». Ularni uch xil blokda
 * ko'rsatish o'sha savolni uch marta so'rashga majbur qilardi va vaqt
 * bo'yicha tartib yo'qolardi — 2023 yildagi hodisa 2026 yildagi
 * belgidan yuqorida turib qolishi mumkin edi.
 */
interface MarkRow {
  key: string
  /** Saralash uchun — boshlanish sanasi. */
  date: string
  badge: React.ReactNode
  text: string
  /** Sana satri: «2026-08-29» yoki «2026-01-10 — 2026-08-29». */
  period: string
  /** O'ng tomondagi amal (faqat ochiq belgida). */
  action?: React.ReactNode
}

/**
 * Xodim kartasidagi «Belgilar» tabi.
 *
 * <b>Uchta manba, bitta ro'yxat:</b> belgilar (rezerv / qora ro'yxat /
 * nazorat / qobiliyatli), baxtsiz hodisalar va qayta aloqa suhbatlari.
 * Oxirgi ikkitasi BELGI EMAS — ularning ortida to'liq yozuv turadi va
 * ular o'z jurnallarida yashaydi (sabab `employee-presentation.ts` dagi
 * `ROUTED_MARKS` izohida). Bu yerda ular FAQAT o'qish uchun ko'rinadi:
 * tahrirlash o'z sahifasida, chunki u yerda barcha maydonlar bor.
 *
 * <b>Ruxsatlar alohida.</b> `safety.view` yoki `feedback.view` yo'q
 * bo'lsa, tegishli so'rov umuman yuborilmaydi va yozuvlar ro'yxatga
 * qo'shilmaydi — «bo'sh/xatoli» holat ko'rsatilmaydi (yon menyudagi
 * bandlar bilan bir xil qoida).
 */
export function EmployeeMarksTab({
  employeeId,
  employeeName,
  employeePhotoKey,
  employeePinfl,
  stage,
}: {
  employeeId: string
  employeeName: string
  employeePhotoKey: string | null
  /** JSHSHIR — jurnalga o'tishda eng aniq qidiruv kaliti. */
  employeePinfl: string | null
  /** Xodim hozir ishlayaptimi — belgi tanlovi shunga qarab tuziladi. */
  stage: EmployeeStage
}) {
  const { t, date } = useLocale()
  const navigate = useNavigate()
  const { hasPermission } = useAuth()

  const canManage = hasPermission(LISTS_MANAGE)
  const canViewSafety = hasPermission(SAFETY_VIEW)
  const canViewFeedback = hasPermission(FEEDBACK_VIEW)

  const flags = useEmployeeFlags(employeeId)
  const incidents = useEmployeeIncidents(employeeId, canViewSafety)
  const interviews = useEmployeeExitInterviews(employeeId, canViewFeedback)

  const [raising, setRaising] = useState(false)
  const [routed, setRouted] = useState<RoutedMark | null>(null)
  const [closing, setClosing] = useState<{
    id: string
    employeeId: string
    employeeName: string
  } | null>(null)

  /*
   * `isLoading`, `isPending` EMAS. Ruxsat yo'q bo'lganda so'rov
   * `enabled: false` bilan o'chiriladi va TanStack Query uni MANGU
   * «pending» holatida ushlab turadi — `isPending` bilan tekshirilsa,
   * `safety.view` yo'q foydalanuvchi tabda abadiy skelet ko'rardi.
   * `isLoading` esa faqat HAQIQATDA yuklanayotganda rost bo'ladi.
   */
  const loading = flags.isLoading || incidents.isLoading || interviews.isLoading

  /*
   * Jurnalga o'tish kaliti: JSHSHIR bo'lsa — U, aks holda F.I.Sh.
   * Sabab: ikkala jurnalning qidiruvi ham xodimlar ro'yxatidagi bilan bir
   * xil qoidada ishlaydi (F.I.Sh., JSHSHIR, pasport, telefon), lekin ism
   * NOYOB EMAS — «Каримов Азиз» ikkita bo'lishi mumkin va o'shanda HR
   * begona odamning hodisasini ko'rardi. JSHSHIR esa noyob.
   */
  const journalKey = employeePinfl ?? employeeName

  /*
   * Hodisa va suhbat yozuvi TO'LIQ ko'rinishini o'z jurnalida oladi: u
   * yerda o'nlab maydon, komissiya xulosasi va tahrirlash bor. Ularni
   * kartaga nusxalash ikkinchi, eskiradigan ekran yasardi — shu sababli
   * bu yerda faqat havola. Qidiruv bilan ochiladi, chunki yozuvning
   * o'ziga alohida manzil yo'q.
   */
  const openJournal = (to: '/hodisalar' | '/qayta-aloqa') => {
    // Manzildagi kalit — `q` (loyihaning barcha ro'yxatlarida bir xil,
    // `validateListSearch`).
    void navigate({ to, search: { q: journalKey } })
  }

  const rows: MarkRow[] = [
    ...(flags.data ?? []).map((flag) => ({
      key: `flag:${flag.id}`,
      date: flag.startDate,
      badge: <FlagBadge type={flag.type} />,
      text: flag.reason ?? '—',
      period:
        flag.endDate === null
          ? date(flag.startDate)
          : `${date(flag.startDate)} — ${date(flag.endDate)}`,
      // Yopilgan belgini qayta yopib bo'lmaydi — domen rad etadi.
      action:
        canManage && flag.endDate === null ? (
          <Button
            size="sm"
            variant="ghost"
            onClick={() => setClosing({ id: flag.id, employeeId, employeeName })}
          >
            {t('flags.close')}
          </Button>
        ) : undefined,
    })),

    ...(incidents.data ?? []).map((incident) => ({
      key: `incident:${incident.id}`,
      date: incident.incidentDate,
      badge: (
        <Badge tone={SEVERITY_LOOK[incident.severity].tone} dot>
          {t('employees.marks.incident')}
        </Badge>
      ),
      // Hodisa TAVSIFI — belgidagi «sabab» ning o'rnida: ikkalasi ham
      // «nima bo'lgan?» degan savolga javob beradi.
      text: incident.description,
      period: date(incident.incidentDate),
      action: (
        <Button size="sm" variant="ghost" onClick={() => openJournal('/hodisalar')}>
          {t('employees.markOpenJournal')}
        </Button>
      ),
    })),

    ...(interviews.data ?? []).map((interview) => ({
      key: `interview:${interview.id}`,
      date: interview.interviewDate,
      badge: (
        <Badge tone="neutral" dot>
          {t('employees.marks.interview')}
        </Badge>
      ),
      // Xodimning O'Z so'zi bilan aytgan ketish sababi — rasmiy sabab
      // emas (`ExitInterview` domen izohi).
      text: interview.reason,
      period: date(interview.interviewDate),
      action: (
        <Button size="sm" variant="ghost" onClick={() => openJournal('/qayta-aloqa')}>
          {t('employees.markOpenJournal')}
        </Button>
      ),
    })),
  ].sort((a, b) => b.date.localeCompare(a.date))

  return (
    <Card>
      <CardContent className="pt-5">
        {canManage && (
          <div className="mb-4 flex justify-end">
            <Button size="sm" variant="create" onClick={() => setRaising(true)}>
              <Plus />
              {t('employees.raiseFlag')}
            </Button>
          </div>
        )}

        {loading ? (
          <Skeleton className="h-24 w-full" />
        ) : rows.length > 0 ? (
          <ul className="space-y-3">
            {rows.map((row) => (
              <li key={row.key} className="flex items-start gap-3">
                {row.badge}
                <span className="min-w-0 flex-1">
                  <span className="block text-footnote">{row.text}</span>
                  <span className="tabular block text-caption2 text-[var(--fg-tertiary)]">
                    {row.period}
                  </span>
                </span>
                {row.action}
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-subhead text-[var(--fg-secondary)]">{t('common.empty')}</p>
        )}
      </CardContent>

      <RaiseFlagDialog
        employeeId={employeeId}
        employeeName={employeeName}
        stage={stage}
        open={raising}
        onClose={() => setRaising(false)}
        /*
          Havolali yozuv tanlandi: belgi oynasi YOPILADI va o'rniga to'liq
          forma ochiladi. Ikkalasi bir vaqtda turmaydi — ustma-ust ikkita
          modal HR ni qaysi biri faolligi haqida o'ylashga majbur qilardi.
        */
        onRoute={(mark) => {
          setRaising(false)
          setRouted(mark)
        }}
      />

      <IncidentFormDialog
        open={routed === 'incident'}
        onClose={() => setRouted(null)}
        item={null}
        lockedEmployee={{ id: employeeId, name: employeeName, photoKey: employeePhotoKey }}
      />

      <ExitInterviewFormDialog
        open={routed === 'interview'}
        onClose={() => setRouted(null)}
        item={null}
        lockedEmployee={{ id: employeeId, name: employeeName }}
      />

      <CloseFlagDialog item={closing} onClose={() => setClosing(null)} />
    </Card>
  )
}
