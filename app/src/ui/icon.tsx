import clsx from 'clsx'

/**
 * Yagona nishon to'plami — chiziqli uslub, 24 px, qalinlik 1.5
 * (UX §E.8). Yigirma to'rtta nishon: UX dagi o'n beshta, «So'rov turini
 * tanlang» varag'ining yettitasi (SPEC-erkin-jadval §7.2 — Verifix'dagi
 * sakkiz turning rasmlariga yaqin, `map-pin` umumiy) va «Jamoa» ning ikkitasi
 * (SPEC-jamoam §F — tab va qo'ng'iroq). Yangisi kerak bo'lsa,
 * avval ekranning o'zi haqiqatan yangimi — shu savol.
 *
 * ⚠️ Nishon HECH QACHON amalning yagona yorlig'i bo'lmaydi (UX §H.3):
 * yonida so'z turadi yoki `aria-label` beriladi.
 */
export type IconName =
  | 'home'
  | 'calendar'
  | 'document-text'
  | 'user'
  | 'arrow-right-on-rectangle'
  | 'arrow-left-on-rectangle'
  | 'map-pin'
  | 'clock'
  | 'chevron-left'
  | 'chevron-right'
  | 'check-circle'
  | 'exclamation-triangle'
  | 'x-circle'
  | 'information-circle'
  | 'cog'
  // So'rov turlari varag'i (SPEC-erkin-jadval §7.2)
  | 'person-running'
  | 'arrows-right-left'
  | 'calendar-pencil'
  | 'person-walking-arrows'
  | 'briefcase-clock'
  | 'user-minus'
  | 'beach-umbrella'
  // «Jamoa» (SPEC-jamoam §F): tab va a'zoga qo'ng'iroq
  | 'users'
  | 'phone'

