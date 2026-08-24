import { useObjectUrl } from '@/shared/lib/use-object-url'
import { Avatar, type AvatarSize } from '@/shared/ui'
import { useMyPhoto } from '../api/account-photo-queries'
import { useAuth } from '../auth-context'

/**
 * Joriy foydalanuvchining avatari — yuqori panelda, menyuda va sozlamalarda.
 *
 * `Avatar` ustidagi yupqa qatlam: rasm baytlarini olib, ko'rsatiladigan
 * havolaga aylantiradi. Rasm yo'q, hali kelmagan yoki so'rov yiqilgan
 * bo'lsa — `Avatar` ning o'zi bosh harflarni chizadi.
 *
 * NEGA skelet yo'q: yuklanish paytida ham bosh harflar turaveradi. Kulrang
 * doira paydo bo'lib keyin yuzga almashishi yuqori panelni «qaltiratadi»,
 * ism esa birinchi kadrdanoq o'qiladi.
 *
 * NEGA ism tashqaridan: «foydalanuvchi noma'lum» matni tarjima qilinadi va
 * uni chaqiruvchi allaqachon hisoblab qo'ygan bo'ladi. Bu yerda ikkinchi
 * marta hisoblansa, ikki joyda ikki xil matn paydo bo'lishi mumkin edi.
 */
export function AccountPhoto({
  name,
  size,
  className,
}: {
  /** Bosh harflar va `alt` matni shundan olinadi. */
  name: string
  size?: AvatarSize
  className?: string
}) {
  const { user } = useAuth()

  const photo = useMyPhoto(user?.id ?? null, user?.photoKey ?? null)
  const src = useObjectUrl(photo.data ?? null)

  return <Avatar name={name} src={src} size={size} className={className} />
}
