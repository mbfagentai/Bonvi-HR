import { Ban, LoaderCircle } from 'lucide-react'
import { useEffect } from 'react'

import { problemCode } from '@/shared/api/problem-details'
import { useErrorMessage, useLocale } from '@/shared/i18n/use-locale'
import { Alert, Button, Dialog } from '@/shared/ui'

import type { McpConnection } from '../api/mcp-api'
import { useRevokeMcpConnection } from '../api/mcp-queries'

/**
 * Ulanishni uzishni tasdiqlash — `api-key-revoke-dialog.tsx` dagi bilan bir
 * xil naqsh.
 *
 * <b>Nega tasdiq kerak.</b> Ulanish DARHOL ishlamay qoladi va uni qaytarib
 * bo'lmaydi: AI ilovasi keyingi savolda ma'lumot ololmaydi, odam esa
 * qaytadan ulanib (telefon + parol + ruxsat) chiqishi kerak bo'ladi.
 *
 * ⚠️ Brauzerning `confirm()` si ISHLATILMAYDI (sababi o'sha faylda).
 */
export function McpRevokeDialog({
  item,
  onClose,
}: {
  item: McpConnection | null
  onClose: () => void
}) {
  const { t } = useLocale()
  const errorMessage = useErrorMessage()
  const revoke = useRevokeMcpConnection()

  useEffect(() => {
    if (item !== null) revoke.reset()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [item])

  if (item === null) return null

  const submit = () => {
    revoke.mutate(item.id, { onSuccess: onClose })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      size="sm"
      dismissible={!revoke.isPending}
      title={t('settings.mcp.revokeTitle')}
      description={`${item.userName} · ${item.clientName}`}
      footer={
        <>
          <Button type="button" onClick={onClose} disabled={revoke.isPending}>
            {t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant="danger"
            onClick={submit}
            disabled={revoke.isPending}
          >
            {revoke.isPending ? <LoaderCircle className="animate-spin" /> : <Ban />}
            {/*
              Tugma o'z obyektini nomlaydi («Ulanishni uzish»), yonidagi
              «Bekor qilish» (oynani yopish) bilan adashmasin — API kaliti
              oynasidagi bilan bir xil qoida.
            */}
            {revoke.isPending ? t('settings.mcp.revoking') : t('settings.mcp.revokeConfirm')}
          </Button>
        </>
      }
    >
      <div className="space-y-4">
        <p className="text-subhead text-[var(--fg-secondary)]">
          {t('settings.mcp.revokeText', { name: item.userName, client: item.clientName })}
        </p>

        {revoke.isError && <Alert tone="danger">{errorMessage(problemCode(revoke.error))}</Alert>}
      </div>
    </Dialog>
  )
}
