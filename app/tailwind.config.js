/**
 * Tailwind 3.4 — ATAYLAB 4 emas.
 *
 * Tailwind 4 `@property`, `@layer` va `color-mix()` ustiga qurilgan; uchalasi
 * ham Safari 16.4 dan boshlab ishlaydi, bizning pol esa iOS 12.2 (UX §F.2).
 * Shu sababli ranglar ham hisoblanmaydi — har bir token `tokens.css` da
 * TAYYOR hex bo'lib yotadi, bu yerda faqat unga nom beriladi.
 *
 * @type {import('tailwindcss').Config}
 */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],

  // Mavzu `data-theme` atributi bilan boshqariladi: tizim tanlovi ham
  // JS da hal qilinadi (`theme.tsx`), CSS da emas — aks holda «tizimdagi
  // kabi» va «qo'lda tanlangan» qoidalari bir-birining ustiga tushardi.
  darkMode: ['selector', '[data-theme="dark"]'],

  theme: {
    extend: {
      colors: {
        bg: 'var(--bg)',
        'bg-elevated': 'var(--bg-elevated)',
        'bg-sunken': 'var(--bg-sunken)',
        'bg-pressed': 'var(--bg-pressed)',

        fg: 'var(--fg)',
        'fg-secondary': 'var(--fg-secondary)',
        'fg-tertiary': 'var(--fg-tertiary)',
        'fg-on-accent': 'var(--fg-on-accent)',

        border: 'var(--border)',
        'border-strong': 'var(--border-strong)',

        accent: 'var(--accent)',
        'accent-pressed': 'var(--accent-pressed)',
        'accent-text': 'var(--accent-text)',
        'accent-soft': 'var(--accent-soft)',
        'accent-on-soft': 'var(--accent-on-soft)',

        danger: 'var(--danger)',
        'danger-text': 'var(--danger-text)',
        ring: 'var(--ring)',

        // Holat ranglari (UX §D.4): nuqta uchun «hue», fon uchun «tint».
        // Matn HAR DOIM `--fg` — shu sababli tintlar uchun matn rangi yo'q.
        'hue-late': 'var(--hue-late)',
        'hue-absent': 'var(--hue-absent)',
        'hue-excused': 'var(--hue-excused)',
        'hue-dayoff': 'var(--hue-dayoff)',
        'hue-dayoff-worked': 'var(--hue-dayoff-worked)',
        'hue-success': 'var(--hue-success)',

        'tint-late': 'var(--tint-late)',
        'tint-absent': 'var(--tint-absent)',
        'tint-excused': 'var(--tint-excused)',
        'tint-dayoff': 'var(--tint-dayoff)',
        'tint-dayoff-worked': 'var(--tint-dayoff-worked)',
        'tint-success': 'var(--tint-success)',
      },

      borderRadius: {
        chip: '8px',
        control: '12px',
        card: '16px',
        sheet: '20px',
        hero: '28px',
        pill: '999px',
      },

      fontSize: {
        caption2: ['0.6875rem', { lineHeight: '1.4' }],
        caption1: ['0.75rem', { lineHeight: '1.4' }],
        footnote: ['0.8125rem', { lineHeight: '1.4' }],
        subhead: ['0.9375rem', { lineHeight: '1.4' }],
        callout: ['1rem', { lineHeight: '1.4' }],
        body: ['1.0625rem', { lineHeight: '1.4' }],
        title3: ['1.25rem', { lineHeight: '1.2' }],
        title2: ['1.375rem', { lineHeight: '1.2' }],
        title1: ['1.75rem', { lineHeight: '1.2' }],
        hero: ['2.125rem', { lineHeight: '1.2' }],
      },

      fontFamily: {
        // Veb-shrift YO'Q: iOS da SF Pro, Android da Roboto (UX §D.7).
        sans: [
          '-apple-system',
          'BlinkMacSystemFont',
          '"SF Pro Text"',
          '"Segoe UI"',
          'Roboto',
          'system-ui',
          'sans-serif',
        ],
      },

      transitionTimingFunction: {
        ios: 'cubic-bezier(0.25, 0.1, 0.25, 1)',
      },

      maxWidth: {
        // Bitta ustun, 480 px da to'xtaydi (UX §F.5)
        app: '480px',
      },
    },
  },

  plugins: [],
}
