# Qabul qilingan farazlar

Foydalanuvchidan so'ralmagan, lekin qaror talab qilgan savollar.
Format: `- [sana] <savol> → <qaror> → <sabab>`

- [2026-09-06] Anketa holati qayerda saqlansin (20+ qadam, bot qayta ishga
  tushsa yo'qoladi)? → XOTIRADA qoladi, Redis qo'shilmaydi → botning
  «hech qanday saqlash tizimiga bog'lanmaslik» qoidasi kuchida
  (`ConversationStore` izohi); prod ishlab turganda yangi bog'liqlik
  qo'shish xavfi anketa yo'qolishi xavfidan katta. Anketa ~5 daqiqa,
  suhbat umri 60 daqiqa.
- [2026-09-06] Surat qadami majburiymi? → O'TKAZIB YUBORISH mumkin →
  selfi talab qilish eng ko'p tashlab ketiladigan qadam bo'lardi; HR
  uchun rasm foydali, lekin arizadan muhimroq emas.
- [2026-09-06] Anketa matnlari (uzunligi chegaradan oshsa) rad etilsinmi?
  → QIRQILADI, xato qaytarilmaydi → matnni yozgan odam ekran oldida
  turibdi va uzunlik xatosi butun anketani oxirgi qadamda yo'qotardi.
- [2026-09-06] Rus tili darajasi uchun yangi enum kerakmi? → YO'Q, mavjud
  `LanguageLevel` (xodim kompetensiyasi) qayta ishlatiladi → nomzod ishga
  qabul qilinganda daraja xodim kartasiga to'g'ridan-to'g'ri ko'chadi.
- [2026-09-06] Anketani HR tahrirlay oladimi? → YO'Q → bu nomzodning O'Z
  javoblari; tahrirlansa «nomzod nima deb aytdi» degan fakt yo'qolardi.
  HR fikri uchun `Candidate.Note` bor.
- [2026-09-06] «Umumiy anketa» ni bot ro'yxatidan qanday chiqarish kerak —
  `status=Open` so'ralganda HAR DOIM filtrlansinmi? → YO'Q, filtr ATAYLAB
  so'raladigan parametr bo'ldi (`?excludeGeneralPool=true`, uni faqat bot
  yuboradi) → HR ekranlari ham aynan shu `status=Open` ro'yxatidan
  foydalanadi (nomzod kiritish, boshqa vakansiyaga ko'chirish, vakansiyalar
  ekranidagi «Ochiq» filtri). Doimiy filtr HR ni nomzodni «Umumiy anketa» ga
  kirita olmaydigan qilib qo'yardi va sababi hech qayerda ko'rinmasdi.
- [2026-09-06] Anketa nomzod kartasida TO'RTINCHI tab bo'lsinmi? → YO'Q,
  «Umumiy» tabining ichida karta → anketa shaxsiy ma'lumotning davomi
  (SPEC 7-bo'limi «bo'lim» deydi, ekran emas); to'rtinchi tab HR ni ikkita
  ekran orasida yurishga majbur qilardi.
- [2026-09-06] Nomzod surati qayerda turadi? → SARLAVHA kartasida, xodim
  kartasidagi bilan aynan bir joyda → surat anketa javobi emas, nomzodning
  o'zi; anketasi yo'q nomzodda ham u ko'rinishi va yuklanishi kerak.
- [2026-09-06] Rekruting klienti `./bonvi gen` chiqargan hook'larga
  o'tkazilsinmi? → HOZIR EMAS → generatsiya ishga tushirildi va tiplar
  tekshirildi, lekin butun modulni ko'chirish anketa vazifasidan katta va
  ishlab turgan uchta ekranga tegardi. Anketa qo'lda yozilgan klientga
  QO'SHIMCHA sifatida kiritildi (o'sha fayldagi mavjud naqsh bilan).
- [2026-09-06] Nomzod surati keshining kaliti nima bo'ladi? → `hasPhoto`
  emas, faqat `candidateId` (`['candidate-photo', id]`), yangilash esa
  QO'LDA → `CandidateDto` saqlash kalitini ataylab qaytarmaydi, ya'ni
  xodim rasmidagi «yangi kalit — yangi kesh yozuvi» hiylasi ishlamaydi.
- [2026-09-06] `formatDate`/`todayIso` qaysi vaqt mintaqasida hisoblaydi?
  → BRAUZERNIKIDA EMAS, `Asia/Tashkent` da qat'iy → backend allaqachon
  `SystemClock.TashkentOffset` (UTC+5) bilan ishlaydi; boshqa mintaqadagi
  noutbukda yoki serverdagi brauzerda `todayIso()` forma orqali bazaga
  NOTO'G'RI kun yozardi.
- [2026-09-06] `employee-schemas.ts` dagi `toISOString().slice(0,10)` ham
  mintaqaga o'tkazilsinmi? → YO'Q, tegilmadi → u JSHSHIR ichidagi sanani
  TEKSHIRADI (`new Date('...T00:00:00Z')` bilan o'zi qurgan qiymatning
  aylanma nusxasi), ko'rsatmaydi. UTC ga qadalgani ataylab; mintaqaga
  o'tkazish yangi xato tug'dirardi.
- [2026-09-06] Ommaviy oferta havolasi sozlanmaganda rozilik vaqtiga nima
  yozilsin? → HECH NARSA, ustun `NULL` bo'ladi (migratsiya
  `OfertaRoziligiIxtiyoriy`) → dastlab bot yuborish lahzasini yozardi,
  ya'ni nomzod ko'rmagan ofertaga «rozilik berdi» degan huquqiy fakt
  bazaga tushardi. `NULL` = «so'ralmagan», qiymat = «aynan o'sha lahzada
  berilgan»; ikkalasi bir xil ko'rinmasligi kerak.

