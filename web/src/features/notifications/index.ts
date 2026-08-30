/**
 * Modulning tashqi yuzi — ATAYLAB ikkita komponent.
 *
 * Yuqori panelga faqat qo'ng'iroq kerak; ro'yxat, qatorlar va so'rovlar
 * uning ichki ishi. Hook'larni ham eksport qilish modulga tashqaridan
 * ikkinchi kirish yo'lini ochardi va «sanoq qayerdan olinadi» degan
 * savolga ikkita javob paydo bo'lardi (`features/auth/index.ts` bilan
 * bir xil qoida).
 *
 * <b>Nega ikkinchisi ham chiqarildi.</b> `LiveNotifications` — jonli
 * oqim va uning ekran tepasidagi xabari. U qo'ng'iroqning ichida
 * TURA OLMAYDI: qo'ng'iroq yuqori panelning bir bo'lagi, xabar esa
 * butun ekran ustida suzadi va u ilova qobig'iga bir marta
 * o'rnatilishi kerak (`app-shell.tsx`). Ikkalasini bitta komponentga
 * qo'shish panelning joylashuvini xabarning joylashuviga bog'lab
 * qo'yardi.
 */
export { NotificationsBell } from './components/notifications-bell'
export { LiveNotifications } from './components/notification-toast'
