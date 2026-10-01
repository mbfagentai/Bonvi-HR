import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import {
  MCP_AUDIT_TAKE,
  checkMcp,
  fetchMcpAudit,
  fetchMcpConnections,
  fetchMcpStatus,
  revokeMcpConnection,
  setMcpEnabled,
  type McpStatus,
} from './mcp-api'

/**
 * «AI ulanish» kalitlari BITTA ildiz ostida: har qanday amaldan keyin
 * bo'limning hammasi qayta o'qilishi mumkin (ro'yxatlar kichik — yuztagacha
 * qator), lekin holat alohida ham yangilanadi, chunki o'chirgich va
 * «Qayta tekshirish» javobida u TAYYOR keladi.
 */
export const mcpKeys = {
  root: ['mcp'] as const,
  status: ['mcp', 'status'] as const,
  connections: ['mcp', 'connections'] as const,
  audit: ['mcp', 'audit', MCP_AUDIT_TAKE] as const,
}

/**
 * Ishga tushish tekshiruvi tugashini kutish oralig'i.
 *
 * `Pending` — server endigina ko'tarilgan va o'qish rolini tayyorlayapti
 * (bir necha soniya). Shu paytda sahifani ochgan odam qo'lda yangilamasin:
 * holat o'zi yetib keladi, keyin so'rov to'xtaydi.
 */
const PENDING_POLL_MS = 3000

/**
 * Holat va o'chirgich.
 *
 * `enabled` — ruxsat tekshiruvi CHAQIRUVCHIDA (`useApiKeys` dagi bilan bir
 * xil fikr): hamma `/api/mcp/*` `settings.manage` bilan himoyalangan va
 * ruxsatsiz odamda so'rov umuman yuborilmasligi kerak.
 */
export function useMcpStatus(enabled: boolean) {
  return useQuery({
    queryKey: mcpKeys.status,
    queryFn: ({ signal }) => fetchMcpStatus(signal),
    enabled,
    refetchInterval: (query) => (query.state.data?.state === 'Pending' ? PENDING_POLL_MS : false),
  })
}

export function useMcpConnections(enabled: boolean) {
  return useQuery({
    queryKey: mcpKeys.connections,
    queryFn: ({ signal }) => fetchMcpConnections(signal),
    enabled,
  })
}

export function useMcpAudit(enabled: boolean) {
  return useQuery({
    queryKey: mcpKeys.audit,
    queryFn: ({ signal }) => fetchMcpAudit(MCP_AUDIT_TAKE, signal),
    enabled,
  })
}

/**
 * Javobdagi YANGI holat keshga darhol yoziladi — o'chirgich qayta so'rovni
 * kutmasdan to'g'ri turadi. Keyin baribir qayta o'qiladi: boshqa oynada
 * ham shu sahifa ochiq bo'lishi mumkin.
 */
function useStatusWriter() {
  const queryClient = useQueryClient()

  return (status: McpStatus) => {
    queryClient.setQueryData(mcpKeys.status, status)
    void queryClient.invalidateQueries({ queryKey: mcpKeys.status })
  }
}

/** O'chirgich (`PUT /api/mcp/enabled`). */
export function useSetMcpEnabled() {
  const writeStatus = useStatusWriter()

  return useMutation({
    mutationFn: (enabled: boolean) => setMcpEnabled(enabled),
    onSuccess: writeStatus,
  })
}

/** «Qayta tekshirish» (`POST /api/mcp/check`) — faqat nosoz holatda taklif qilinadi. */
export function useCheckMcp() {
  const writeStatus = useStatusWriter()

  return useMutation({
    mutationFn: () => checkMcp(),
    onSuccess: writeStatus,
  })
}

/**
 * Uzish.
 *
 * Yozuv O'CHIRILMAYDI — u ro'yxatda kulrang bo'lib qoladi, shu sababli
 * ro'yxat qayta o'qiladi (server 204 qaytaradi, yangilangan qator emas).
 */
export function useRevokeMcpConnection() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (id: string) => revokeMcpConnection(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: mcpKeys.connections })
    },
  })
}
