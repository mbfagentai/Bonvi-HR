import { createFileRoute } from '@tanstack/react-router'

import { PipelineStagesPage } from '@/features/recruiting/components/pipeline-stages-page'

/**
 * Voronka bosqichlari — ma'lumotnomalar bo'limining to'qqizinchi sahifasi.
 *
 * NEGA u `features/catalog` da EMAS, `features/recruiting` da: yozuv
 * `/api/recruiting/stages` da yashaydi, o'z xato kodlarini
 * (`bosqich.*`) qaytaradi va uning TARTIBI tahrirlanadi — sakkizta
 * ma'lumotnomaning birortasida bunday narsa yo'q. Ma'lumotnomalar
 * moduliga rekruting haqidagi bilimni olib kirish o'rniga, sahifa o'z
 * modulida qoladi va bu yerda faqat menyudagi joyi belgilanadi
 * (batafsil sabab `pipeline-stage-dialog.tsx` izohida).
 */
export const Route = createFileRoute('/_app/malumotnomalar/bosqichlar')({
  component: PipelineStagesPage,
})
