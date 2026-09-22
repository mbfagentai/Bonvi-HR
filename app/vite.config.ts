import { fileURLToPath, URL } from 'node:url'

import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

/**
 * Xodim ilovasi — HR web ilovasidan BUTUNLAY alohida build.
 *
 * NEGA alohida: HR ilovasi Tailwind 4 + React 19 da ishlaydi, Tailwind 4 esa
 * Safari 16.4 talab qiladi. Xodimlarning telefonlari esa ancha eski —
 * pol iOS 12.2 / Chrome 80 (SPEC §G). Ikkalasi bitta build'da yashay
 * olmaydi, va eng muhimi: bu yerdagi xatolik ishlab turgan HR tizimiga
 * TEGMASLIGI kerak.
 */
export default defineConfig(({ mode }) => ({
  /*
    Ilova `/ilova/` ostida turadi — dev'da ham, production'da ham bir xil.
    Dev'da boshqa yo'lak ishlatilsa, service worker qamrovi va marshrut
    asosi (`basename`) faqat production'da sinalardi.
  */
  base: '/ilova/',

  plugins: [
    react(),

    VitePWA({
      /*
        `prompt` — ATAYLAB `autoUpdate` emas (UX §G.5): yangilanish
        belgilash so'rovi uchib turgan paytda sahifani qayta yuklasa,
        xodim «yuborildimi yoki yo'qmi» bilmay qolardi.
      */
      registerType: 'prompt',
      injectRegister: null,

      /*
        ⚠️ QAMROV — bitta qatorlik xato butun HR ilovasini keshlab
        qo'yishi mumkin (UX §G.1). Worker faqat `/ilova/` ostida
        ishlaydi va boshqa hamma navigatsiya deny-list'da.
      */
      scope: '/ilova/',
      workbox: {
        globPatterns: ['**/*.{js,css,html,png,svg,webmanifest}'],
        navigateFallback: '/ilova/index.html',
        navigateFallbackDenylist: [/^(?!\/ilova\/).*/],
        cleanupOutdatedCaches: true,
      },

      manifest: {
        name: 'Bonvi',
        short_name: 'Bonvi',
        description: 'Bonvi xodim ilovasi',
        start_url: '/ilova',
        scope: '/ilova/',
        display: 'standalone',
        orientation: 'portrait',
        lang: 'uz',
        /*
          Rang YORUG' mavzudan olinadi (UX §G.4): iOS splash ekranini
          tizim mavzusiga qarab almashtirmaydi, ya'ni tungi splash
          yorug' ilovadan oldin miltillab chiqardi.
        */
        theme_color: '#FFFFFF',
        background_color: '#FFFFFF',
        icons: [
          { src: 'icons/icon-192.png', sizes: '192x192', type: 'image/png' },
          { src: 'icons/icon-512.png', sizes: '512x512', type: 'image/png' },
          {
            src: 'icons/icon-maskable-512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },

      devOptions: {
        // Dev'da worker o'chirilgan: eski bundle keshda qolib, o'zgarish
        // ko'rinmay qolishi — bu bosqichda eng keraksiz vaqt yo'qotish.
        enabled: false,
      },
    }),
  ],

  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },

  server: {
    // Konteyner ichidan tashqariga ochilishi uchun — `localhost` bo'lsa
    // port tashqaridan javob bermaydi.
    host: true,
    port: 7174,
    strictPort: true,
    watch: {
      // macOS + Docker bind-mount inotify hodisalarini uzatmaydi
      usePolling: true,
      interval: 1000,
      binaryInterval: 3000,
      ignored: ['**/node_modules/**', '**/dist/**', '**/.git/**'],
    },
  },

  build: {
    outDir: 'dist',
    /*
      ⚠️ POL — SPEC §G va UX §F.3. Bu qator olib tashlansa, ilova yangi
      telefonlarda ishlaydi va eski telefonlarda OQ EKRAN bo'lib qoladi;
      xatoni esa faqat mijoz ko'radi.
    */
    target: ['es2017', 'safari12', 'chrome80'],
    sourcemap: mode !== 'production',
    chunkSizeWarningLimit: 600,
  },
}))
