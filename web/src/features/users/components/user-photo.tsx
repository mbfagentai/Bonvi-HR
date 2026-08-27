import { useObjectUrl } from '@/shared/lib/use-object-url'
import { Avatar, type AvatarSize } from '@/shared/ui'
import { useUserPhoto } from '../api/users-queries'

/**
 * Foydalanuvchi rasmi — ro'yxatda ham, tahrirlash oynasida ham bir xil.
 *
 * `Avatar` ustidagi yupqa qatlam: rasm baytlarini olib, ko'rsatiladigan
 * havolaga aylantiradi (`EmployeePhoto` bilan aynan bir xil naqsh —
 * autentifikatsiya sarlavha orqali yuradi, ya'ni oddiy `<img src>` 401
 * olardi). Rasm yo'q, hali kelmagan yoki so'rov yiqilgan bo'lsa — `Avatar`
 * ning o'zi bosh harflarni chizadi.
 *
 * NEGA skelet yo'q: yuklanish paytida ham bosh harflar turaveradi. Bir
 * necha qatorli ro'yxatda kulrang doiralar paydo bo'lib keyin yuzga
 * almashishi ekranni «qaltiratadi» — ism esa birinchi kadrdanoq o'qiladi.
 */
export function UserPhoto({
  userId,
  photoKey,
  name,
  size,
  className,
}: {
  userId: string
  /** `null` — rasm yuklanmagan, so'rov ham yuborilmaydi. */
  photoKey: string | null
  /** Bosh harflar va `alt` matni shundan olinadi. */
  name: string
  size?: AvatarSize
  className?: string
}) {
  const photo = useUserPhoto(userId, photoKey)
  const src = useObjectUrl(photo.data ?? null)

  return <Avatar name={name} src={src} size={size} className={className} />
}
