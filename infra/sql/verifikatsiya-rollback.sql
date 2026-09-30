-- ============================================================
--  Verifikatsiya (davomat) relizidan ORQAGA QAYTISH — ikki holat
--
--  A) FAQAT IMAGE ROLLBACK (baza qoladi, `rollback-<commit>` teglari):
--     QO'SHIMCHA QADAM KERAK EMAS. Eski image `employees.mobile_check_in_allowed`
--     ni bilmaydi; ustun NOT NULL, lekin `20260930094327_EmployeeMobileFlagSukuti`
--     unga `DEFAULT false` qo'ygan — eski image xodim qo'shganda ham ishlaydi.
--     Qolgan hamma narsa (o'qish, rekruting, rollar) eski image'da o'zgarishsiz
--     ishlaydi — 2026-09-30 da lokal repetitsiyada sinalgan. Quyidagi A bandi
--     faqat nazorat: sukut joyidami.
--
--  B) ZAXIRADAN TIKLASH (deploy'dan oldingi dump): `restore.sh` dagi
--     `pg_restore --clean` YANGI jadvallarni bilmaydi, ular esa `employees`,
--     `employments`, `positions`, `departments`, `legal_entities` ga FK
--     bilan bog'langan — `--clean` o'sha jadvallarni o'chira olmaydi va
--     baza YARIM tiklangan holda qoladi (repetitsiyada ko'rilgan).
--     Shuning uchun B bandini tiklashdan OLDIN bajaring.
-- ============================================================

-- ---------- A: nazorat (image rollback'dan oldin) — `false` qaytishi kerak ----------
SELECT column_default FROM information_schema.columns
 WHERE table_name = 'employees' AND column_name = 'mobile_check_in_allowed';

-- ---------- B: zaxiradan tiklashdan oldin (A shart emas) ----------
-- BEGIN;
-- DROP SCHEMA IF EXISTS hangfire CASCADE;
-- DROP TABLE IF EXISTS
--     absence_requests, absence_types, attendance_days, attendance_identities,
--     attendance_passes, device_enrollments, employee_refresh_tokens, employee_accounts,
--     mobile_check_ins, schedule_assignments, work_schedule_dates, work_schedule_days,
--     work_schedules, attendance_devices, work_sites
--     CASCADE;
-- DROP SEQUENCE IF EXISTS device_person_no_seq;
-- COMMIT;
