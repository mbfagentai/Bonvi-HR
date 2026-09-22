import clsx from 'clsx'

/**
 * Yagona nishon to'plami — chiziqli uslub, 24 px, qalinlik 1.5
 * (UX §E.8). O'n beshta nishon butun ilovaga yetadi; o'n oltinchisi
 * kerak bo'lsa, avval ekranning o'zi haqiqatan yangimi — shu savol.
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
