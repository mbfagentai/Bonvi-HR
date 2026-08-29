import { useNavigate } from '@tanstack/react-router'
import { Cake, ChevronRight } from 'lucide-react'

import { useDashboardBirthdays } from '@/features/dashboard/api/dashboard-queries'
import type { DashboardBirthday } from '@/features/dashboard/api/dashboard-api'
/*
 * Rasm xodimlar modulidan OLINADI, bu yerda qayta yozilmaydi.
 *
 * Sabab tashqi ko'rinishda emas: rasm `Authorization` sarlavhasi bilan
 * `blob` bo'lib so'raladi (oddiy `<img src>` ishlamaydi), kesh kaliti
 * `photoKey` ga bog'lanadi va kaliti yo'q xodim uchun so'rov umuman
 * yuborilmaydi. Bu qoidalarni ikkinchi marta yozish ularning ikkinchi
 * nusxasini hosil qilardi — va bittasi o'zgarganda ekranlardan biri
 * jimgina bosh harflarga qaytib qolardi.
 */
import { EmployeePhoto } from '@/features/employees/components/employee-photo'
import { useLocale } from '@/shared/i18n/use-locale'
import { cn } from '@/shared/lib/cn'
import { Alert, Dialog, EmptyState, Skeleton } from '@/shared/ui'

/** Skelet qatorlari soni: oynaning ko'rinadigan qismiga sig'adigan miqdor. */
const SKELETON_ROWS = [0, 1, 2]

/**
 * Bugungi tug'ilgan kunlar oynasi.
 *
 * <b>Nega umuman kerak.</b> Bosh sahifadagi qator ilgari faqat SONNI
 * aytardi — «Bugun 2 ta xodimning tug'ilgan kuni». HR uchun esa aynan
 * «kim?» degan savol muhim: tabriklash uchun ismni bilish kerak va u
 * ilgari xodimlar ro'yxatidan qo'lda qidirilardi (tug'ilgan sana
 * bo'yicha saralab, bugungi kunni ko'z bilan topib). Endi son bosiladi
 * va ro'yxat shu yerda ochiladi.
 *
 * <b>Nega modal, alohida sahifa emas.</b> Bu ro'yxat — ishning O'ZI
 * emas, unga ishora: HR ochib, ismni ko'rib, kerak bo'lsa kartaga
 * o'tadi yoki yopib, qilayotgan ishini davom ettiradi. Alohida marshrut
 * bosh sahifadan chiqib ketishni talab qilardi va «orqaga» tugmasini
 * bu qisqa qarashning bir qismiga aylantirardi
 * (`notifications-dialog.tsx` dagi bilan bir xil sabab).
 *
 * <b>⚠️ Ma'lumot faqat SHU YERDA so'raladi.</b> `useDashboardBirthdays`
 * ga `open` uzatiladi va oyna yopiq turganda so'rov umuman ketmaydi.
 * Sabab `dashboard-queries.ts` da: bosh sahifa har kirishda ochiladi,
 * bu ro'yxatni esa ko'p hollarda hech kim ochmaydi.
 *
 * <b>Son bu yerda QAYTA ko'rsatilmaydi.</b> U chaqirgan qatorda
 * allaqachon yozilgan va ikkalasi bitta serverdagi shartdan hosil
 * bo'ladi. Uni oynada takrorlash bir raqamni ikki joyda chizish bo'lardi
 * — ular ajralib ketishi uchun ochilgan eshik.
 */
