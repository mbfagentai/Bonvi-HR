import { cva, type VariantProps } from 'class-variance-authority'
import type { ComponentPropsWithoutRef } from 'react'

import { cn } from '@/shared/lib/cn'

const button = cva(
  [
    'inline-flex items-center justify-center gap-2 font-medium whitespace-nowrap',
    /*
      Bosish javobi RANG bilan beriladi, o'lcham bilan emas — shu sababli
      bu yerda `press` (scale 0.97) yo'q.

      iOS HIG «Buttons»: tugma bosilganda «cho'kmaydi» va kichraymaydi —
      uning to'ldirilgan foni to'qlashadi. Kichrayish naqshi iOS'da boshqa
      narsaga tegishli (bosh ekrandagi ikonka), tugmaga emas. Qatorlar va
      kartalar uchun `.press` o'z o'rnida qoladi.

      120ms — Apple'ning boshqaruv javobi uchun odatiy oralig'i: barmoq
      ko'tarilganda holat darhol qaytadi, «kechikkan» tuyulmaydi.
    */
    'transition-colors duration-[120ms] ease-[var(--ease-ios)]',
    'disabled:pointer-events-none disabled:opacity-40',
    '[&>svg]:shrink-0',
  ],
  {
    variants: {
      variant: {
        /**
         * Asosiy harakat. Sahifada bittadan ko'p bo'lmasligi kerak.
         *
         * Soyasiz. iOS'da to'ldirilgan boshqaruv sirtga «yopishgan» bo'ladi:
         * HIG «Buttons» chuqurlikni rang bilan beradi, ko'lanka bilan emas.
         * Soya tugmani suzayotgan kartaga o'xshatib qo'yadi va aynan shu
         * narsa uni «xunuk» ko'rsatadi.
         */
        primary:
          'bg-[var(--accent)] text-[var(--fg-on-accent)] hover:bg-[var(--accent-hover)] active:bg-[var(--accent-pressed)]',
        /**
         * Yangi yozuv ochadigan amal: ishga qabul, ma'lumotnomaga qator
         * qo'shish, bo'lim yoki lavozim yaratish.
         *
         * NEGA `primary` dan alohida. Ekranda bir vaqtda bir nechta ko'k
         * tugma turadi («Saqlash», «Qidirish», «Ochish») va ular orasidan
         * «yangi yaratadi» ganini ajratish uchun HR yorliqni o'qishga
         * majbur bo'lardi. Yaratish — qaytarib bo'lmaydigan emas, lekin
         * ENG KO'P bosiladigan amal: u shaklidan tanilishi kerak.
         *
         * Farqi ikkita va ikkalasi ham ataylab:
         *   · doim TAXTA shaklida (`rounded-full`) — `pill` bayrog'iga
         *     bog'liq emas, chunki shakl bu yerda bezak emas, belgi;
         *   · yuzasi yorug'likli gradient va u YAGONA soyali tugma.
         *     Qolgan tugmalar sirtga yopishgan (izohi `primary` da),
         *     bu esa ataylab bir qadam ko'tarilgan.
         *
         * Rang, gradient va ko'lanka `.btn-create` da (globals.css):
         * uch qatlamli vergulli qiymatlar sinf nomida o'qilmasdi.
         */
        create: [
          'btn-create',
          /*
            Asosdagi `transition-colors` bu yerda foydasiz: fon — gradient,
            u rang emas. Sichqoncha javobi `.btn-create::after` qoplamasining
            shaffofligi orqali beriladi (uning o'z o'tishi bor), shu yerda
            esa faqat ko'lanka qoladi. O'tish xossasi ALMASHTIRILADI —
            tailwind-merge asosdagi qiymatni shu qator bilan almashtiradi
            (izohi `cn` da).
          */
          'transition-[box-shadow] duration-[120ms] ease-[var(--ease-ios)]',
        ].join(' '),
        /** Ikkilamchi — chegarali, oq. */
        secondary:
          'bg-[var(--bg-elevated)] border border-[var(--border-strong)] hover:bg-[var(--bg-hover)] active:bg-[var(--bg-active)]',
        /** Uchinchi darajali — fon yo'q. */
        ghost:
          'text-[var(--fg-secondary)] hover:bg-[var(--bg-hover)] hover:text-[var(--fg)] active:bg-[var(--bg-active)]',
        /**
         * Yumshoq tint — iOS'ning «tinted» tugmasi (filtr, yorliq).
         *
         * `brightness()` filtri ATAYLAB ishlatilmaydi: u butun elementga,
         * shu jumladan yorliq MATNIGA ham ta'sir qiladi va uni xiralashtiradi.
         * O'rniga fon tokeni almashadi — matn rangi tegilmasdan qoladi.
         */
        soft:
          'bg-[var(--accent-soft)] text-[var(--accent-on-soft)] hover:bg-[var(--accent-soft-hover)] active:bg-[var(--accent-soft-hover)]',
        /**
         * Qaytarib bo'lmaydigan amal. Soyasiz — `primary` bilan bir qoida.
         * Foni `--danger`: systemRed oq matn bilan AA dan o'tmaydi
         * (izohi globals.css da).
         */
        danger:
          'bg-[var(--danger)] text-[var(--fg-on-accent)] hover:bg-[var(--danger-hover)] active:bg-[var(--danger-pressed)]',
      },
      size: {
        sm: 'h-8  px-3   text-footnote rounded-sm  [&>svg]:size-4',
        md: 'h-10 px-4   text-subhead  rounded-md  [&>svg]:size-[18px]',
        lg: 'h-12 px-5   text-callout  rounded-md  [&>svg]:size-5',
        icon: 'size-10 rounded-md [&>svg]:size-[18px]',
        'icon-sm': 'size-8 rounded-sm [&>svg]:size-4',
      },
      pill: { true: 'rounded-full', false: '' },
    },
    /*
      Taxta shakli SHU YERDA, `create` variantining o'z satrida emas.

      Sabab cva'ning tartibida: variantlar e'lon qilingan ketma-ketlikda
      qo'shiladi, ya'ni `size` ning `rounded-md` si `variant` nikidan
      KEYIN turadi va tailwind-merge oxirgisini oladi — dumaloq chekka
      jimgina yo'qolardi. Birikma variant esa hammasidan keyin qo'shiladi.
    */
    compoundVariants: [{ variant: 'create', class: 'rounded-full' }],

    defaultVariants: { variant: 'secondary', size: 'md', pill: false },
  },
)

export function Button({
  className,
  variant,
  size,
  pill,
  ...props
}: ComponentPropsWithoutRef<'button'> & VariantProps<typeof button>) {
  return <button className={cn(button({ variant, size, pill }), className)} {...props} />
}

export { button as buttonVariants }