const PATHS: Record<IconName, string> = {
  home: 'M3 10.5 12 3l9 7.5M5.5 9.5V20h13V9.5M9.5 20v-6h5v6',
  calendar:
    'M4 6.5h16v14H4zM4 10.5h16M8.5 3.5v4M15.5 3.5v4',
  'document-text': 'M6.5 3.5h7l4.5 4.5v12h-11.5zM13.5 3.5v5h4.5M9 13h6M9 16.5h6',
  user: 'M12 12a3.75 3.75 0 1 0 0-7.5 3.75 3.75 0 0 0 0 7.5ZM4.5 20c0-3.6 3.4-5.5 7.5-5.5s7.5 1.9 7.5 5.5',
  'arrow-right-on-rectangle':
    'M11 7.5V5.5a1.5 1.5 0 0 1 1.5-1.5h5A1.5 1.5 0 0 1 19 5.5v13a1.5 1.5 0 0 1-1.5 1.5h-5A1.5 1.5 0 0 1 11 18.5v-2M4.5 12h10M11.5 8.5 15 12l-3.5 3.5',
  'arrow-left-on-rectangle':
    'M13 7.5V5.5A1.5 1.5 0 0 0 11.5 4h-5A1.5 1.5 0 0 0 5 5.5v13A1.5 1.5 0 0 0 6.5 20h5a1.5 1.5 0 0 0 1.5-1.5v-2M9.5 12h10M16 8.5 19.5 12 16 15.5',
  'map-pin': 'M12 21s7-5.6 7-11a7 7 0 1 0-14 0c0 5.4 7 11 7 11ZM12 12.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z',
  clock: 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18ZM12 7v5.2l3.2 2',
  'chevron-left': 'M14.5 5.5 8 12l6.5 6.5',
  'chevron-right': 'M9.5 5.5 16 12l-6.5 6.5',
  'check-circle': 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18ZM8 12.2l2.7 2.8L16 9.5',
  'exclamation-triangle': 'M12 4 2.5 20h19L12 4ZM12 10v4.5M12 17.2v.3',
  'x-circle': 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18ZM9 9l6 6M15 9l-6 6',
  'information-circle': 'M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18ZM12 11v5.5M12 7.7v.3',
  cog: 'M12 15.2a3.2 3.2 0 1 0 0-6.4 3.2 3.2 0 0 0 0 6.4ZM19.5 12c0-.6-.1-1.1-.2-1.7l1.7-1.3-1.8-3.1-2 .8a7.6 7.6 0 0 0-2.9-1.7L14 3h-4l-.3 2a7.6 7.6 0 0 0-2.9 1.7l-2-.8-1.8 3.1L4.7 10.3a7.7 7.7 0 0 0 0 3.4l-1.7 1.3 1.8 3.1 2-.8c.8.8 1.8 1.4 2.9 1.7l.3 2h4l.3-2a7.6 7.6 0 0 0 2.9-1.7l2 .8 1.8-3.1-1.7-1.3c.1-.6.2-1.1.2-1.7Z',
  // «Ish joyida yo'qlik» — yugurayotgan odam
  'person-running':
    'M16.75 4.5a1.75 1.75 0 1 1-3.5 0 1.75 1.75 0 0 1 3.5 0ZM13.5 8 11 13M13.5 8 10 8.5 8 10.5M13.5 8l2 3h3M11 13l3 2.5v5M11 13l-1.5 3.5L6 18',
  // «Kun almashtirish» — Verifix'dagi kesishgan ikki strelka (shuffle)
  'arrows-right-left':
    'M3 7h4l10 10h4M3 17h4l3.5-3.5M13.5 10.5 17 7h4M18.5 4.5 21 7l-2.5 2.5M18.5 14.5 21 17l-2.5 2.5',
  // «Jadval o'zgartirish» — qalamli taqvim
  'calendar-pencil':
    'M11 20.5H4v-14h16v4.5M4 10.5h16M8.5 3.5v4M15.5 3.5v4M14 20.5l.5-2.5 5-5a1.4 1.4 0 0 1 2 2l-5 5-2.5.5Z',
  // «Belgilash» — ikki strelkali (kirish / chiqish) yurayotgan odam
  'person-walking-arrows':
    'M10.75 4.5a1.75 1.75 0 1 1-3.5 0 1.75 1.75 0 0 1 3.5 0ZM9.5 8l-1 5.5M9.5 8l-3 2.5M9.5 8l2 3M8.5 13.5l2.5 3v4M8.5 13.5 7 17l-2.5 3.5M15 8.5h6M19 6.5l2 2-2 2M21 14.5h-6M17 12.5l-2 2 2 2',
  // «Qo'shimcha ish vaqti» — soatli portfel
  'briefcase-clock':
    'M9 6.5V5a1.5 1.5 0 0 1 1.5-1.5h3A1.5 1.5 0 0 1 15 5v1.5M11 19.5H4.5A1.5 1.5 0 0 1 3 18V8a1.5 1.5 0 0 1 1.5-1.5h15A1.5 1.5 0 0 1 21 8v2.5M3 12h7.5M21 17a4 4 0 1 1-8 0 4 4 0 0 1 8 0ZM17 15v2l1.5 1',
  // «Ishdan bo'shatish» — minusli odam
  'user-minus':
    'M9.5 12a3.75 3.75 0 1 0 0-7.5 3.75 3.75 0 0 0 0 7.5ZM2.5 20c0-3.6 3.1-5.5 7-5.5s7 1.9 7 5.5M16.5 10h5',
  // «Ta'til» — plyaj soyaboni
  'beach-umbrella':
    'M3 11a9 8 0 0 1 18 0M3 11c1-1 2-1.3 3-1.3s2 .3 3 1.3c1-1 2-1.3 3-1.3s2 .3 3 1.3c1-1 2-1.3 3-1.3s2 .3 3 1.3M12 3c-2 2-3 4.5-3 8M12 3c2 2 3 4.5 3 8M12 9.7v10.8M6 20.5h12',
  // «Jamoa» tabi — `user` ning o'zi, orqasida ikkinchi odam
  users:
    'M9.5 11.75a3.25 3.25 0 1 0 0-6.5 3.25 3.25 0 0 0 0 6.5ZM3 20c0-3.3 2.9-5 6.5-5s6.5 1.7 6.5 5M15 5.4a3 3 0 0 1 0 6.1M17.5 15.1c2.1.6 3.5 2.2 3.5 4.9',
  // Qo'ng'iroq — go'shak
  phone:
    'M3 6.75c0 7.87 6.38 14.25 14.25 14.25h1.5a2.25 2.25 0 0 0 2.25-2.25v-1.2c0-.52-.35-.97-.85-1.09l-3.6-.9c-.44-.11-.9.05-1.17.42l-.79 1.05a1.13 1.13 0 0 1-1.21.38 11.3 11.3 0 0 1-6.29-6.29 1.13 1.13 0 0 1 .38-1.21l1.05-.79c.37-.27.53-.73.42-1.17l-.9-3.6A1.13 1.13 0 0 0 6.95 3H5.25A2.25 2.25 0 0 0 3 5.25v1.5Z',
}

interface IconProps {
  name: IconName
  className?: string
  /** Nishon o'zi yagona yorliq bo'lsa — matn shu yerda. */
  label?: string
}

export function Icon({ name, className, label }: IconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.5}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={clsx('h-6 w-6 shrink-0', className)}
      role={label ? 'img' : undefined}
      aria-label={label}
      aria-hidden={label ? undefined : true}
      focusable="false"
    >
      <path d={PATHS[name]} />
    </svg>
  )
}