export function BirthdaysDialog({
  open,
  onClose,
}: {
  open: boolean
  onClose: () => void
}) {
  const { t } = useLocale()
  const navigate = useNavigate()

  const list = useDashboardBirthdays(open)
  const items = list.data ?? []

  /**
   * Xodim kartasiga o'tish: oyna YOPILADI, keyin ekran ochiladi.
   *
   * Yopish oldin turadi, chunki modal ochiq qolsa yangi sahifa uning
   * ortida chizilib, foydalanuvchi «orqaga» tugmasini qidirib qolardi
   * (`notifications-dialog.tsx` dagi bilan bir xil tartib).
   */
  const openEmployee = (employeeId: string) => {
    onClose()
    void navigate({ to: '/xodimlar/$employeeId', params: { employeeId } })
  }

  return (
    <Dialog open={open} onClose={onClose} size="md" title={t('dashboard.birthdaysTitle')}>
      {list.isError && (
        <Alert tone="danger" title={t('dashboard.birthdaysLoadFailed')}>
          {t('dashboard.loadFailedHint')}
        </Alert>
      )}

      {list.isPending ? (
        /*
          Spinner emas, skelet: ro'yxat kelganda ekran sakramaydi va
          qatorlarning kelajakdagi shakli oldindan ko'rinib turadi
          (doira + ikki qator matn).
        */
        <div aria-hidden className="space-y-4 py-1">
          {SKELETON_ROWS.map((row) => (
            <div key={row} className="flex items-center gap-3">
              <Skeleton className="size-10 rounded-full" />
              <div className="min-w-0 flex-1 space-y-2">
                <Skeleton className="h-4 w-44" />
                <Skeleton className="h-3 w-32" />
              </div>
            </div>
          ))}
        </div>
      ) : items.length === 0 ? (
        /*
          Bo'sh holat amalda deyarli ko'rinmaydi: qator soni nol bo'lganda
          bosh sahifada UMUMAN chizilmaydi, ya'ni bosiladigan joy ham
          yo'q. U baribir kerak — oyna ochiq turganda boshqa foydalanuvchi
          xodimni arxivga o'tkazishi mumkin va kesh yangilanadi. Bunday
          holatda bo'sh oyna ko'rsatishdan ko'ra sababini aytish yaxshi.
        */
        <EmptyState
          icon={Cake}
          title={t('dashboard.birthdaysEmpty')}
          description={t('dashboard.birthdaysEmptyText')}
        />
      ) : (
        <ul className="divide-y divide-[var(--border)]">
          {items.map((item) => (
            <BirthdayRow
              key={item.id}
              item={item}
              onOpen={() => openEmployee(item.id)}
            />
          ))}
        </ul>
      )}
    </Dialog>
  )
}

/**
 * Ro'yxatning bitta qatori — butunlay bosiladigan.
 *
 * Qatorning HAMMASI bitta tugma (rasm ham, ism ham): kichik nishonlar
 * o'rniga bitta katta nishon — iOS ro'yxatlaridagi asosiy qoida. Havola
 * (`Link`) emas, tugma, chunki bosilganda ikki ish bajariladi — oyna
 * yopiladi va ekran ochiladi; `Link` da yopish `onClick` ga osilib
 * qolardi va o'rta tugma bilan «yangi oynada ochish» oynani ochiq
 * qoldirardi.
 *
 * Yosh o'ngda va u YO'Q bo'lishi mumkin (shubhali sana). Bunday holatda
 * joy bo'sh qoladi, «—» chizilmaydi: chiziqcha «yoshi noma'lum» degan
 * qo'shimcha ma'no yuklardi, holbuki bu shunchaki ko'rsatishga
 * arzimaydigan ma'lumot.
 */
function BirthdayRow({
  item,
  onOpen,
}: {
  item: DashboardBirthday
  onOpen: () => void
}) {
  const { t, num } = useLocale()

  /*
   * Lavozim va bo'lim bitta qatorda, nuqta bilan ajratiladi. Ikkalasi
   * ham bo'lmasligi mumkin (Excel'dan ko'chirilgan yozuvlarda odatiy
   * hol) — u holda ikkinchi qator umuman chizilmaydi va ism yolg'iz
   * qoladi. Bo'sh nuqta osilib qolmasligi uchun ro'yxat AVVAL
   * filtrlanadi, keyin birlashtiriladi.
   */
  const meta = [item.positionName, item.departmentName].filter(Boolean).join(' · ')

  return (
    <li>
      <button
        type="button"
        onClick={onOpen}
        title={t('dashboard.birthdaysOpen')}
        className={cn(
          'flex w-full items-center gap-3 rounded-md px-2 py-2.5 text-left',
          'press transition-colors duration-[120ms] ease-[var(--ease-ios)] hover:bg-[var(--bg-hover)]',
        )}
      >
        <EmployeePhoto
          employeeId={item.id}
          photoKey={item.photoKey}
          name={item.fullName}
          size="md"
        />

        <span className="min-w-0 flex-1">
          <span className="block truncate text-subhead font-semibold">{item.fullName}</span>

          {meta !== '' && (
            <span className="mt-0.5 block truncate text-footnote text-[var(--fg-secondary)]">
              {meta}
            </span>
          )}
        </span>

        {item.age !== null && (
          <span className="tabular shrink-0 text-footnote text-[var(--fg-tertiary)]">
            {t('dashboard.birthdaysAge', { n: num(item.age) })}
          </span>
        )}

        <ChevronRight
          aria-hidden
          className="size-4 shrink-0 text-[var(--fg-tertiary)]"
          strokeWidth={2.5}
        />
      </button>
    </li>
  )
}
