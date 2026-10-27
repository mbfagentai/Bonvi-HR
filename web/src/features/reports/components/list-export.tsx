import { FileSpreadsheet, LoaderCircle } from 'lucide-react'

import { employeeErrorCode, isRateLimited } from '@/features/employees/lib/employee-error'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button } from '@/shared/ui'

/**
 * «Excelga yuklash» tugmasi — hamma ro'yxatda bir xil ko'rinish va joy:
 * filtrlar qatorida, «Filtrlar» yonida (xodimlar ro'yxatidagi kabi — fayl
 * aynan shu filtrga tushadi).
 */
export function ListExportButton({
  onClick,
  pending,
  disabled = false,
  labelClassName = 'hidden lg:inline',
}: {
  onClick: () => void
  pending: boolean
  disabled?: boolean
  /**
   * Yorliq qachon ko'rinadi. Sukut — `lg` dan. Asboblari zich qatorda
   * (davomat statistikasi) kengroq chegara beriladi: aks holda tugma
   * qatorni ikkiga bo'lib yuborardi.
   */
  labelClassName?: string
}) {
  const { t } = useLocale()

  return (
    <Button
      type="button"
      pill
      onClick={onClick}
      // Ikki marta bosilsa ikkita fayl yuklanardi va ikkinchisi tezlik cheklovini bekorga yerdi
      disabled={pending || disabled}
      title={t('common.export')}
      aria-label={t('common.export')}
    >
      {pending ? <LoaderCircle className="animate-spin" /> : <FileSpreadsheet />}
      {/* Yorliq faqat keng ekranda: tor oynada u qidiruv maydonini siqib qo'yardi */}
      <span className={labelClassName}>{pending ? t('employees.exporting') : t('common.export')}</span>
    </Button>
  )
}

/**
 * Eksport xatosi — tugma turgan kartaning ichida (xodimlar ro'yxatidagi kabi).
 * 429 alohida: uning yagona yechimi — biroz kutish, filtrni o'zgartirish emas.
 */
export function ListExportError({ error, className }: { error: unknown; className?: string }) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()

  if (error === null || error === undefined) return null

  return (
    <div className={className ?? 'border-b border-[var(--border)] px-4 py-3.5 sm:px-5'}>
      <Alert tone="danger" title={t('employees.exportFailed')}>
        {isRateLimited(error) ? t('employees.exportTooOften') : errorMessage(employeeErrorCode(error))}
      </Alert>
    </div>
  )
}
