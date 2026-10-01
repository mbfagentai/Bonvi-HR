# Bonvi HR — AI orqali o'qish (MCP)

Rahbarlar uchun qisqa qo'llanma: Claude yoki ChatGPT'ga Bonvi HR ma'lumoti
bo'yicha oddiy tilda savol berish.

> **Faqat o'qish.** AI hech narsani o'zgartira olmaydi: ishga qabul qila
> olmaydi, kartani tahrirlay olmaydi, hech narsani o'chira olmaydi. Bunday
> imkoniyat tizimda umuman yo'q.

---

## 1. Bu nima

MCP — AI yordamchini tashqi tizimga ulash usuli. Ulangandan keyin siz
Claude'ga «Shu oyda kimning sinov muddati tugaydi?» deb yozasiz, u Bonvi HR'dan
kerakli ma'lumotni o'qib, javob beradi.

AI tizimga **sizning nomingizdan** kiradi va **faqat sizning rolingiz
ko'rsatadigan** ma'lumotni ko'radi. Masalan, rolingizda oklad ko'rish ruxsati
bo'lmasa, AI ham okladni ko'rmaydi.

## 2. Nima mumkin, nima mumkin emas

| AI ko'ra oladi (rolingiz ruxsat bersa) | AI hech qachon ko'rmaydi |
|---|---|
| Xodimlar: ism, telefon, manzil, lavozim, bo'lim, tillar, belgilar | Pasport seriyasi va raqami |
| Buyruqlar, sinov muddatlari, ishdan ketish sabablari | JSHSHIR (PINFL) |
| Oklad va qo'shimcha haq (alohida ruxsat bilan) | Yuklangan fayllar: hujjatlar, rasmlar, skanlar |
| Rag'bat va jarimalar, baxtsiz hodisalar, qayta aloqa | Login, parol, tokenlar |
| Vakansiyalar, nomzodlar, anketalar, voronka | Hech narsani o'zgartirish yoki o'chirish |
| Davomat, kechikishlar, yo'qlik so'rovlari | Rolingiz yopgan bo'limlar |

Har bir so'rov jurnalga yoziladi: kim, qachon, qaysi AI orqali, nimani
so'radi. Jurnalni tizim administratori ko'radi.

## 3. Kim ulana oladi

1. Tizim administratori **Tizim sozlamalari → AI ulanish (MCP)** bo'limida
   ulanishni yoqadi.
