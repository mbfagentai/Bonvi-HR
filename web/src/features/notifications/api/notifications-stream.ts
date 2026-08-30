import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'

import { api } from '@/shared/api/http-client'
import { authStorage } from '@/shared/lib/auth-storage'
import { isRecentLocalWrite } from '@/shared/lib/local-write'

import { toNotificationList, type AppNotification, type NotificationList } from './notifications-api'
import { NOTIFICATION_LIMIT, notificationKeys } from './notifications-queries'

/**
 * Uzilgandan keyin qayta ulanishning eng qisqa va eng uzun kutishi.
 *
 * Kutish har urinishda IKKI BAROBAR ortadi. Nega kerak: server qayta
 * ishga tushayotgan bo'lsa (dev'da `dotnet watch` buni kuniga o'nlab
 * marta qiladi), doimiy bir soniyalik urinish jurnalni to'ldirardi.
 * Yuqori chegara esa 30 soniya — undan uzoq kutish «tarmoq qaytdi,
 * lekin ilova hamon jim» degan holatga olib kelardi.
 */
const RECONNECT_MIN_MS = 1_000
const RECONNECT_MAX_MS = 30_000

/**
 * Bildirishnomalarning JONLI oqimi.
 *
 * ⭐ <b>Qanday muammoni yechadi.</b> Ilgari qo'ng'iroq ustidagi son bir
 * daqiqada bir marta so'ralardi, ro'yxat esa faqat oyna ochilganda.
 * Ya'ni bot orqali ariza tushganda ish bilan band HR buni BILMASDI —
 * sahifani yangilagandan keyingina ko'rardi.
 *
 * ⭐ <b>Nega `EventSource` emas</b> (SSE uchun brauzerning tayyor
 * vositasi). U sarlavha qo'sha olmaydi, ya'ni `Authorization: Bearer …`
 * ni yubora olmaydi. Tokenni manzil satriga qo'yish esa mumkin emas:
 * u brauzer tarixiga, proksi jurnaliga va server jurnaliga ochiq matn
 * bo'lib tushardi. `fetch` + oqim o'quvchisi sarlavhani odatdagidek
 * yuboradi.
 *
 * ⭐ <b>Ishlayotgan ishga TEGMAYDI.</b> Bu yerda `invalidateQueries`
 * UMUMAN chaqirilmaydi — faqat ikkita bildirishnoma kaliti
 * `setQueryData` bilan to'g'ridan-to'g'ri yangilanadi. Sabab:
 * `providers.tsx` dagi umumiy bekor qilish har qanday yozuvdan keyin
 * BUTUN keshni eskirtiradi, ya'ni bu yerda ishlatilsa, boshqa
 * foydalanuvchining harakati HR ning ekranidagi ro'yxatlarni qayta
 * yuklatib yuborardi — to'ldirilayotgan forma ostidan sahifa
 * qimirlardi. Bildirishnoma hech qachon boshqa ishni to'xtatmasligi
 * kerak.
 *
 * @param enabled Foydalanuvchi tizimga kirganmi. `false` bo'lsa ulanish
 *   umuman ochilmaydi (kirish sahifasida oqimning ma'nosi yo'q).
 * @param onNew YANGI paydo bo'lgan o'qilmagan bildirishnomalar. Birinchi
 *   javobda HECH QACHON chaqirilmaydi (sababi pastda).
 */
