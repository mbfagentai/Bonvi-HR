# SPEC — Deactivating reference records (Faol / Faol emas)

Status: owner request 2026-10-31, decisions D1–D3 answered the same day. Reference records are
never deleted (`ReferenceEntity`) — "delete" in the owner's words means **deactivate**.

## 0. Owner decisions (2026-10-31)

| # | Question | Decision |
|---|---|---|
| D0 | Where inactive records live | Every reference menu has two tabs, **Faol** (default, always opens first) and **Faol emas**. Reactivation happens from the inactive tab |
| D1 | Working employees on a department / position | **Moved in the dialog itself**: HR picks the target and all of them move at once; each gets an ordinary transfer record (date, optional order no/date). Then the record is deactivated |
| D2 | Department with active sub-departments | **Deactivated together** with the whole subtree, provided no working employee is left in the subtree |
| D3 | Legal entity / direction | Legal entity: **blocked** with the list (changing it is a new contract, not a transfer). Direction: **replaced** on the working employees, no history record (it is a tag on the employee) |

## A. Who counts as "attached"

"Working" = an employment that is not `Terminated` (probation or active), not soft-deleted.

| Reference | Attached through |
|---|---|
| Department | `employments.department_id` in the department **or any descendant** |
| Position | `employments.position_id` |
| Legal entity | `employments.legal_entity_id` |
| Direction | `employees.direction_ids` of employees who have a working employment |

Other references (termination reasons, award/penalty/document/absence types, referral channels)
only appear on past records — deactivating them stays immediate, as before.

## B. Rules (`IReferenceStateRule<TEntity>`, run by the generic activate/deactivate handlers)

- Deactivate with attached working employees → **409 `malumotnoma.faol_xodim_bor`**.
- Department deactivate → its active descendants are deactivated in the same save.
- Department activate → only that department; **409 `bolim.ota_faol_emas`** if its parent is inactive.
  Sub-departments are reactivated one by one (some may have been closed separately earlier).
- Department head is left as is (an inactive department has no working employees, so the head sees nobody).
- Positions bound to a deactivated department are not touched — they are a separate reference.

## C. API

| Method | Path | Permission | Notes |
|---|---|---|---|
| GET | `/api/catalog/{kind}/{id}/usage` | `catalog.manage` | `{ activeEmployees, employees[≤500], employeesHidden, activeSubdepartments, canReassign }`; names only with `employees.view` |
| POST | `/api/catalog/{departments\|positions\|directions}/{id}/reassign` | `catalog.manage` + `employment.transfer` (directions: `employees.edit`) in the handler → `auth.ruxsat_yoq` | body `{ targetId, effectiveDate?, orderNo?, orderDate? }`; target must be active, not the source, not inside the source subtree → `malumotnoma.otkazish_joyi_notogri`. Moves + deactivation in **one** `SaveChanges` |

Department/position moves use `Employment.Transfer` — the same event the employee card writes
(`Transfer` for a department, `Promotion` for a position change).

## D. Web

- `CatalogStatusTabs` on every reference page (simple catalogs, positions, departments) with counts.
  Departments: `filterTreeByStatus` keeps an other-status node only as the path to matching ones.
- `CatalogActiveField` (edit dialog switch): deactivation first loads `/usage`; free → plain confirm;
  occupied + reassignable → employee list, target select, transfer date, order no/date →
  «O'tkazish va faol emas qilish»; legal entity → list + blocked.
- No "add sub-department" button on an inactive department.
