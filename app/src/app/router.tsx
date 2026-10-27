import { useEffect, useRef } from 'react'
import {
  BrowserRouter,
  Navigate,
  Outlet,
  Route,
  Routes,
  useLocation,
  useNavigationType,
} from 'react-router-dom'

import { CalendarScreen } from '@/screens/calendar'
import { HomeScreen } from '@/screens/home'
import { LocationHelpScreen } from '@/screens/location-help'
import { LoginScreen } from '@/screens/login'
import { NotFoundScreen } from '@/screens/not-found'
import { PasswordHelpScreen } from '@/screens/password-help'
import { ProfileScreen } from '@/screens/profile'
import { RequestDetailScreen } from '@/screens/request-detail'
import { RequestNewScreen } from '@/screens/request-new'
import { RequestsScreen } from '@/screens/requests'
import { ScheduleRequestDetailScreen } from '@/screens/schedule-request-detail'
import { ScheduleRequestNewScreen } from '@/screens/schedule-request-new'
import { SettingsScreen } from '@/screens/settings'
import { TeamScreen } from '@/screens/team'
import { TeamMemberScreen } from '@/screens/team-member'
import { TabBar } from '@/ui/app-shell'
import { useSession } from './session'
import { UpdateToast } from './update'

/**
 * Marshrutlar — UX §B.4 dagi jadvalning aynan o'zi.
 *
 * Manzil bo'laklari o'zbekcha (`/taqvim`, `/sorovlar/yangi`), so'rov
 * kalitlari ham (`oy`, `kun`, `keyin`): qo'llab-quvvatlash
 * qo'ng'irog'ida o'qib berilgan havola ekrandagi til bilan bir xil
 * bo'lishi kerak.
 *
 * ⚠️ Ilova `/ilova` ostida yashaydi — `basename` shu yerda beriladi
 * va service worker qamrovi bilan bir xil bo'lishi SHART (UX §G.1).
 *
 * ⚠️ OXIRIDAGI CHIZIQ bilan (`/ilova/`). Chiziqsiz bo'lsa bosh sahifa
 * manzili `/ilova` bo'lib qoladi: u Vite `base` idan ham, service worker
 * qamrovidan ham TASHQARIDA — sahifani yangilash «public base URL of
 * /ilova/» xatosini berardi. React Router oxiridagi chiziqni qo'llaydi
 * (`stripBasename`), boshqa manzillar `/ilova/taqvim` ko'rinishida qoladi.
 */
export function AppRouter() {
  return (
    <BrowserRouter basename="/ilova/">
      <ScrollBehaviour />

      <Routes>
        {/* Auth: tab paneli ham, orqaga chevroni ham yo'q */}
        <Route path="/kirish" element={<LoginScreen />} />
        <Route path="/parol-yordam" element={<PasswordHelpScreen />} />

        {/*
          Parolni o'rnatish/almashtirish ekranlari YO'Q (2026-09-27): parolni
          HR beradi, xodim uni almashtirmaydi. Eski `/parol-ornatish` va
          `/profil/parol` manzillari «topilmadi» ga tushadi.
        */}
        <Route element={<RequireSession />}>
          {/* Tab ildizlari — tab paneli ko'rinadi */}
          <Route element={<TabLayout />}>
            <Route path="/" element={<HomeScreen />} />
            <Route path="/taqvim" element={<CalendarScreen />} />
            <Route path="/sorovlar" element={<RequestsScreen />} />
            {/* Bo'lim boshlig'ining jamoasi (SPEC-jamoam §F) — tabi faqat boshliqqa
                ko'rinadi; manzil ochilsa ham ekran o'zi `me.isTeamLead` ni tekshiradi */}
            <Route path="/jamoa" element={<TeamScreen />} />
            <Route path="/profil" element={<ProfileScreen />} />
          </Route>

          {/* Ichkariga ochilgan ekranlar — tab paneli YO'Q */}
          <Route path="/sorovlar/yangi" element={<RequestNewScreen />} />
          {/* Jadval so'rovlari (SPEC-jadval-sorovi §C4) — `/sorovlar/:id` bitta
              bo'lakni oladi, shu sababli ikki bo'lakli manzil unga tushmaydi. */}
          <Route path="/sorovlar/jadval/yangi" element={<ScheduleRequestNewScreen />} />
          <Route path="/sorovlar/jadval/:id" element={<ScheduleRequestDetailScreen />} />
          <Route path="/sorovlar/:id" element={<RequestDetailScreen />} />
          {/* Qaror kutayotgan so'rovni o'zgartirish (SPEC-sorov-tahriri §7.1) — o'sha
              yaratish formasi, `:id` bo'lgani uchun tahrir rejimida ochiladi. */}
          <Route path="/sorovlar/jadval/:id/ozgartirish" element={<ScheduleRequestNewScreen />} />
          <Route path="/sorovlar/:id/ozgartirish" element={<RequestNewScreen />} />
          {/* Jamoa a'zosining oyi — `?oy=YYYY-MM&kun=` taqvimdagidek */}
          <Route path="/jamoa/:employeeId" element={<TeamMemberScreen />} />
          <Route path="/profil/sozlamalar" element={<SettingsScreen />} />
          <Route path="/joylashuv" element={<LocationHelpScreen />} />
        </Route>

        <Route path="*" element={<NotFoundLayout />} />
      </Routes>
    </BrowserRouter>
  )
}

function TabLayout() {
  return (
    <>
      <Outlet />
      <TabBar />
      <UpdateToast />
    </>
  )
}

function NotFoundLayout() {
  const session = useSession()

  return (
    <>
      <NotFoundScreen />
      {session.token ? <TabBar /> : null}
    </>
  )
}

/** Tokensiz har qanday marshrut — kirish ekraniga (UX §B.3). */
function RequireSession() {
  const session = useSession()
  const location = useLocation()

  if (!session.token) {
    const next = encodeURIComponent(`${location.pathname}${location.search}`)
    return <Navigate to={`/kirish?keyin=${next}`} replace />
  }

  return <Outlet />
}

/**
 * Aylantirish holati (UX §B.2): ichkariga o'tilganda tepaga,
 * orqaga qaytilganda — avvalgi joyiga.
 */
function ScrollBehaviour() {
  const location = useLocation()
  const navigationType = useNavigationType()
  const positions = useRef<Record<string, number>>({})

  // Joriy ekranning holati DOIM yozib boriladi: marshrut almashgandan
  // keyin o'qisak, brauzer allaqachon boshqa joyga sakragan bo'lardi.
  useEffect(() => {
    const onScroll = (): void => {
      positions.current[location.key] = window.scrollY
    }

    window.addEventListener('scroll', onScroll, { passive: true })
    return () => window.removeEventListener('scroll', onScroll)
  }, [location.key])

  useEffect(() => {
    if (navigationType === 'POP') {
      window.scrollTo(0, positions.current[location.key] ?? 0)
    } else {
      window.scrollTo(0, 0)
    }
  }, [location.key, navigationType])

  return null
}
