-- ════════════════════════════════════════════════════════════════════
--  Oklad xatosi prod'ga tushganmi — FAQAT O'QIYDI (2026-09-27)
-- ════════════════════════════════════════════════════════════════════
--
--  Xato (0f79ef3, 2026-09-16 dan 2026-09-27 gacha): mehnat yo'lidagi
--  yozuvni tuzatish (buyruq raqami, sana, izoh) xodimning JORIY okladini
--  o'sha yozuvdagi summaga tenglardi. Ikki holatda bu noto'g'ri edi:
--
--    1. Qo'shimcha vazifa yozuvi — okladga vazifa HAQI tushardi
--       (5 mln oklad → 1 mln).
--    2. Excel'dan kelgan yozuv — joriy oklad «91 Умумий» dan kelgan va
--       tarixdagi yozuv bilan teng bo'lishi shart emas edi.
--
--  So'rov TUZATILGAN (updated_at to'lgan) va summasi hozirgi okladga
--  teng bo'lgan shunday yozuvlarni chiqaradi. Bo'sh natija — xato hech
--  kimga tushmagan. Har bir qator QO'LDA ko'riladi: «ehtimoliy_oklad»
--  faqat ishora (vazifa haqi bo'lmagan oxirgi summali yozuv).
--
--  Ishga tushirish (prod, /opt/bonvi-hr):
--    docker compose -f docker-compose.prod.yml exec -T postgres \
--      sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"' \
--      < infra/sql/oklad-tuzatish-tekshiruvi.sql
-- ════════════════════════════════════════════════════════════════════

BEGIN TRANSACTION READ ONLY;

SELECT x.last_name || ' ' || x.first_name        AS xodim,
       m.id                                       AS employment_id,
       m.salary_amount                            AS joriy_oklad,
       CASE WHEN h.type = 4 THEN 'qo''shimcha vazifa haqi'
            ELSE 'Excel yozuvi' END               AS sabab,
       h.effective_date                           AS yozuv_sanasi,
       h.order_no                                 AS buyruq,
       h.updated_at                               AS tuzatilgan_vaqt,
       (SELECT s.to_salary_amount
          FROM employment_events s
         WHERE s.employment_id = m.id
           AND s.id <> h.id
           AND s.to_salary_amount IS NOT NULL
           AND s.type <> 4
           AND NOT (s.type IN (2, 3)
                    AND s.from_salary_amount IS NOT DISTINCT FROM s.to_salary_amount)
         ORDER BY s.effective_date DESC, s.recorded_at DESC
         LIMIT 1)                                 AS ehtimoliy_oklad
  FROM employment_events h
  JOIN employments m ON m.id = h.employment_id AND m.deleted_at IS NULL
  JOIN employees x   ON x.id = m.employee_id
 WHERE h.updated_at IS NOT NULL
   AND h.to_salary_amount IS NOT NULL
   AND m.salary_amount = h.to_salary_amount
   AND (h.type = 4 OR h.is_imported)
 ORDER BY h.updated_at DESC;

ROLLBACK;