2. Rolingizga **«AI orqali o'qish (MCP)»** ruxsati berilgan bo'lishi kerak
   (**Rollar** bo'limida).

Ikkalasi bo'lmasa, ulanish oynasi kirishni rad etadi.

Ulanish manzili — **Tizim sozlamalari → AI ulanish (MCP)** dagi «Ulanish
manzili» maydonida. Odatda bu:

```
https://hr.bonvi.uz/mcp
```

## 4. Claude'da ulash

### Kompyuterda (claude.ai)

1. claude.ai ga kiring → **Settings → Connectors**.
2. **Add custom connector** ni bosing.
3. Nom: `Bonvi HR`. Manzil: yuqoridagi ulanish manzili. **Add** ni bosing.
4. **Connect** ni bosing — Bonvi HR'ning kirish oynasi ochiladi.
5. Bonvi HR'dagi **telefon raqamingiz va parolingiz** bilan kiring.
6. **Ruxsat berish** ni bosing. Oyna yopiladi va Claude'ga qaytasiz.

Endi suhbatda savol bering. Kerak bo'lsa, suhbat oynasidagi vositalar
(«Search and tools») menyusida Bonvi HR yoqilganini tekshiring.

> **Team / Enterprise rejasida** maxsus konnektorni avval tashkilot egasi
> (Owner) admin sozlamalarida qo'shadi. Keyin har bir a'zo o'zi **Connect**
> ni bosib, o'z Bonvi HR login'i bilan kiradi.

### Telefonda (Claude ilovasi)

Konnektorni telefonda qo'shib bo'lmasligi mumkin — uni **bir marta
kompyuterda** (yuqoridagi tartibda) qo'shing. Shundan keyin u o'sha Claude
hisobidagi iOS va Android ilovalarida ham ishlaydi.

## 5. ChatGPT'da ulash

Faqat **rejangiz maxsus konnektorlarni (custom connectors) qo'llasa**.
Menyu nomlari ChatGPT versiyasiga qarab biroz farq qilishi mumkin.

1. **Settings → Apps & Connectors** (yoki **Connectors**) ni oching.
   Shaxsiy rejalarda avval **Advanced → Developer mode** ni yoqish kerak
   bo'lishi mumkin; Business/Enterprise'da konnektorni ish maydoni
   administratori qo'shadi.
2. **Create** (yoki **Add**) ni bosing: nom `Bonvi HR`, manzil — ulanish
   manzili, autentifikatsiya — **OAuth**.
3. Bonvi HR oynasida telefon va parolingiz bilan kiring, **Ruxsat berish** ni
   bosing.

## 6. Savollar namunasi

- «Toshkent filialidagi «Sotuv menejeri» vakansiyasi uchun rezervda turgan,
  rus tilini biladigan nomzodlarni top.»
- «Bu yil bo'limlar kesimida kadrlar almashinuvi qanday? Eng ko'p ketgan
  bo'lim qaysi va asosiy sabablar nima?»
- «Shu oyda kimning sinov muddati tugaydi?»
- «Bugun kim ishga kechikdi?»
- «Qaysi faol xodimlarda majburiy hujjat yetishmayapti?»
- «Keyingi 7 kunda kimning tug'ilgan kuni bor?»

Javobdagi muhim raqamlarni (oklad, sanalar) qaror qabul qilishdan oldin Bonvi
HR panelida tekshiring — AI ham adashishi mumkin.

## 7. Ulanishni uzish

- **O'zingiz:** Claude'da **Settings → Connectors → Bonvi HR → Disconnect**
  (ChatGPT'da — o'sha konnektor sozlamasidan).
- **Administrator:** **Tizim sozlamalari → AI ulanish (MCP) → Ulangan
  hisoblar** → kerakli qatorda **Uzish**. Ulanish darhol to'xtaydi.
- **Hammasini birdan:** o'sha bo'limdagi o'chirgichni o'chirish. Ulanishlar
  ro'yxatda qoladi, lekin hech biri ma'lumot ololmaydi.

Ulanish o'zi ham to'xtaydi: 90 kundan keyin (qayta ulanasiz), parolingiz
tiklansa, «barcha sessiyalardan chiqish» bosilsa, hisobingiz faolsizlantirilsa
yoki rolingizdan «AI orqali o'qish (MCP)» ruxsati olinsa.

**Parolingiz begona odamga ma'lum bo'lib qolgan deb o'ylasangiz** — tizim
egasidan parolni tiklashni so'rang: bu barcha veb-sessiyalar bilan birga AI
ulanishlarini ham uzadi.

## 8. Maxfiylik

AI'ga bergan savolingiz va unga Bonvi HR'dan o'qilgan ma'lumot **AI
provayderining serverlariga** (Anthropic yoki OpenAI) yuboriladi.

- **Team yoki Enterprise** (ChatGPT'da **Business / Enterprise**) rejasidan
  foydalaning — bu rejalarda suhbatlar sukut bo'yicha model o'qitishga
  ishlatilmaydi.
- Shaxsiy rejada bo'lsangiz, maxfiylik sozlamasida ma'lumotni model
  yaxshilashga berishni **o'chiring**.
- Faqat ishingiz uchun kerakli narsani so'rang. Har bir so'rov Bonvi HR
  jurnalida qoladi.

### Ehtiyot chorasi: nomzod yozgan matnlar

Nomzodlar anketani ochiq Telegram bot orqali to'ldiradi, ya'ni ularning
matnini istalgan odam yoza oladi. Bonvi HR AI'ga bu matnni faqat MA'LUMOT
sifatida beradi va AI tizimda hech narsani o'zgartira olmaydi. Lekin AI
suhbatida boshqa imkoniyatlar ham yoqilgan bo'lsa (internetda qidirish, pochta,
Google Drive, xat yuborish kabi boshqa konnektorlar), matnga yashirilgan
«buyruq» AI'ni o'qigan ma'lumotini boshqa joyga yuborishga undashi mumkin.

- Bonvi HR konnektorini **alohida suhbatda yoki loyihada** ishlating — u yerda
  internet qidiruvi va yozish huquqi bor boshqa konnektorlar yoqilmagan bo'lsin.
- AI javobida kutilmagan havola yoki «shu manzilga yuboring» degan taklif
  chiqsa — bosmang, tizim egasiga xabar bering.