export function useNotificationStream(
  enabled: boolean,
  onNew: (items: AppNotification[]) => void,
): void {
  const queryClient = useQueryClient()

  /*
    `onNew` har render'da yangi funksiya bo'lishi mumkin. Uni effekt
    bog'liqligiga qo'shsak, ulanish har render'da uzilib qayta
    ochilardi. Shu sababli u havolada saqlanadi va effekt faqat
    `enabled` o'zgarganda qayta ishga tushadi.
  */
  const latestOnNew = useRef(onNew)
  latestOnNew.current = onNew

  useEffect(() => {
    if (!enabled) return

    const controller = new AbortController()

    /*
      Ko'rilgan kalitlar.

      BIRINCHI javob shu to'plamni to'ldiradi va hech qanday xabar
      bermaydi. Nega shart: ulanish sahifa har ochilganda qayta
      quriladi va boshlang'ich javobda o'nlab eski bildirishnoma
      bo'lishi mumkin. Ularning hammasini «yangi keldi» deb ko'rsatish
      ekranni bosib ketardi va bir kunda odam bu xabarlarga umuman
      qaramay qo'yardi — qo'ng'iroqning eski, doim yonib turadigan
      qizil nuqtasi bilan bir xil kasallik.
    */
    const seen = new Set<string>()
    let primed = false

    let retry = RECONNECT_MIN_MS
    let timer: number | undefined

    const apply = (list: NotificationList) => {
      queryClient.setQueryData(notificationKeys.list(NOTIFICATION_LIMIT), list)
      queryClient.setQueryData(notificationKeys.unreadCount, list.unreadCount)

      const unread = list.items.filter((item) => !item.isRead)

      if (!primed) {
        primed = true
        for (const item of unread) seen.add(item.key)
        return
      }

      const fresh = unread.filter((item) => !seen.has(item.key))
      for (const item of unread) seen.add(item.key)

      /*
        O'ZIMIZ qilgan o'zgarish uchun xabar chiqarilmaydi: HR nomzod
        qo'shib «Saqlash» bosgan zahoti «Yangi nomzod: …» degan karta
        tushishi — bo'sh shovqin va aynan ishlayotgan odamni bo'lish
        (sabab `local-write.ts` da). Kalitlar yuqorida ALLAQACHON
        «ko'rilgan» deb belgilangan, ya'ni ular keyinroq ham qayta
        chiqmaydi.
      */
      if (fresh.length > 0 && !isRecentLocalWrite()) latestOnNew.current(fresh)
    }

    /**
     * Bitta SSE «ramka»sini o'qiydi.
     *
     * Ramka bo'sh qator bilan tugaydi; ichida `event:` va `data:`
     * qatorlari bo'ladi. `:` bilan boshlangan qator — IZOH (server
     * «tirikman» belgisi) va u e'tiborga olinmaydi.
     */
    const handleFrame = (frame: string) => {
      let event = 'message'
      const data: string[] = []

      for (const line of frame.split('\n')) {
        if (line.startsWith(':')) continue
        if (line.startsWith('event:')) event = line.slice(6).trim()
        else if (line.startsWith('data:')) data.push(line.slice(5).trim())
      }

      if (event !== 'notifications' || data.length === 0) return

      try {
        apply(toNotificationList(JSON.parse(data.join('\n'))))
      } catch {
        // Buzilgan ramka butun ulanishni yiqitmaydi: keyingisi to'g'ri
        // bo'lishi mumkin va oqim baribir to'liq holatni yuboradi.
      }
    }

    const connect = async (): Promise<void> => {
      const token = authStorage.getAccessToken()
      if (token === null) return

      const response = await fetch(`${api.defaults.baseURL}/api/notifications/stream`, {
        headers: { Authorization: `Bearer ${token}`, Accept: 'text/event-stream' },
        signal: controller.signal,
      })

      if (response.status === 401) {
        /*
          Token eskirgan. Uni SHU YERDA yangilamaymiz — yangilash
          mantig'i (bir vaqtda bitta urinish, muvaffaqiyatsizlikda
          kirish sahifasiga qaytarish) `http-client.ts` da va uning
          nusxasi ikkita bo'lishi mumkin emas. O'rniga eng arzon
          odatiy so'rov yuboriladi: axios uni 401 bilan ushlab,
          tokenni yangilaydi. Keyingi urinishda oqim yangi token
          bilan ochiladi.
        */
        await api.get('/api/notifications/unread-count').catch(() => undefined)
        throw new Error('stream unauthorized')
      }

      if (!response.ok || response.body === null) {
        throw new Error(`stream failed: ${response.status}`)
      }

      // Ulanish ochildi — keyingi uzilishda yana qisqa kutishdan
      // boshlanadi.
      retry = RECONNECT_MIN_MS

      const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()

      // Ramkalar bo'laklarga bo'lingan holda keladi: chala qolgani
      // keyingi bo'lakka qo'shiladi.
      let buffer = ''

      for (;;) {
        const { value, done } = await reader.read()
        if (done) break

        buffer += value

        let split = buffer.indexOf('\n\n')
        while (split !== -1) {
          handleFrame(buffer.slice(0, split))
          buffer = buffer.slice(split + 2)
          split = buffer.indexOf('\n\n')
        }
      }
    }

    const run = () => {
      connect().catch(() => undefined).finally(() => {
        if (controller.signal.aborted) return

        timer = window.setTimeout(run, retry)
        retry = Math.min(retry * 2, RECONNECT_MAX_MS)
      })
    }

    run()

    return () => {
      controller.abort()
      if (timer !== undefined) window.clearTimeout(timer)
    }
  }, [enabled, queryClient])
}
