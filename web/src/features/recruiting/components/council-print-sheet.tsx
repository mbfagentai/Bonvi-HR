import { createPortal } from 'react-dom'

import { useLocale } from '@/shared/i18n/use-locale'
import type { CouncilMeeting } from '../api/council-api'

const FOLLOW_UP_KEYS = ['resultsDeliveredOn', 'offersGivenOn', 'documentsHandedOn', 'registryOn', 'reserveOn'] as const

const CELL = 'border border-black px-1.5 py-1 align-top text-left'
const HEAD = `${CELL} font-semibold`

/**
 * The paper minutes, printed from the browser (which also saves a PDF). It sits outside `#root`,
 * and `globals.css` hides `#root` when printing, so only this sheet reaches the paper.
 */
export function CouncilPrintSheet({ meeting }: { meeting: CouncilMeeting }) {
  const { t, date, money } = useLocale()
  const members = meeting.members.filter((member) => member.fullName.trim() !== '')
  const present = members.filter((member) => member.present).length
  const decided = meeting.candidates.filter((row) => row.decided)
  const conditions = decided.filter((row) => row.condition !== null)

  return createPortal(
    <div className="council-print hidden bg-white text-[11px] leading-snug text-black print:block">
      <h1 className="text-sm font-bold">{t('council.print.title')}</h1>
      <p className="mt-1">
        {t('council.print.header', {
          number: meeting.number ?? '____',
          date: date(meeting.date),
          present,
        })}
      </p>

      <h2 className="mt-3 font-bold">{t('council.print.membersTitle')}</h2>
      <table className="mt-1 w-full border-collapse">
        <thead>
          <tr>
            <th className={HEAD}>№</th>
            <th className={HEAD}>{t('council.print.name')}</th>
            <th className={HEAD}>{t('council.memberRole')}</th>
            <th className={HEAD}>{t('council.present')}</th>
            <th className={HEAD}>{t('council.print.signature')}</th>
          </tr>
        </thead>
        <tbody>
          {members.map((member, index) => (
            <tr key={index}>
              <td className={CELL}>{index + 1}</td>
              <td className={CELL}>{member.fullName}</td>
              <td className={CELL}>{member.role}</td>
              <td className={CELL}>{member.present ? '☑' : '☐'}</td>
              <td className={`${CELL} w-40`} />
            </tr>
          ))}
        </tbody>
      </table>

      <h2 className="mt-3 font-bold">{t('council.print.candidatesTitle')}</h2>
      <table className="mt-1 w-full border-collapse">
        <thead>
          <tr>
            <th className={HEAD}>№</th>
            <th className={HEAD}>{t('council.print.candidate')}</th>
            <th className={HEAD}>{t('council.print.position')}</th>
            <th className={HEAD}>{t('council.print.form')}</th>
            <th className={HEAD}>{t('council.decisionLabel')}</th>
            <th className={HEAD}>{t('council.practicalLabel')}</th>
            {meeting.canSeeSalary && <th className={HEAD}>{t('council.salary')}</th>}
            <th className={HEAD}>{t('council.startDate')}</th>
          </tr>
        </thead>
        <tbody>
          {meeting.candidates.map((row, index) => (
            <tr key={row.candidateId} className="break-inside-avoid">
              <td className={CELL}>{index + 1}</td>
              <td className={CELL}>{row.fullName}</td>
              <td className={CELL}>{row.position}</td>
              <td className={CELL}>{row.formNumber}</td>
              <td className={CELL}>
                {row.decision !== null && <strong>{t(`council.decision.${row.decision}`)}</strong>}
                {row.conclusion !== null && <div>{row.conclusion}</div>}
              </td>
              <td className={CELL}>{row.practical !== null && t(`council.practical.${row.practical}`)}</td>
              {meeting.canSeeSalary && (
                <td className={CELL}>{row.offeredSalary !== null && money(row.offeredSalary)}</td>
              )}
              <td className={CELL}>{row.startDate !== null && date(row.startDate)}</td>
            </tr>
          ))}
        </tbody>
      </table>

      {conditions.length > 0 && (
        <>
          <h2 className="mt-3 font-bold">{t('council.print.conditionsTitle')}</h2>
          <table className="mt-1 w-full border-collapse">
            <tbody>
              {conditions.map((row) => (
                <tr key={row.candidateId} className="break-inside-avoid">
                  <td className={`${CELL} w-1/3`}>{row.fullName}</td>
                  <td className={CELL}>{row.condition}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      <h2 className="mt-3 font-bold">{t('council.followUpTitle')}</h2>
      <table className="mt-1 w-full border-collapse break-inside-avoid">
        <tbody>
          {FOLLOW_UP_KEYS.map((key) => {
            const done = meeting.followUp[key]
            return (
              <tr key={key}>
                <td className={CELL}>{t(`council.followUp.${key}`)}</td>
                <td className={`${CELL} w-32`}>{done !== null && date(done)}</td>
              </tr>
            )
          })}
        </tbody>
      </table>

      <p className="mt-6 break-inside-avoid">{t('council.print.signatures')}</p>
    </div>,
    document.body,
  )
}
