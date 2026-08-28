import { MutationCache, QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ReactQueryDevtools } from '@tanstack/react-query-devtools'
import { type ReactNode, useState } from 'react'

import { AuthProvider } from '@/features/auth'
import { ThemeProvider } from '@/shared/theme/theme-provider'

// i18n yon ta'sir sifatida ishga tushadi (til aniqlanadi va yuklanadi)
import '@/shared/i18n'

/**
 * Rasm keshlari — ular UMUMIY BEKOR QILISHDAN CHETLAB O'TILADI.
 *
 * Uchala nom maydoni ham binar `blob` saqlaydi: `employee-photo`,
 * `user-photo`, `account-photo`. Ular faqat O'Z amali bilan o'zgaradi
 * (rasm yuklash/o'chirish) va o'sha amallar o'z kalitini o'zi bekor qiladi.
 *
 * NEGA chetlab o'tish SHART: xodimlar ro'yxatida bir vaqtda 50 tagacha rasm
 * so'rovi faol turadi. Ular ham umumiy bekor qilishga tushsa, har qanday
 * yozuv amali (masalan bitta jarima qo'shish) 50 ta rasmni qaytadan
 * yuklatib yuborardi — foydasi nol, narxi katta.
 */
const RASM_KESHI = /-photo$/

/**
 * QueryClient.
 *
 * ⭐ HAR QANDAY MUVAFFAQIYATLI YOZUV AMALIDAN KEYIN BUTUN KESH BEKOR
 * QILINADI (rasmlardan tashqari).
 *
 * NEGA bu «qo'pol» yechim ATAYLAB tanlangan. Ilgari har bir mutatsiya
 * qaysi keshlarni bekor qilishni QO'LDA sanab turardi — 40 dan ortiq
 * chaqiruv. Bunday tuzilishda har safar yangi ekran qo'shilganda, unga
 * ta'sir qilishi mumkin bo'lgan HAMMA mutatsiyani eslab, ro'yxatga qo'lda
 * qo'shish kerak bo'ladi. Bu albatta unutiladi va aynan shunday bo'ldi:
 * xodim kartasidan rag'bat qo'shilganda `/ragbat` ro'yxati eskirib
 * qolardi — sahifa yangilanmaguncha yangi yozuv ko'rinmasdi. Xuddi shu
 * xato `/buyruqlar`, bosh sahifa raqamlari va `/hisobotlar` ni ham
 * kutayotgan edi: ularning hech biri yozuv amallari ro'yxatida yo'q edi.
 *
 * Xato SINFINI yo'qotish uchun qoida teskarisiga o'girildi: «nima
 * eskirishini sanab chiq» o'rniga «hammasi eskirdi deb hisobla».
 *
 * NEGA BU QIMMAT EMAS. `invalidateQueries` keshni eskirgan deb
 * BELGILAYDI, lekin faqat FAOL (ekranda turgan) so'rovlarni qaytadan
 * yuklaydi. Bitta ekranda odatda 1–3 ta faol so'rov bo'ladi — ya'ni
 * haqiqiy narx bir necha so'rov, butun kesh emas. Tizimdan bir vaqtda
 * 4 kishi foydalanadi va yozuv amallari kunda o'nlab marta bajariladi,
 * ya'ni bu yuk umuman sezilmaydi.
 *
 * Mutatsiyalardagi MAVJUD `invalidateQueries` chaqiruvlari OLIB
 * TASHLANMADI. Ikki sabab: ular «bu amal nimaga ta'sir qiladi» degan
 * niyatni o'z joyida hujjatlaydi, va rasm kalitlari uchun ular YAGONA
 * yo'l — yuqoridagi umumiy qoida rasmlarni ataylab chetlab o'tadi.
 */
function queryClientYarat(): QueryClient {
  const mutationCache = new MutationCache({
    onSuccess: (_data, _variables, _context, mutation) => {
      // Serverda hech narsani o'zgartirmaydigan amallar (masalan Excel
      // eksporti — u mutatsiya shaklida yozilgan, chunki tugma bosilganda
      // ishga tushadi) keshni bekor qilishi ma'nosiz.
      if (mutation.meta?.['readOnly'] === true) return

      void client.invalidateQueries({
        predicate: (query) => !RASM_KESHI.test(String(query.queryKey[0])),
      })
    },
  })

  const client = new QueryClient({
    mutationCache,
    defaultOptions: {
      queries: {
        // HR ma'lumoti tez-tez o'zgarmaydi — 1 daqiqa kesh maqbul
        staleTime: 60_000,
        retry: 1,
        refetchOnWindowFocus: false,
      },
      mutations: {
        retry: 0,
      },
    },
  })

  return client
}

/**
 * Ilova bo'ylab umumiy kontekstlar.
 * QueryClient useState ichida yaratiladi — qayta render'da yangisi hosil bo'lmasligi uchun.
 */
export function Providers({ children }: { children: ReactNode }) {
  const [queryClient] = useState(queryClientYarat)

  return (
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        {/* Auth so'rovlari ham QueryClient orqali o'tishi mumkin,
            shu sababli u ichkarida turadi */}
        <AuthProvider>
          {children}
          {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
        </AuthProvider>
      </QueryClientProvider>
    </ThemeProvider>
  )
}
