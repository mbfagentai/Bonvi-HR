# SPEC — candidate notes, next-contact date, multi-file attachments, withdrawn-in-rejected

> Status: authoritative. Written 2026-09-10.
> Language: English (project rule — internal docs are English; only
> client-facing text is Uzbek). Three things stay in the repo's existing
> Uzbek style, each for a stated reason: error codes (§4.3), the migration
> class name (§2.5) and every user-facing string (§5.7).
>
> **Read this before writing code.** Build agents cannot see the conversation
> that produced this file. Every decision below is final unless the user says
> otherwise; the "rejected alternative" notes exist so nobody re-litigates
> them halfway through the work.

---

## 0. Reference implementation

Copy the shape of these files. Where this SPEC deviates from them, the
deviation is named and justified.

| What | Reference |
|---|---|
| Multi-file child + upload/download/delete endpoints | `api/src/Bonvi.Api/Endpoints/EmployeeDocumentEndpoints.cs`, `api/src/Bonvi.Application/Employees/Documents/EmployeeDocumentCommands.cs`, `api/src/Bonvi.Domain/Employees/EmployeeDocument.cs` |
| Child collection inside an aggregate | `Candidate.StageEntries` / `CandidateStageEntry` (`api/src/Bonvi.Domain/Recruiting/`) |
| File validate-and-store pipeline | `api/src/Bonvi.Application/Common/Documents/DocumentFileStore.cs` |
| Image re-encode pipeline | `api/src/Bonvi.Application/Common/Photos/SquarePhotoStore.cs` + `api/src/Bonvi.Infrastructure/Storage/SkiaImageProcessor.cs` |
| Hand-written migration with data carry-over | `api/src/Bonvi.Infrastructure/Persistence/Migrations/20260908050836_VoronkaBosqichlariToldirildi.cs` |
| List filter + hide rules | `CandidateSearchFilter` in `api/src/Bonvi.Application/Recruiting/Abstractions/IRecruitingReadModel.cs`, applied in `api/src/Bonvi.Infrastructure/Persistence/ReadModels/RecruitingReadModel.cs` |
| Confirm dialog with per-action text | `web/src/features/recruiting/components/candidate-action-dialog.tsx` |

`docs/STACK.md` and `docs/CONVENTIONS.md` do not exist in this repo. This is
not a new project, so the conventions were read out of the code instead; every
convention this SPEC relies on is named at the point of use.

---

## 1. Goal and scope

### 1.1 Goal

HR loses the substance of every candidate contact. Today a stage button moves
the candidate in one click with `outcome=Pending`, no date and no conclusion,
and a hint tells HR to write the conclusion later on the "Bosqichlar" tab — in
practice it is never written. HR also has nowhere to record "we called, he
said he will come in 5-6 days", and a candidate can only carry one attached
file although passports and diplomas arrive as three or four phone photos.
Finally, candidates who withdrew on their own still clutter the everyday list.

Four changes, one release:

1. Every stage move and every candidate action asks for a note (never
   mandatory) and an optional next-contact date, in a short dialog.
2. The next-contact date is a first-class field: a table column, a card field,
   and a "contact needed today" filter.
3. One resume becomes up to ten files; uploaded images are downscaled
   server-side.
4. `CandidateStatus.Withdrawn` leaves the everyday list and joins the
   "Rad etilganlar" section, while staying a distinct status.

### 1.2 Non-goals — do not build these

- **No notifications and no background job** for the next-contact date. The
  user explicitly chose the version without notifications. Do not add a
  `NotificationType`, do not touch `NotificationState`, do not add a hosted
  service. The only surface is the table column and the filter.
- **No OCR, no text extraction, no thumbnail generation** for candidate files.
- **Do not merge `Rejected` and `Withdrawn`.** The enum values stay separate,
  the badges stay separate (`CANDIDATE_LOOK`: red vs yellow), and every report
  keeps distinguishing them. The `CandidateStatus.Withdrawn` domain remark
  explains why the split exists; only the *list visibility* changes.
- **No stand-alone "add a note" button** on the candidate card. Notes are
  produced by actions, never floating. Rationale: a free note stream would
  compete with the stage conclusion and HR would have two places to look for
  the same sentence — which is the problem this release fixes, not creates.
- **No per-file note or per-file document type.** Employee documents have both;
  candidate files have neither (§4.3).
- **No change to the funnel drag-and-drop.** See §5.1.6.
- **No change to any bot output.** See §7.

---

## 2. Data model

Naming: PostgreSQL identifiers are produced by
`UseSnakeCaseNamingConvention()` (`api/src/Bonvi.Infrastructure/DependencyInjection.cs`).
Column names below are the resulting snake_case names; do not set
`HasColumnName` except where the existing code already does.

### 2.1 `candidates` — changed

| Column | Type | Null | Default | Note |
|---|---|---|---|---|
| `next_contact_date` | `date` | yes | — | **new**. When HR plans to contact this person next. Tashkent calendar date, no time — `DateOnly` (the project stores instants as `timestamptz` and calendar facts as `date`; a callback is a calendar fact, cf. `ClosedAt`). |
| `resume_file_key` | `varchar(500)` | — | — | **dropped** (§2.5) |
| `resume_file_name` | `varchar(300)` | — | — | **dropped** (§2.5) |
| `resume_content_type` | `varchar(150)` | — | — | **dropped** (§2.5) |
| `resume_size_bytes` | `bigint` | — | — | **dropped** (§2.5) |

New index:

```
ix_candidates_next_contact_date  ON candidates (next_contact_date)
                                 WHERE next_contact_date IS NOT NULL
```

Partial, following the habit already established for `phone`, the rejection
queue and the interview queue in `RecruitingConfigurations`: most candidates
have no callback date, so a full index would store thousands of NULL entries
that the "contact due" query never reads.

#### 2.1.1 Why the date lives on `Candidate` and is not derived

**Decision: a denormalised column on `Candidate`.**

Rejected alternative: derive it from the latest `CandidateStageEntry`. The
list screen needs (a) a sortable column and (b) a `WHERE next_contact_date <= today`
filter that also feeds the row count. Deriving it means a correlated subquery
or a lateral join per row inside `SearchCandidatesAsync`, which cannot use an
index and would scan the stage-entry table for every page. `Candidate.CurrentStageId`
is denormalised out of the same child collection for exactly this reason and
its XML doc says so — this is the same trade-off, made the same way.

Accepted cost: the column can drift from the stage history if someone writes
to the database by hand. There is no history of past callback dates; the
conclusion text already carries "he said he will come in 5-6 days".

#### 2.1.2 When it is set and when it is cleared — normative table

| Action | Effect on `next_contact_date` |
|---|---|
| `POST /candidates/{id}/stages` | set when `nextContactDate` is present; cleared when `clearNextContact = true`; **unchanged** when both are absent |
| `POST /candidates/{id}/interview-invite` | same three-way rule |
| `POST /candidates/{id}/reassign` | set when `nextContactDate` is present; otherwise unchanged |
| `POST /candidates/{id}/return-from-reserve` | set when `nextContactDate` is present; otherwise unchanged |
| `POST /candidates/{id}/reserve` | set when `nextContactDate` is present; otherwise **unchanged** — the date survives |
| `POST /candidates/{id}/reject` | **cleared** |
| `POST /candidates/{id}/withdraw` | **cleared** |
| `POST /candidates/{id}/hire` | **cleared** |
| `PUT /candidates/{id}` (edit details) | untouched — editing a person's details is not a decision about contacting them |

Two rules deserve their reasons in the code:

- **Reject / withdraw / hire clear it.** Otherwise a rejected candidate with a
  callback date sits in the "contact needed today" list forever and HR learns
  to ignore the list. Implement inside the single private
  `Candidate.Close(status, at, reason)` so all exits obey it, with the one
  exception below.
- **Reserve keeps it.** Reserve is not an exit, it is a postponement — the
  whole documented meaning of `CandidateStatus.Reserve` is "we will call them
  when a slot opens", so a callback date is exactly right there. Concretely:
  `Close` clears the date **unless** `status == CandidateStatus.Reserve`.

#### 2.1.3 No past-date validation

`next_contact_date` accepts any date, including the past.

Rejected alternative: copy `InviteToInterview`'s "yesterday is a typo" rule.
It cannot be copied here: the field's entire purpose is a "today or overdue"
list, so an overdue value is a *normal* state — and once a date passes, the
pre-filled stage dialog would refuse to save without HR touching a field they
never meant to touch. The interview time is different because a past interview
time produces a *message to the candidate*; a past callback date only makes a
row appear in HR's own list, where the table column shows the mistake and one
click fixes it.

### 2.2 `candidate_files` — new table

Child of the `Candidate` aggregate, exactly like `candidate_stage_entries`.

| Column | Type | Null | Note |
|---|---|---|---|
| `id` | `uuid` PK | no | UUID v7 from `Entity.NewId()` for rows created by code; `gen_random_uuid()` for rows created by the migration (§2.5) |
| `candidate_id` | `uuid` FK → `candidates.id`, `ON DELETE CASCADE` | no | |
| `file_key` | `varchar(500)` | no | `IFileStorage` key. Length copied from `employee_documents.file_key` |
| `file_name` | `varchar(300)` | no | display only, never used in a storage path. Length copied from the dropped `candidates.resume_file_name` so carried-over data cannot overflow |
| `content_type` | `varchar(150)` | no | MIME type **detected from content**, never the client header. Length copied from the dropped `candidates.resume_content_type` |
| `size_bytes` | `bigint` | no | **stored** size — the bytes actually written to disk, i.e. *after* downscaling. See §2.2.2 |
| `created_at` | `timestamptz` | no | upload instant |
| `created_by` | `uuid` | yes | `users.id` — same meaning and same nullability as `CandidateStageEntry.RecordedBy`, and for the same documented reason ("who typed it" is an audit fact and only exists for a logged-in account) |

Index: `ix_candidate_files_candidate_id_created_at ON candidate_files (candidate_id, created_at)`.
Covers both reads (the card lists a candidate's files in upload order) and the
count that enforces the ten-file cap; it also replaces the plain foreign-key
index.

No `deleted_at` / `deleted_by`: candidate files are **hard-deleted** (§2.2.3).
No `note` and no `document_type_id` (§4.3).

#### 2.2.1 Why a child of `Candidate` and not its own aggregate root

`EmployeeDocument` is an aggregate root with its own repository. Candidate
files are deliberately **not**.

Reason: the ten-file cap and the total-size cap are invariants of the
candidate, and an invariant can only be enforced inside the aggregate that
owns it. A separate root would have to count rows with a query before
inserting, which is a race (two uploads in flight both see nine files) and
puts a business rule in a handler instead of the domain. The second half of
what made `EmployeeDocument` a root — a reference-data type per document and a
tab that filters by it — does not exist here.

Consequence for the repository:
`CandidateRepository.GetWithStagesAsync` must also include `Files`, and the
query gains `.AsSplitQuery()`:

```
Set.Include(c => c.StageEntries.OrderBy(e => e.RecordedAt))
   .Include(c => c.Files.OrderBy(f => f.CreatedAt))
   .Include(c => c.Notes.OrderBy(n => n.CreatedAt))
   .Include(c => c.Questionnaire)
   .AsSplitQuery()
   .FirstOrDefaultAsync(...)
```

`AsSplitQuery()` is required, not cosmetic: three collection includes in one
query produce a cartesian product (10 × 10 × 10 rows in the worst case) and EF
Core logs a warning for it. Split query issues four cheap index-covered
queries instead. Keep the `OrderBy` inside each `Include` — the existing XML
doc explains why (PostgreSQL returns child rows in arbitrary order otherwise,
so the timeline reshuffles on every open).

Keep the method **name** `GetWithStagesAsync`. Renaming it would touch ten
call sites for no behavioural gain; instead extend its XML remark to say it
loads the whole card — stages, files, notes and the questionnaire.

#### 2.2.2 `size_bytes` is the stored size, not the uploaded size

Today `Candidate.AttachResume` is handed `command.SizeBytes`, i.e. what the
client uploaded. After downscaling that number becomes a lie — a 4 MB phone
photo is stored as roughly 300 KB and the card would claim 4 MB. The pipeline
in §4.4 therefore returns the length of the bytes it wrote, and that is what
is persisted. The 20 MB per-file limit is still measured on the **uploaded**
bytes (you must reject the request before decoding it), so the two numbers
have two different jobs; say so in the XML doc.

#### 2.2.3 Deletion is hard, on purpose

`EmployeeDocument` is soft-deleted and its file is deliberately left on disk,
because a soft-deleted row that points at a missing file would break the
`ISoftDeletable` promise. Candidate files do the opposite: the row is removed
and `IFileStorage.DeleteAsync` is called.

Reasons, both specific to this feature:

1. A soft-deleted row still occupies one of the ten slots unless every count
   filters it out — and a hidden row that silently blocks an upload is exactly
   the class of invisible failure this codebase avoids.
2. It preserves today's behaviour: `DeleteCandidateResumeCommandHandler`
   already hard-deletes the file with the documented reason "the user
   deliberately said remove it and nothing else refers to the file". A
   candidate's attachment is the candidate's own document, not a company
   document of record.

### 2.3 `candidate_notes` — new table

Child of the `Candidate` aggregate. Holds the note for actions that have **no
stage entry to attach it to**.

| Column | Type | Null | Note |
|---|---|---|---|
| `id` | `uuid` PK | no | |
| `candidate_id` | `uuid` FK → `candidates.id`, `ON DELETE CASCADE` | no | |
| `kind` | `integer` | no | `CandidateNoteKind`, see below |
| `text` | `varchar(2000)` | no | a row exists only because there is text; an empty note creates no row |
| `created_at` | `timestamptz` | no | |
| `created_by` | `uuid` | yes | `users.id`, same rule as `candidate_files.created_by` |

Index: `ix_candidate_notes_candidate_id_created_at ON candidate_notes (candidate_id, created_at)`.

```csharp
public enum CandidateNoteKind
{
    /// <summary>Moved to another vacancy.</summary>
    Reassign = 1,

    /// <summary>Returned from reserve into the funnel.</summary>
    ReturnFromReserve = 2,

    /// <summary>Interview moved to a new time (no stage change).</summary>
    InterviewRescheduled = 3,
}
```

Add new values **at the end** only — the same rule the `CandidateStatus`
remark already states for this project (inserting in the middle silently
shifts stored values).

`kind` is an enum and not free text because the timeline must label the row
("Moved to another vacancy") in four languages; a label baked into the text
would be untranslatable. The enum having no `Manual` value is what enforces
the non-goal "no free-floating notes".

#### 2.3.1 Why a new table and not `Candidate.Note`, `ClosingNote` or a stage entry

All three alternatives named in the requirement were evaluated and rejected:

- **`Candidate.Note`** — this is a long-lived *description of the person*
  ("who referred them, what stands out"; see `recruiting.candidateNotePlaceholder`).
  The `MoveToReserve` remark already draws the line explicitly: `ClosingNote`
  is the reason for a decision, `Note` is the long-term description. It is
  also editable from the edit form, so a note appended here would be silently
  overwritten by the next edit — the exact failure the requirement forbids.
  And it is a single field, not history.
- **`ClosingNote`** — impossible for `returnFromReserve`: `ReturnFromReserve()`
  *clears* `ClosingNote` in the same transaction, so the note would be erased
  as it was written.
- **A `CandidateStageEntry`** — a stage entry carries `StageId` **and**
  `Outcome`, and both would be lies. "Moved to another vacancy" is not a
  result at the candidate's current stage: if they had already passed the
  phone interview, writing a new `Pending` entry on "Гаплашилди" claims the
  phone interview is being redone, and the timeline would grow a
  "Record the outcome" button for something that has no outcome.
- **A fourth `StageOutcome` value (`Note`)** — rejected for a concrete reason
  beyond taste: `StageOutcome` has a **second consumer**. The employee card's
  council-archive section reads `EmployeeStageEntryDto` from
  `GET /api/employees/{id}/stages` (`web/src/features/employees/components/employee-council-section.tsx`),
  so a new value would appear in the employees module — a screen that is out
  of this task's scope and where a candidate note has no meaning.

The new table is small (six columns, one index, three write sites) and it is
the only option where nothing lies and nothing is overwritten.

### 2.4 Enum values touched elsewhere

None. `CandidateStatus`, `StageOutcome`, `CandidateSource` and
`VacancyStatus` are unchanged. `CandidateNoteKind` is the only new enum.

### 2.5 Migration plan

**One migration**, not three. EF Core serialises the whole model into
`AppDbContextModelSnapshot.cs`, so parallel migrations cannot be authored and
they would have to be applied in a fixed order anyway.

- File: `api/src/Bonvi.Infrastructure/Persistence/Migrations/<timestamp>_NomzodIzohVaFayllar.cs`
- Class name is Uzbek because every migration in this repo is
  (`NomzodAnketasi`, `SuhbatgaChaqirish`, `TaklifOlibTashlandi`,
  `VoronkaBosqichlariToldirildi`). Follow the repo, not the global
  English-identifiers rule, so `ls Migrations/` still reads as one sequence.
- Generate with `./bonvi migrate NomzodIzohVaFayllar`, then **read the
  generated file and delete every DROP that this SPEC did not ask for**
  (project rule: `--autogenerate` emits spurious drops). The four resume
  columns are the only intended drops.

#### `Up` — exact order, and the order matters

1. `CREATE TABLE candidate_files` + its index.
2. `CREATE TABLE candidate_notes` + its index.
3. `ALTER TABLE candidates ADD COLUMN next_contact_date date NULL` + its
   partial index.
4. **Carry the existing resume over** — raw SQL, before any drop:

```sql
INSERT INTO candidate_files
    (id, candidate_id, file_key, file_name, content_type, size_bytes, created_at, created_by)
SELECT gen_random_uuid(),
       id,
       resume_file_key,
       coalesce(nullif(btrim(resume_file_name), ''), 'rezyume'),
       coalesce(nullif(btrim(resume_content_type), ''), 'application/octet-stream'),
       coalesce(resume_size_bytes, 0),
       created_at,
       NULL
  FROM candidates
 WHERE resume_file_key IS NOT NULL;
```

   - `WHERE resume_file_key IS NOT NULL` — the file key is the only column
     that proves a file exists; `AttachResume` never wrote a key without a
     content type, but hand-written or imported rows might, hence the
     `coalesce` fallbacks rather than a `NOT NULL` violation mid-migration.
   - `created_at` comes from `candidates.created_at`, not `updated_at`. The
     upload instant was never recorded (the old card deliberately showed no
     upload date, and its comment says so). `candidates.created_at` is the
     only timestamp guaranteed to be at or before the real upload, and using
     it keeps the "page order" sort in §4.3.4 stable. `updated_at` was rejected
     because it moves on every unrelated edit.
   - `created_by` is `NULL` — genuinely unknown. The column is nullable for
     exactly this case.
   - `gen_random_uuid()` (v4) matches the existing migrations in this repo;
     code paths use UUID v7 via `Entity.NewId()`. Mixed versions are fine, the
     column is just a `uuid`.
   - Idempotent enough for a re-run: a second `Up` cannot happen (EF records
     the migration), and the statement has no side effect outside the new
     table.

5. `DROP COLUMN resume_file_key, resume_file_name, resume_content_type, resume_size_bytes`.

#### Decision: the four columns are dropped **now**, not kept for a release

Reasons:

1. There is one deployment target (one VPS at `/opt/bonvi-hr`) and the API and
   web app deploy together. There is no fleet where an older API keeps writing
   to the old columns, so the usual reason for a two-release drop does not
   apply here.
2. A kept-but-unwritten column is a trap: the next `--autogenerate` would
   propose dropping it anyway, and in the meantime someone reads it and gets a
   stale value.
3. Keeping them means keeping `Candidate.AttachResume` / `RemoveResume` and
   the four properties alive as dead code that the domain tests would still
   exercise.

The safety net is the `Down` path plus the migration test in §8.2 — not a
second release.

#### `Down`

1. `ALTER TABLE candidates ADD COLUMN` × 4, with the original types and
   lengths, all nullable.
2. Copy back the **earliest** file per candidate — for carried-over data that
   is exactly the original resume:

```sql
UPDATE candidates AS c
   SET resume_file_key    = f.file_key,
       resume_file_name   = f.file_name,
       resume_content_type = f.content_type,
       resume_size_bytes  = nullif(f.size_bytes, 0)
  FROM (SELECT DISTINCT ON (candidate_id) candidate_id, file_key, file_name, content_type, size_bytes
          FROM candidate_files
         ORDER BY candidate_id, created_at, id) AS f
 WHERE c.id = f.candidate_id;
```

3. `DROP TABLE candidate_files`, `DROP TABLE candidate_notes`.
4. `DROP INDEX ix_candidates_next_contact_date`, `DROP COLUMN next_contact_date`.

Accepted and documented loss on `Down`: files two through ten become orphans
on disk, and every next-contact date and every note is gone. A migration
cannot delete files (it has no `IFileStorage`), and `Down` is a development
escape hatch, not a production operation. Write this in the `Down` XML doc so
the next reader is not surprised.

### 2.6 Domain API — new and changed members

`api/src/Bonvi.Domain/Recruiting/Candidate.cs`:

```csharp
// new
public const int MaxFiles = 10;
public const long MaxFilesTotalBytes = 60L * 1024 * 1024;

public DateOnly? NextContactDate { get; private set; }
public IReadOnlyList<CandidateFile> Files => _files.AsReadOnly();
public IReadOnlyList<CandidateNote> Notes => _notes.AsReadOnly();

public Result<CandidateFile> AttachFile(
    string fileKey, string? fileName, string contentType, long storedBytes, Guid? createdBy);

/// <summary>Removes the row and returns the storage key so the caller can delete the bytes.</summary>
public Result<string> RemoveFile(Guid fileId);

public Result<CandidateNote> AddNote(CandidateNoteKind kind, string text, Guid? createdBy);

public void SetNextContactDate(DateOnly? date);   // null clears

// changed
public Result<CandidateStageEntry> RecordStage(... unchanged signature ...);
private Result Close(CandidateStatus status, DateOnly at, string? reason);  // clears NextContactDate unless status == Reserve
public Result MoveToReserve(DateOnly at, string? reason = null, DateOnly? nextContactDate = null);
public Result ReturnFromReserve(DateOnly? nextContactDate = null);

// removed together with the four columns
// AttachResume, RemoveResume, ResumeFileKey, ResumeFileName, ResumeContentType, ResumeSizeBytes
```

Why the limits live on `Candidate` and not in a limits class: both numbers are
invariants enforced by `AttachFile`, and a constant in another layer would sit
away from the rule that uses it. `RecruitingRules` was rejected as their home
because its own XML doc says those constants are *defaults for system
settings* and are never used directly — these two are hard limits, not
settings.

`AttachFile` rejects, in this order:

1. `Files.Count >= MaxFiles` → `nomzod.rezyume_kop`
2. `Files.Sum(SizeBytes) + storedBytes > MaxFilesTotalBytes` → `nomzod.rezyume_joy`
3. blank `fileKey` / blank `contentType` / `storedBytes <= 0` → `nomzod.rezyume_bosh` (existing code, reused)

`AttachFile` does **not** require the candidate to be in progress. HR uploads
a diploma copy the day after a rejection; blocking that would push the file
outside the system. `RecordStage`, `Reassign` and the closing actions keep
their existing `IsActive` guard.

`CandidateFile` mirrors `EmployeeDocument`'s `NormalizeFileName` behaviour —
strip any path (`C:\Users\hr\rezyume.pdf` → `rezyume.pdf`), fall back to
`"rezyume"` when blank. Copy the method from `Candidate` as it is removed from
there; the fallback string stays `"rezyume"` (not `"hujjat"`) so carried-over
and new rows agree.

`CandidateNote.Create` trims the text and returns no length error. This
matches the existing convention exactly: `Candidate.Note` (2000) and
`CandidateStageEntry.Conclusion` (4000) are also capped only by the column.
The UI caps the field instead (`maxLength` — §5.1.1 and §5.1.4); the
server-side gap is listed as a pre-existing risk in §10.2.

---

## 3. Persistence configuration

`api/src/Bonvi.Infrastructure/Persistence/Configurations/RecruitingConfigurations.cs`:

- `CandidateConfiguration`
  - remove the two `Property(...)` calls for `ResumeFileName` / `ResumeContentType`
  - add the two child collections, both `Cascade`, both with backing fields,
    copying the `StageEntries` block verbatim:

```csharp
builder.HasMany(c => c.Files).WithOne().HasForeignKey(f => f.CandidateId).OnDelete(DeleteBehavior.Cascade);
builder.Navigation(c => c.Files).HasField("_files").UsePropertyAccessMode(PropertyAccessMode.Field);
// same for Notes / _notes
```

  - add the partial index from §2.1
- new `CandidateFileConfiguration` → table `candidate_files`, `Ignore(DomainEvents)`,
  lengths from §2.2, index from §2.2. **No** foreign key to any reference
  table (there is no document type).
- new `CandidateNoteConfiguration` → table `candidate_notes`, `Ignore(DomainEvents)`,
  `Property(n => n.Text).HasMaxLength(2000).IsRequired()`, `Property(n => n.Kind).IsRequired()`,
  index from §2.3.
- `AppDbContext`: add `public DbSet<CandidateFile> CandidateFiles => Set<CandidateFile>();`
  and `public DbSet<CandidateNote> CandidateNotes => Set<CandidateNote>();`
  next to `CandidateStageEntries`.

Both new entities are **not** `ISoftDeletable` and **not** `IAuditable` — they
carry `CreatedAt`/`CreatedBy` as plain properties set by the domain, exactly
like `CandidateStageEntry` carries `RecordedAt`/`RecordedBy`. Do not add them
to any soft-delete global filter.

---

## 4. API contract

Every route below is under `/api/recruiting/candidates` and lives in
`api/src/Bonvi.Api/Endpoints/CandidateEndpoints.cs`. RBAC uses the two
existing permissions only: `Permissions.RecruitingView` (`recruiting.view`)
for reads, `Permissions.RecruitingManage` (`recruiting.manage`) for writes.
No new permission is introduced — the file's own remark explains why
recruiting has exactly two.

> **OpenAPI trap, mandatory.** Never give a non-nullable value type a `default`
> value in a request record (`Guid = default`, `bool = false`,
> `DateTimeOffset = default`). It makes `/openapi/v1.json` return 500 and
> `./bonvi gen` stops working — this repo was broken by it once on 2026-08-29.
> Every new optional flag below is therefore `bool?` with `= null`, read as
> `?? false`.

### 4.1 `POST /{id}/stages` — record a stage (changed)

Request `RecordCandidateStageRequest`:

```csharp
public sealed record RecordCandidateStageRequest(
    Guid StageId,
    StageOutcome Outcome = StageOutcome.Pending,
    DateOnly? Date = null,
    string? Conclusion = null,
    DateOnly? NextContactDate = null,
    bool? ClearNextContact = null);
```

- `NextContactDate` present → set it.
- `ClearNextContact = true` → clear it. `NextContactDate` is ignored when this
  is true.
- both absent → **leave the stored value untouched.**

**Why two fields instead of "null means clear".** The same endpoint is called
from the new dialog *and* from the funnel drag-and-drop
(`useMoveCandidateToStage`), which has no form and sends `conclusion: null`.
With a null-means-clear contract, dragging a card would silently erase a
callback date HR set five minutes earlier. Two fields are uglier and explicit;
this codebase consistently prefers explicit over silent (`IncludeArchive`,
`ExcludedVacancyId`).

Response `CandidateDto` (200). Errors unchanged: `bosqich.topilmadi` (404),
`bosqich.faol_emas` (409), `nomzod.jarayonda_emas` (409),
`nomzod.topilmadi` (404). RBAC `recruiting.manage`.

`RecordCandidateStageCommand` gains the same two fields and the handler calls
`candidate.SetNextContactDate(...)` **after** `RecordStage` succeeds and before
`SaveChangesAsync`, so a rejected stage move does not leave a changed date
behind.

### 4.2 `POST /{id}/interview-invite` — invite to interview (changed)

Request:

```csharp
public sealed record InviteCandidateToInterviewRequest(
    DateTimeOffset InterviewAt,
    Guid? StageId = null,
    string? Conclusion = null,
    DateOnly? NextContactDate = null,
    bool? ClearNextContact = null);
```

`NextContactDate` / `ClearNextContact` behave exactly as in §4.1.

**`Conclusion` has two sinks and the server decides which.** This is the only
place in the contract where one field lands in two tables; document it in the
endpoint description and in the handler:

- `StageId != null` → the text becomes the new stage entry's `Conclusion`
  (today's behaviour, unchanged).
- `StageId == null` (rescheduling — the stage does not change) → the text
  becomes a `candidate_notes` row with `kind = InterviewRescheduled`.

Rejected alternative: two request fields (`conclusion` and `note`) and let the
client pick. That pushes a server-side storage decision into the browser and
gives the frontend a rule to get wrong; the client sends one text field
always. Rejected alternative: keep today's behaviour and hide the field when
rescheduling — that is the current confusing UI, where a field appears and
disappears and any typed note is silently dropped.

Response `CandidateDto` (200). Errors unchanged, including
`nomzod.suhbat_vaqti_otgan` (400). RBAC `recruiting.manage`.

Order of operations in the handler is unchanged and still matters: validate
the interview time **first** (the existing comment explains why), then the
stage, then the note sink, then the next-contact date, then save.

### 4.3 Candidate files — three new endpoints, three removed

**Removed:** `POST /{id}/resume`, `GET /{id}/resume`, `DELETE /{id}/resume`
and their three handlers in
`api/src/Bonvi.Application/Recruiting/Candidates/CandidateResumeUseCases.cs`.
They read columns that no longer exist; keeping them as deprecated aliases is
not possible.

The route segment becomes `files` (plural) even though the error codes keep the
`rezyume_` prefix. The tension is deliberate: a plural collection under a
singular name would mislead every future reader, while the error codes are
already translated into four languages and "rezyume" is the word HR uses for
the attachment.

#### 4.3.1 `POST /{id}/files`

- `multipart/form-data`, single field `file`. **One file per request.**
- `.DisableAntiforgery()` — JWT lives in the header, not a cookie (same
  reason as the existing resume and photo uploads).
- `.WithMetadata(new RequestSizeLimitAttribute(MaxResumeRequestBytes))` — reuse
  the private constant already in `CandidateEndpoints`
  (`DocumentLimits.MaxBytes + 64 KB`; the 64 KB is the multipart envelope).
  Leave its name alone — renaming it is churn with no reader benefit. The route
  limit matters: a body over the limit is never read by Kestrel at all.
- RBAC `recruiting.manage`.
- Response **`200` with `CandidateFileDto`** — the created file, not the whole
  candidate. `200`, not `201`: every write in this endpoint file returns `200`
  and `UploadEmployeeDocument` does too. This follows the employee-documents
  shape rather than the old resume upload, which returned the whole
  `CandidateDto`; the frontend does not need the candidate back because
  `providers.tsx` invalidates the entire cache after any successful write.
- Errors: `nomzod.topilmadi` (404), `nomzod.rezyume_katta` (400),
  `nomzod.rezyume_yaroqsiz` (400), `nomzod.rezyume_kop` (400),
  `nomzod.rezyume_joy` (400).

**Why one file per request and not a multi-file multipart.** With N files in
one request, one bad file either fails the whole batch or forces
partial-success semantics that neither `Result<T>` nor `ProblemDetails` can
express. The frontend loops over the picked files, uploads them sequentially
and shows a per-file error (§5.4). The `<input>` still allows multi-select, so
HR's gesture is unchanged.

Handler order (cheap checks first, matching `UploadEmployeeDocumentCommandHandler`):

1. load the candidate with children → 404 if missing (do not read the stream
   for an unknown candidate)
2. **pre-check the count** against `Candidate.MaxFiles` → `nomzod.rezyume_kop`
   before touching the stream. Rechecking inside `AttachFile` is still
   required (it is the invariant), but failing early avoids decoding a 4 MB
   photo just to throw it away.
3. `ScaledDocumentStore.AcceptAsync(...)` (§4.4)
4. `candidate.AttachFile(key, fileName, contentType, storedBytes, currentUser.UserId)`
5. on domain failure: delete the just-written file from disk, then return the
   error — the existing "no orphan file behind a rejected upload" rule
6. `SaveChangesAsync`
7. map to `CandidateFileDto`

#### 4.3.2 `GET /{id}/files/{fileId}`

- Returns raw bytes via `Results.File(stream, contentType, fileName)`, so
  `Content-Disposition` carries the original file name — same as
  `DownloadEmployeeDocument`.
- RBAC `recruiting.view`.
- Errors: `nomzod.topilmadi` (404), `nomzod.rezyume_topilmadi` (404 — unknown
  file id, file belongs to another candidate, or the row exists but the bytes
  are missing from disk; the existing handler already collapses the last case
  into "not found" with a documented reason).
- Route shape is `/{id}/files/{fileId}`, not the employee-documents
  `/{documentId}/file`. The collection segment is already `files`, so a
  trailing `/file` would read as `files/{id}/file`.
- **Lookup is scoped by both ids** — `WHERE candidate_id = {id} AND id = {fileId}`.
  This is a deliberate deviation from `EmployeeDocumentEndpoints`, which looks
  a document up by its id alone and ignores the route's `employeeId`. Scoping
  means a mismatched pair returns 404 instead of serving another candidate's
  passport scan.
- `{id}` is used here, but note the repo-wide rule anyway: **every route
  parameter must appear in the handler signature** even when unused, or the
  OpenAPI document becomes invalid and `./bonvi gen` (Orval) refuses to read
  it. The comment appears four times in `EmployeeDocumentEndpoints.cs`.
- **No cache headers.** `DownloadEmployeeDocument` sets none and this endpoint
  sets none either. Do not copy `PhotoHttp`'s `ETag` + `private, 1 day` block
  here: a candidate file is personal data, the blob is already cached in the
  client's query cache for the lifetime of the screen, and an `ETag` derived
  from a storage key would have to be invented (files are immutable, but their
  keys are not exposed). If a future change adds caching it must be
  `Cache-Control: private`, never `public`.

#### 4.3.3 `DELETE /{id}/files/{fileId}`

- RBAC `recruiting.manage`. Response `204`.
- Row removed, then `IFileStorage.DeleteAsync` **after** `SaveChangesAsync` —
  if the save fails the row still points at bytes that are still there
  (the ordering rule already written in `UploadCandidateResumeCommandHandler`).
- Errors: `nomzod.topilmadi` (404), `nomzod.rezyume_topilmadi` (404).
- **Not idempotent**: an unknown `fileId` is a 404, unlike
  `DELETE /{id}/photo` which returns 204 when there is no photo. The photo is
  a single slot ("the goal is that no photo remains, and it does not"); here
  the caller named a specific row, and silently succeeding would hide a stale
  UI listing a file that is already gone.

#### 4.3.4 Listing: inside `CandidateDto`, no list endpoint

`CandidateDto` gains `files: CandidateFileDto[]`, ordered by `createdAt`
**ascending** (upload order).

Rejected alternative: `GET /{id}/files`. This is the same argument the
codebase already makes for `stages` and `questionnaire` — HR always looks at
files together with the card, so a second request only slows the screen down.
Employee documents *do* have a list endpoint because that list is its own tab
with its own `documentTypeId` filter; candidate files have neither.

Ascending order is a named deviation from employee documents, which sort
newest-first: a scanned passport arrives as pages 1-2-3 in that order, and
reversing them makes the card unreadable.

```csharp
/// <param name="Id">File id — used by download and delete.</param>
/// <param name="FileName">Original name, display only.</param>
/// <param name="ContentType">MIME type detected from content.</param>
/// <param name="SizeBytes">STORED size, after downscaling. 0 = unknown (carried over from a row with no size).</param>
/// <param name="UploadedAt">When it was attached.</param>
public sealed record CandidateFileDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset UploadedAt);
```

No `isImage` flag: the client already derives it from
`contentType.startsWith('image/')` (`iconFor` in the current resume card).

### 4.4 The upload pipeline — `ScaledDocumentStore`

New file `api/src/Bonvi.Application/Common/Documents/ScaledDocumentStore.cs`,
next to `DocumentFileStore`. Registered as `services.AddScoped<ScaledDocumentStore>()`
in `api/src/Bonvi.Application/Common/DependencyInjection.cs`, beside the
existing two stores.

```csharp
public sealed record ScaledDocumentAcceptance
{
    public string? Key { get; }
    public string? ContentType { get; }
    public long StoredBytes { get; }
    public DocumentRejection? Rejection { get; }

    public static ScaledDocumentAcceptance Accepted(string key, string contentType, long storedBytes);
    public static ScaledDocumentAcceptance Rejected(DocumentRejection rejection);
}

public sealed class ScaledDocumentStore
{
    public ScaledDocumentStore(IFileStorage storage, IFileContentInspector inspector, IImageProcessor images);

    public Task<ScaledDocumentAcceptance> AcceptAsync(
        Stream content, long sizeBytes, CancellationToken cancellationToken = default);
}
```

Algorithm:

1. `sizeBytes > DocumentLimits.MaxBytes` → `Rejected(TooLarge)`. Cheap check
   first, before reading the stream.
2. Buffer the stream fully into memory (bounded by step 1, same as
   `DocumentFileStore`).
3. Empty buffer → `Rejected(Invalid)`.
4. `IFileContentInspector.Detect(bytes)`; `null` → `Rejected(Invalid)`. This
   is the only trusted type check — never the extension, never the client's
   `Content-Type`.
5. `DetectedFileKind.Pdf` → store the original bytes unchanged (a
   `new MemoryStream(bytes, writable: false)`, as `DocumentFileStore` does),
   content type `application/pdf`, `StoredBytes = bytes.Length`.
6. `Jpeg` / `Png` / `Webp` → `ToBoundedAsync` over a fresh
   `MemoryStream(bytes, writable: false)` (the buffer was already consumed by
   `Detect`), `maxEdge: 2000`. `null` → `Rejected(Invalid)`. `await using` the
   returned `ProcessedImage.Content`, store it, content type `image/jpeg`,
   `StoredBytes = the length of that stream`.

The `fileName` argument handed to `IFileStorage.SaveAsync` is a server-made
name that only supplies the extension — `"hujjat.pdf"` for PDFs and
`"hujjat.jpg"` for images, following `DocumentFileStore`'s
`"hujjat" + ExtensionFor(kind)`. The client's file name never reaches a
storage path (`LocalFileStorage` mints a GUID key); it is persisted separately
in `candidate_files.file_name` for display only. Getting this backwards is how
`../../etc/passwd` becomes a path — the reason is already written in
`PhotoLimits.StoredFileName`.

**Why a new class instead of changing `DocumentFileStore`.** Employee
documents must **not** be re-encoded — the existing XML doc states it in
capitals ("a contract or an order must be stored unchanged") and
`IFileContentInspector`'s doc repeats it. Adding downscaling to
`DocumentFileStore` would silently start re-encoding every contract scan in
the company. Additive, per the project's hard rule.

#### 4.4.1 `IImageProcessor` gains one method

```csharp
/// <summary>
/// Fits the image inside a <paramref name="maxEdge"/> × <paramref name="maxEdge"/>
/// box, preserving the aspect ratio. Images that already fit are re-encoded
/// but never enlarged.
/// </summary>
Task<ProcessedImage?> ToBoundedAsync(Stream source, int maxEdge, CancellationToken cancellationToken = default);
```

Only one implementation exists (`SkiaImageProcessor`) and there are no test
doubles, so adding a method breaks nothing.

Implementation notes for `SkiaImageProcessor.ToBoundedAsync` — reuse, do not
duplicate:

- the same accepted source formats (`IsSupported`: JPEG / PNG / WebP only,
  even though Skia opens more — the HTTP contract declares three)
- the same `SKEncodedOrigin` handling (`SwapsSides`, `ApplyOrigin`). Phone
  photos arrive rotated and Skia does not apply EXIF itself; skip this and
  every scan comes out sideways.
- the same `SKCubicResampler.Mitchell` sampling
- the same `canvas.Clear(SKColors.White)` before drawing
- promote the existing `private const int JpegQuality = 85` so both methods
  use it

#### 4.4.2 Downscaling parameters — decided

| Parameter | Value | Reason |
|---|---|---|
| Longest edge | **2000 px** | An A4 page at 2000 px on the long edge is ~170 dpi — enough to read a passport number, a diploma's fine print and a stamp, which is the only reason these files exist. 1600 px (~135 dpi) starts to smear stamps and handwritten Cyrillic. Above 2000 px the file grows quadratically for detail nobody reads on a screen. |
| JPEG quality | **85 — the existing constant, reused** | Rejected alternative: a second constant at 82 for the larger images. It buys under 15% of the file size and does not change the storage story (3-5 MB → roughly 300-500 KB either way), while adding a second number that has to be kept in sync with the first. One number, one place. |
| Output format | **always JPEG** | Rejected alternative: preserve PNG/WebP when the image has an alpha channel. A candidate attachment is a photograph or a scan; transparency carries no information there, and two output formats would mean two content types, two extensions and two code paths for zero benefit. `SquarePhotoStore` already flattens onto white for the same reason. Accepted consequence: a PNG screenshot with a transparent background gets a white background. |
| Upscaling | **never** | An image already under 2000 px is re-encoded at q85 but not resized. Re-encoding still matters: a 5 MB quality-100 JPEG at 1200 px is exactly the case the client complained about. |
| PDFs | **byte-for-byte** | The server has no PDF library, and re-encoding a document of record is the thing `DocumentFileStore` exists to prevent. |
| Limit measured on | uploaded bytes for the 20 MB per-file cap; stored bytes for the 60 MB per-candidate cap | You must reject an oversized request before decoding it; you must bound the disk by what actually lands on it. |

Expected effect, to be stated in the XML doc: a 3-5 MB phone photo of a
passport lands at roughly 300-500 KB, so ten scans cost about 4 MB instead of
about 40 MB.

### 4.5 Notes on stage-less actions

#### 4.5.1 `POST /{id}/reassign` (changed)

```csharp
public sealed record ReassignCandidateRequest(
    Guid VacancyId,
    string? Note = null,
    DateOnly? NextContactDate = null);
```

`VacancyId` stays positional with **no** default (the OpenAPI trap). Non-empty
`Note` creates a `candidate_notes` row with `kind = Reassign`. Response
`CandidateDto` (200). Errors unchanged: `vakansiya.topilmadi` (404),
`vakansiya.qabul_qilmaydi` (409), `nomzod.jarayonda_emas` (409). RBAC
`recruiting.manage`.

#### 4.5.2 `POST /{id}/return-from-reserve` (changed — gains a body)

```csharp
public sealed record ReturnCandidateFromReserveRequest(
    string? Note = null,
    DateOnly? NextContactDate = null);
```

The endpoint currently takes no body; it now takes an **optional** one
(`ReturnCandidateFromReserveRequest? request`), exactly like the three closing
endpoints take `CloseCandidateRequest?`. A caller that sends no body behaves
as before. Non-empty `Note` creates a `candidate_notes` row with
`kind = ReturnFromReserve`. Errors unchanged: `nomzod.rezervda_emas` (409),
`nomzod.topilmadi` (404).

#### 4.5.3 `POST /{id}/reject` | `/withdraw` | `/reserve` (changed)

```csharp
public sealed record CloseCandidateRequest(
    DateOnly? At = null,
    string? Reason = null,
    DateOnly? NextContactDate = null);
```

`NextContactDate` is **honoured only by `/reserve`**; `/reject` and
`/withdraw` ignore it and clear the stored date (§2.1.2). Do not return an
error for it — the frontend only renders the field for reserve, and rejecting
an ignorable field would break nothing but callers.

Why one shared record and not three: the record's own XML doc already explains
that three identical records would drift apart, and splitting it now for one
field would undo that decision. Document the "ignored by reject/withdraw"
rule in the record's XML doc **and** in the two endpoint descriptions.

#### 4.5.4 Application command records — the full list of changes

All in `api/src/Bonvi.Application/Recruiting/Candidates/`. New parameters go at
the **end** of each record; several of these are constructed positionally.

```csharp
// CandidateUseCases.cs
RecordCandidateStageCommand(..., DateOnly? NextContactDate = null, bool ClearNextContact = false)
ReassignCandidateCommand(Guid CandidateId, Guid VacancyId, string? Note = null, DateOnly? NextContactDate = null)
RejectCandidateCommand(Guid CandidateId, DateOnly? At = null, string? Reason = null)                              // unchanged
WithdrawCandidateCommand(Guid CandidateId, DateOnly? At = null, string? Reason = null)                            // unchanged
ReserveCandidateCommand(Guid CandidateId, DateOnly? At = null, string? Reason = null, DateOnly? NextContactDate = null)
ReturnCandidateFromReserveCommand(Guid CandidateId, string? Note = null, DateOnly? NextContactDate = null)
SearchCandidatesQuery(..., bool ContactDue = false, bool Rejected = false)

// InterviewNoticeUseCases.cs
InviteCandidateToInterviewCommand(Guid CandidateId, Guid? StageId, DateTimeOffset InterviewAt,
    string? Conclusion = null, DateOnly? NextContactDate = null, bool ClearNextContact = false)

// CandidateResumeUseCases.cs — DELETE the file, replace with CandidateFileUseCases.cs:
UploadCandidateFileCommand(Guid CandidateId, Stream Content, string? FileName, long SizeBytes)
GetCandidateFileQuery(Guid CandidateId, Guid FileId)
DeleteCandidateFileCommand(Guid CandidateId, Guid FileId)
CandidateFileContent(string FileName, string ContentType, Stream Content)   // renamed CandidateResumeFile
```

Note the asymmetry: the **API request records** use `bool?` for the clear flag
(the OpenAPI trap applies only to types the OpenAPI generator reflects over),
while the **Application command records** use plain `bool` with `= false`. The
endpoint converts with `?? false`. Do not "simplify" the request records to
plain `bool`.

Register the three new file handlers in `RecruitingRegistration.cs`, replacing
the three resume handlers. Register `ScaledDocumentStore` in
`Application/Common/DependencyInjection.cs` next to `DocumentFileStore`, with a
comment saying why the two exist side by side (§4.4).

`CandidateNoteKind` lives in `api/src/Bonvi.Domain/Recruiting/CandidateNote.cs`,
next to the entity, exactly as `StageOutcome` sits in `CandidateStageEntry.cs`.

### 4.6 `GET /` — candidate list (changed)

Query parameters, added:

| Param | Type | Meaning |
|---|---|---|
| `contactDue` | `bool?` | `true` → only candidates whose `next_contact_date` is set and is **today or earlier** (Tashkent). Anything else → no filter. |
| `rejected` | `bool?` | `true` → the "Rad etilganlar" section: `status IN (Rejected, Withdrawn)` and every default hide rule is off. See §4.7. |

Sort keys (`sort`), added: `nextContact`. Existing keys `createdAt` (default,
descending), `name`, `stage` are unchanged. Direction syntax is unchanged
(`-nextContact` or `nextContact:desc`).

`nextContact` ordering is explicit about NULLs, in **both** directions:

```csharp
CandidateSortField.NextContact => filter.Descending
    ? joined.OrderBy(r => r.Candidate.NextContactDate == null)
            .ThenByDescending(r => r.Candidate.NextContactDate)
    : joined.OrderBy(r => r.Candidate.NextContactDate == null)
            .ThenBy(r => r.Candidate.NextContactDate),
```

The leading `OrderBy(x => x.NextContactDate == null)` puts rows without a date
last regardless of direction. Without it PostgreSQL puts NULLs last when
ascending and **first** when descending, so "sort by next contact, newest
first" would open with a screenful of dashes.

Response: `PagedResult<CandidateListItemDto>` with `NextContactDate` appended
(§4.8). Errors unchanged: `nomzod.holat_notogri` (400),
`nomzod.manba_notogri` (400) for unknown enum text. An unknown `sort` value
still falls back to the default silently — the existing documented split
("filters error, sorts fall back").

`CandidateSearchFilter` — append the new parameters **at the end**:

```csharp
public sealed record CandidateSearchFilter(
    string? Search = null,
    Guid? VacancyId = null,
    Guid? StageId = null,
    CandidateStatus? Status = null,
    CandidateSource? Source = null,
    int Page = 1,
    int PageSize = 25,
    CandidateSortField Sort = CandidateSortField.CreatedAt,
    bool Descending = true,
    bool IncludeArchive = false,
    DateOnly? ContactDueOn = null,     // new
    bool RejectedSection = false);     // new
```

Appending is mandatory: `SearchCandidatesQueryHandler` constructs this record
**positionally**, so inserting a parameter in the middle would silently shift
every argument.

`ContactDueOn` carries a date, not a boolean, because `RecruitingReadModel`
takes only `AppDbContext` and has no clock. `SearchCandidatesQueryHandler`
therefore gains an `IClock` dependency and passes
`query.ContactDue == true ? _clock.Today : null`. Rejected alternative:
inject `IClock` into the read model — the read model deliberately knows
nothing but the query, and "today" is a decision the use-case layer already
owns everywhere else in this codebase.

`CandidateSortField` gains `NextContact = 3` (at the end).
`RecruitingQueryParsing.ParseCandidateSort` gains `"NEXTCONTACT" => (CandidateSortField.NextContact, descending)`.

### 4.7 The "Rad etilganlar" section — filter contract

**Decision: a dedicated section flag, `rejected=true`.**

Rejected alternative: a multi-value status filter (`?status=Rejected,Withdrawn`).
It forces `CandidateSearchFilter.Status` to become a collection,
`ParseCandidateStatus` to parse lists, and — worst — it leaks a multi-value
concept into a single-`<select>` toolbar that cannot represent it. The URL
schema (`status?: CandidateStatus`) and `searchToFilters` / `filtersToSearch`
would all have to handle a value the UI can never produce.

The section flag composes cleanly with the status dropdown, which is the
requirement's other half:

| Request | Result |
|---|---|
| no `status`, no `rejected` | everything except `Hired`, `Rejected`, `Withdrawn` (and except the Excel archive) |
| `rejected=true` | exactly `Rejected` + `Withdrawn` |
| `rejected=true&status=Withdrawn` | `status IN (Rejected, Withdrawn) AND status = Withdrawn` → only withdrawn |
| `status=Withdrawn` (no section) | only withdrawn — the existing "status chosen ⇒ hide rules off" rule |

Hide-rule properties on `CandidateSearchFilter`:

```csharp
public bool HideArchive => !IncludeArchive && Source is null;          // unchanged
public bool HideHired => Status is null;                               // unchanged — see note
public bool HideRejected => Status is null && !ArchiveRequested && !RejectedSection;
public bool HideWithdrawn => Status is null && !ArchiveRequested && !RejectedSection;   // new
private bool ArchiveRequested => IncludeArchive || Source == CandidateSource.Excel;     // unchanged
```

- `HideHired` is deliberately **not** given a `&& !RejectedSection` clause. In
  the section the `IN (Rejected, Withdrawn)` predicate already excludes hired
  candidates, so the extra `status != Hired` is redundant defence that changes
  no result; adding the clause would be a change with no observable effect.
- `HideWithdrawn` carries `!ArchiveRequested` for the same reason
  `HideRejected` does: the Excel council archive is almost entirely closed
  records, and a hide rule that survives into the archive would make the
  archive render short or empty. This is the existing carve-out the
  requirement asked to preserve — and it must now cover **both** statuses.
- **`HideArchive` still applies inside the section**, and that is intended:
  `rejected=true` alone does **not** pull the 3110 Excel council rows into
  "Rad etilganlar". The section answers "who did we turn down recently"; the
  archive has its own entry point (`source=Excel`). If someone later reports
  "the rejected section is missing the old records", the answer is the source
  filter, not a change here.

`RecruitingReadModel.SearchCandidatesAsync` — add, in the same block as the
existing hide filters and **before** the count is taken (the existing comment
explains why: `total` and the page must come from one set):

```csharp
if (filter.RejectedSection)
{
    candidates = candidates.Where(c =>
        c.Status == CandidateStatus.Rejected || c.Status == CandidateStatus.Withdrawn);
}

if (filter.HideWithdrawn)
{
    candidates = candidates.Where(c => c.Status != CandidateStatus.Withdrawn);
}

if (filter.ContactDueOn is { } due)
{
    candidates = candidates.Where(c => c.NextContactDate != null && c.NextContactDate <= due);
}
```

#### 4.7.1 The contradicting remark that must be rewritten

`api/src/Bonvi.Application/Recruiting/Abstractions/IRecruitingReadModel.cs`
currently says, inside the `HideRejected` remark:

> ⚠️ «O'zi voz kechgan» (`CandidateStatus.Withdrawn`) va rezervdagilar bu
> qoidaga TUSHMAYDI: birinchisi kam uchraydi, ikkinchisining butun ma'nosi
> «kerak bo'lganda shu yerdan chaqiramiz» degani.

After this change that sentence is false. **Rewrite it** — do not leave it,
do not just delete it. The replacement must say, in the same prose register as
the rest of the file:

- `Withdrawn` **is** now subject to the rule (user decision, 2026-09-10): the
  everyday list answers "who is in play right now", and someone who walked
  away is not. "It is rare" turned out to be wrong in practice.
- `Withdrawn` and `Rejected` remain **separate statuses** — separate badges,
  separate funnel reporting. Only their list visibility is shared. Point at
  the `CandidateStatus.Withdrawn` remark for why the split exists.
- `Reserve` is **still not** subject to the rule, and the reason is unchanged:
  the entire meaning of reserve is "we call them from this list when a slot
  opens", so hiding them destroys the list.

Also update, in the same file and in `SearchCandidatesQuery`'s XML docs, every
sentence that currently enumerates "hired and rejected are hidden by default"
to read "hired, rejected and withdrawn".

### 4.8 DTO changes — complete list

`api/src/Bonvi.Application/Recruiting/Dtos/RecruitingDtos.cs`:

- **`CandidateDto`**
  - removed: `ResumeFileName`, `ResumeContentType`, `ResumeSizeBytes`
  - added: `DateOnly? NextContactDate`, `IReadOnlyList<CandidateFileDto> Files`,
    `IReadOnlyList<CandidateNoteDto> Notes`
- **`CandidateListItemDto`** — added at the end: `DateOnly? NextContactDate`.
  `HasResume` keeps its name and its position; only its computation changes to
  "at least one file". Renaming it would break the TS reader and
  `CandidateQuestionnaireApiTests`, and the project rule is additive change —
  so update the XML doc and the TS doc instead. Do **not** add a file count to
  the list row: the table shows a paperclip, not a number.
- **new `CandidateFileDto`** — §4.3.4
- **new `CandidateNoteDto`**:

```csharp
/// <param name="Id">Note id.</param>
/// <param name="Kind">Which action produced it.</param>
/// <param name="Text">The note itself — INTERNAL, never sent to the candidate.</param>
/// <param name="CreatedBy">Which system user wrote it.</param>
/// <param name="CreatedAt">When.</param>
public sealed record CandidateNoteDto(
    Guid Id,
    CandidateNoteKind Kind,
    string Text,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt);
```

- **`RejectionNoticeDto` / `InterviewNoticeDto` / `ApplicationCheckDto` /
  `CandidateApplicationRow` — unchanged.** See §7.

`IRecruitingReadModel`:

- `CandidateRow` — append `DateOnly? NextContactDate` at the end. It is built
  positionally in `RecruitingReadModel`, so appending is mandatory.
- `HasResume` in the projection becomes
  `_context.CandidateFiles.Any(f => f.CandidateId == candidate.Id)`.

`RecruitingMapper.ToDtoAsync(Candidate, ...)` maps the two new collections. It
needs no new dependency: files and notes carry no reference-data names.
Order files by `CreatedAt` ascending and notes by `CreatedAt` ascending — the
repository already loads them that way, so just project in order.

---

## 5. Frontend

All paths under `web/src/features/recruiting/` unless stated.

### 5.1 Dialogs

#### 5.1.1 `components/candidate-stage-dialog.tsx` — extended, not replaced

New optional prop:

```ts
/** When set, the stage is fixed and the picker is not drawn. */
lockedStage?: PipelineStage | null
```

Behaviour when `lockedStage` is set:

- title becomes `t('recruiting.stageMoveTo', { stage: lockedStage.name })`
- the stage `<Select>` is replaced by a static line showing the stage name
- `stageId` is initialised from `lockedStage.id` and never changes

**Why extend this dialog instead of writing a quick one.** The two dialogs
would ask for the same five things and drift apart, which is the exact reason
`CandidateActionDialog` handles six actions and `CloseCandidateRequest` serves
three endpoints. One dialog, one submit path, one set of fields.

Fields, in order, in both modes:

| # | Field | Control | Default | Required |
|---|---|---|---|---|
| 1 | Stage | `Select` (active stages only) / static line when locked | current stage, or `presetStageId` | yes |
| 2 | Outcome | `Select` over `STAGE_OUTCOMES` | `Pending` | yes |
| 3 | Date | `TextField type="date"` | today | yes |
| 4 | Conclusion | `TextArea`, `maxLength={4000}` | empty | **no** |
| 5 | Next contact | `TextField type="date"` | `candidate.nextContactDate ?? ''` | **no** |

**"Saqlash" with an empty conclusion must save.** No client-side validation on
field 4, no asterisk, no disabled button. The reason is already written in the
domain: forcing it produces fake "ok" conclusions
(`CandidateStageEntry.Conclusion` remark).

Submit payload:

```ts
{
  id, stageId, outcome, date: orNull(date), conclusion: orNull(conclusion),
  nextContactDate: orNull(nextContact),
  clearNextContact: nextContact === '' && candidate.nextContactDate !== null,
}
```

That last line is the whole reason the API has two fields: an empty box means
"clear it" only when there was something to clear, so a dialog opened on a
candidate with no date never sends a clear.

The existing reset `useEffect` runs on `[open, presetStageId, candidate.currentStageId]`.
Extend it to `[open, presetStageId, lockedStage, candidate.currentStageId, candidate.nextContactDate]`
and have it seed `stageId` as `lockedStage?.id ?? presetStageId ?? candidate.currentStageId ?? ''`
and `nextContact` as `candidate.nextContactDate ?? ''`. Missing either
dependency leaves a stale stage or a stale date in a re-opened dialog, and the
second one is the dangerous case: a stale empty box would send
`clearNextContact: true`.

#### 5.1.2 `components/candidate-stage-picker.tsx` — every button opens the dialog

- Remove the `useRecordStage()` hook and the direct `record.mutate({...})`
  call — this component no longer performs a mutation, it only opens dialogs.
  Remove the `Alert` that displayed `record.error` with it; the error now
  belongs to the dialog, which already renders one.
- Add a second piece of state alongside the existing
  `inviting: PipelineStage | null | 'closed'`:
  `const [recording, setRecording] = useState<PipelineStage | null>(null)`
  (`null` = closed; a two-state variable is enough here because, unlike the
  interview dialog, this one is never opened without a stage).
- `move(stage)` becomes:
  `stage.invitesToInterview ? setInviting(stage) : setRecording(stage)`.
- Render `<CandidateStageDialog candidate={candidate} lockedStage={recording}
  open={recording !== null} onClose={() => setRecording(null)} />`.
- The per-button spinner and the `disabled={record.isPending}` on every button
  disappear: there is no in-flight mutation until the dialog submits, and the
  dialog's own Save button owns the pending state. Keep the `CalendarClock`
  icon on stages flagged `invitesToInterview`.
- The current-stage chip stays a non-button `<span>` for the documented
  reason (a disabled button reads as broken).
- **New "what happened last" strip**, above the button row — see §5.2.
- `t('recruiting.stageMoveHint')` must change value: it currently promises
  "one click — the date is set to today; add the outcome and conclusion later
  on the Stages tab", which is now false.

#### 5.1.3 `components/candidate-interview-dialog.tsx` — extended

- The `Conclusion` `TextArea` is now rendered **always**, including when
  `stage === null` (rescheduling). Remove the `{stage !== null && ...}` guard
  and the comment that justified it — the server now stores the reschedule
  note as a candidate note (§4.2).
- Add the same next-contact `TextField` and the same `clearNextContact`
  computation as §5.1.1.
- Everything else is unchanged, including the manual `+05:00` offset on
  `interviewAt` (the comment explains why: without it the candidate is invited
  five hours early) and the "a sent message cannot be recalled" warning above
  the confirm button.
- `t('recruiting.interviewNoteHint')` value changes to say the note stays
  internal in every case, not only "goes to the stage timeline".

#### 5.1.4 `components/candidate-action-dialog.tsx` — note fields for two more actions

Current field matrix: `reject` / `withdraw` / `reserve` get a date + reason;
`reassign` gets a vacancy `Select`; `returnFromReserve` gets nothing.

New matrix:

| Action | Date | Reason / Note | Vacancy | Next contact |
|---|---|---|---|---|
| `reject` | yes | `reason` | — | — |
| `withdraw` | yes | `reason` | — | — |
| `reserve` | yes | `reason` | — | **yes** |
| `reassign` | — | **`note`** | yes | **yes** |
| `returnFromReserve` | — | **`note`** | — | **yes** |

- Reuse the existing labels: `t('recruiting.reason')` for the closing actions,
  `t('recruiting.note')` for `reassign` / `returnFromReserve`, and
  `t('recruiting.candidateReasonHint')` ("internal only, not sent to the
  candidate") under both. No new label keys.
- `TextArea maxLength={2000}` for the note, `maxLength={2000}` for the reason.
- The confirm-button colour rule (`REVERSIBLE`) is unchanged.
- `useCloseCandidate` sends `nextContactDate` only for `reserve`;
  `useReassignCandidate` and `useReturnCandidateFromReserve` send
  `{ note, nextContactDate }`.
- Reset all new fields in the existing `useEffect` that runs on
  `[action, candidate.id]`.

#### 5.1.5 What the dialogs must not do

No dialog may show, mention, or offer to send any of these to the candidate.
The conclusion, the note and the next-contact date are internal (§7). The
existing "the candidate does NOT see it" hints are the pattern to follow.

#### 5.1.6 The funnel drag-and-drop stays one-click — decided

`useMoveCandidateToStage` in `api/recruiting-queries.ts` and
`components/candidate-funnel.tsx` are **unchanged**. Dragging a card still
sends `outcome: 'Pending'`, `date: null`, `conclusion: null` and now also
omits both next-contact fields (so the stored date is untouched, §4.1).

Reasons:

1. The client's complaint is about the path HR uses after a phone call — the
   quick row on the candidate card. That path now asks for the note.
2. A modal would destroy the optimistic patch the drag is built around; the
   hook's XML doc explains that the whole value of the gesture is its speed
   and that waiting for a response cancels it.
3. The board is a triage surface, not a recording surface. An entry created by
   a drag is `Pending` and shows the "Record the outcome" button in the
   timeline, so the note can still be written.

Rejected alternative: open the dialog after a successful drag. It puts a modal
in front of a gesture whose point is that there is no modal, and HR would
learn to dismiss it.

### 5.2 The "what happened last" strip

Requirement: HR must see what was done before pressing the next button.

**Decision: inline, in `candidate-stage-picker.tsx`, above the button row.**

Content — the single most recent item that carries text, chosen from both
streams:

- stage entries with `conclusion !== null`, keyed on `recordedAt`
- candidate notes, keyed on `createdAt`

Render: a label (the stage name, or `t('recruiting.noteKind.<kind>')` for a
note), the date, and the text clamped to three lines
(`line-clamp-3` / the project's `truncate-*` utility). Heading
`t('recruiting.lastActivity')`.

If neither stream has text, render nothing — no empty box. The codebase does
this consistently (`CandidateQuestionnaireCard` is not drawn at all when there
is no questionnaire; the stage picker returns `null` on an empty catalogue).

Plus a "contact due" chip when
`candidate.nextContactDate !== null && candidate.nextContactDate <= todayIso()`:
`t('recruiting.nextContactDue')`, warning tone. The chip appears **only** when
the date is due — the neutral value already lives in `PersonalCard` (§5.3) and
repeating it twice on one screen would be noise.

Rejected alternative: send HR to the "Bosqichlar" tab. That is today's
workflow and the reason nothing gets written.

### 5.3 `components/candidate-card-page.tsx`

- `PersonalCard` gains one `Field`: `t('recruiting.nextContact')` →
  `candidate.nextContactDate === null ? null : date(candidate.nextContactDate)`.
  It renders `—` when empty, matching the card's documented rule that empty
  fields are still shown ("knowing it is missing is information too").
  This field is visible to `recruiting.view` holders, which is why it is here
  and not only in the manager-only stage picker.
- Swap the import `CandidateResumeCard` → `CandidateFilesCard` (§5.4). Its
  position in the "Overview" tab is unchanged: after the questionnaire, before
  the stage picker.
- Everything else — tabs, closing alert, action button order and their
  reasons — is unchanged.

### 5.4 `components/candidate-files-card.tsx` — new (replaces the resume card)

Delete `components/candidate-resume-card.tsx`. This is not unrequested
refactoring: the component reads `candidate.resumeFileName` /
`resumeContentType` / `resumeSizeBytes`, all three of which no longer exist,
so it cannot compile. Update the single import in `candidate-card-page.tsx`.

Structure:

- `CardHeader` — title `t('recruiting.filesTitle')`, subtitle
  `t('recruiting.filesCount', { n, max })` when there are files, otherwise
  `t('recruiting.filesEmpty')`. Action button `t('recruiting.filesAdd')`,
  shown only when `canManage`.
- Hidden `<input type="file" multiple accept={RESUME_ACCEPT}>` — the same
  hidden-input-plus-real-button pattern the current card uses, and for the
  same documented reason (the native file control does not match the rest of
  the UI).
- At `candidate.files.length >= MAX_CANDIDATE_FILES` the button is disabled
  and `t('recruiting.filesFullHint')` is shown.
- Upload loop: files are validated client-side with the existing
  `resumeFileError`, then uploaded **sequentially, one request each**. Track
  per-file state so a failure names the file that failed and the others still
  land. Cap the selection at `MAX_CANDIDATE_FILES - files.length` and report
  the surplus with `nomzod.rezyume_kop` (the same code the server returns, so
  the message is already translated — the pattern `resume-file.ts` documents).
- One row per file, ordered as the server sent them (upload order):
  type icon (`iconFor`, unchanged), name, size (`resumeSizeParts`; render `—`
  when `sizeBytes === 0`), upload date, then a download button and — for
  managers — a delete button with the existing confirm dialog
  (`t('recruiting.filesDeleteTitle')` / `filesDeleteText`).
- `t('recruiting.filesUploadHint')` under the header states the rules in the
  client's language: PDF or image, 20 MB each, images are downscaled on the
  server.

#### 5.4.1 Clicking an image opens it large — consistency rule

Commit `a3590860` ("Rasm ustiga bosish — katta ko'rish, yuklash emas")
established: clicking an image views it, it does not download it. Today that
rule exists only for the avatar (`candidate-photo-editor.tsx`), because the
resume card is download-only and its comment justifies having no separate
"view" button.

**Decision: extend the rule to image files.**

- A row whose `contentType` starts with `image/`: clicking the row (or its
  icon) opens a `Dialog` (`t('recruiting.filesViewTitle')`) that shows the
  full image, fetched as a blob through the download endpoint and turned into
  a URL with `useObjectUrl`. The dialog carries a `t('documents.download')`
  button. Draw the image rectangular, not clipped — the same lesson as
  `a3590860` (the stored file is not a square).
- A PDF row: clicking downloads. There is no in-app PDF viewer, and the
  browser or OS opens the downloaded file — the argument the current resume
  card already makes.
- **No thumbnails in the list.** Rejected because rendering ten thumbnails
  means ten authenticated blob fetches every time the card opens, which would
  be the slowest thing on the screen. The card is a file list, not a gallery.

#### 5.4.2 `lib/resume-file.ts` — extended, not renamed

Keep `RESUME_TYPES`, `RESUME_ACCEPT`, `RESUME_MAX_BYTES`, `resumeFileError`,
`resumeSizeParts` and the file name — everything still applies per file and
the error codes are unchanged. Add:

```ts
/** Server rule — `Candidate.MaxFiles`. */
export const MAX_CANDIDATE_FILES = 10
```

Do **not** add a client-side total-size check. The client cannot know the
stored size before the server downscales: a 5 MB photo becomes roughly
300 KB, so a client-side total would reject uploads the server would happily
accept. The 60 MB rule is the server's alone; the client just surfaces
`nomzod.rezyume_joy` when it comes back.

### 5.5 List page, toolbar, URL schema, navigation

#### `lib/candidates-search.ts`

- `CandidatesSearch` gains `due?: true` and `rejected?: true`. Both are
  omitted from the URL when false — the existing `readView` pattern.
- `CandidatesSortKey` gains `'nextContact'`; add it to `SORT_KEYS`.
- `CandidatesFilterState` gains `contactDue: boolean`; `DEFAULT_FILTERS` gains
  `contactDue: false`.
- `validateCandidatesSearch` reads both new params, dropping anything that is
  not the literal `true` (the file's rule: never throw, drop bad values).
- `searchToFilters` / `filtersToSearch` map `due` ↔ `contactDue`.
- **`isFiltered` must count `due` and must NOT count `rejected`.** `due` is a
  filter (it can produce an empty result the user caused); `rejected` is the
  *section* (an empty result there means "nobody was rejected", not "your
  search found nothing"). The page already makes this distinction for
  `status`; the new flag replaces that special case.

#### `components/candidates-page.tsx`

- `const rejectedSection = search.rejected === true` — replaces
  `filters.status === 'Rejected'`.
- `const emptySection = rejectedSection && !isFiltered(search)` — simpler than
  today's `isFiltered({ ...search, status: undefined })`, because `isFiltered`
  now ignores `rejected` by construction.
- New table column, placed **between `currentStage` and `status`** (it belongs
  next to "where are they now", not at the far right where it would be cut off
  on a laptop):

```ts
helper.accessor('nextContactDate', {
  id: 'nextContact',
  header: t('recruiting.nextContact'),
  meta: { width: '140px' },
  // sortable — enableSorting defaults to true and the server understands `nextContact`
  cell: /* DateCell, with warning-toned text when the date is today or earlier */,
})
```

  Due and overdue dates are rendered in the warning colour so the "who do I
  call" answer is visible without sorting. `null` renders `—`.
  ⚠️ The existing local `DateCell` helper is typed `{ value: string }` and only
  handles `''`. Widen it to `string | null` (or add a sibling helper) —
  `nextContactDate` is genuinely nullable and passing `null` into today's
  version renders an invalid date, not a dash.
- Header, icon (`UserRoundX`), subtitle, empty states and the hidden
  "Add a candidate" button in the section are unchanged. The section keeps the
  label "Rad etilganlar" even though it now also lists people who withdrew —
  that is exactly what the client asked for, and the per-row status badge
  (red vs yellow) is what distinguishes them.
- `sharedFilters` gains `...(filters.contactDue ? { contactDue: true } : {})`
  and `...(rejectedSection ? { rejected: true } : {})`.
- **`funnelFilters` must treat the section flag as an explicit choice.** Today
  the board forces `status: 'InProgress'` when no status is chosen. If it kept
  doing that inside the section, the request would be
  `rejected=true AND status=InProgress` — an empty board, every time. So:

```ts
const funnelFilters = useMemo(
  () => ({
    ...sharedFilters,
    ...(rejectedSection || filters.status !== '' ? {} : { status: 'InProgress' as const }),
  }),
  [sharedFilters, filters.status, rejectedSection],
)
```

  This is the same rule the existing comment already states — a deliberate
  choice is respected, because "which stage did the people we turned down stop
  at" is one of the funnel's most useful questions. The section flag is such a
  choice; it just is not spelled as a status any more.

#### `components/candidates-toolbar.tsx`

- New control in the collapsible panel: a `Checkbox` (or `Switch`) labelled
  `t('recruiting.contactDue')` with hint `t('recruiting.contactDueHint')`.
  `web/src/shared/ui/checkbox.tsx` and `switch.tsx` both exist — use
  `Checkbox`, it reads as a filter rather than a setting.
- `activeFilterCount` must count it, otherwise the badge on the "Filters"
  button lies and HR cannot see why the list is short.
- `t('recruiting.statusFilterHint')` value changes: hired, rejected **and
  withdrawn** are hidden by default.

#### `web/src/app/layout/nav-items.ts`

```ts
{ key: 'rejectedCandidates', to: '/nomzodlar', icon: UserRoundX,
  search: { rejected: 'true' }, permission: 'recruiting.view' },
```

Replaces `search: { status: 'Rejected' }`.

⚠️ **The value is the string `'true'`, not the boolean `true`, and this is
correct — do not "fix" it.** `NavItem.search` is typed
`Readonly<Record<string, string>>` and `sidebar.tsx`'s `matches()` compares
with `String(actual[key] ?? '') === value`. TanStack Router serialises
`{ rejected: 'true' }` to `?rejected=true`, and its default parser
(`JSON.parse` per value) reads that back as the **boolean** `true` — which is
what `validateCandidatesSearch` receives and what `String(true) === 'true'`
matches. The existing `page` param already relies on the same parser
(`readPage` checks `typeof value !== 'number'`). Widening `NavItem.search` and
touching `sidebar.tsx` would be a change to two shared files for no gain.

Update the block comment above the item: it now opens rejected **and**
withdrawn, both are hidden from the everyday list, and the two statuses stay
distinct. An old bookmark pointing at `?status=Rejected` still works — it shows
only rejected candidates, which is a correct if narrower answer. Say so in the
comment.

### 5.6 `api/recruiting-api.ts` and `api/recruiting-queries.ts`

`api/recruiting-api.ts`:

- `interface Candidate`: drop `resumeFileName`, `resumeContentType`,
  `resumeSizeBytes`; add `nextContactDate: string | null`,
  `files: CandidateFile[]`, `notes: CandidateNote[]`.
- `interface CandidateListItem`: add `nextContactDate: string | null`.
  `hasResume` stays; update its doc comment to "at least one file attached".
- New `interface CandidateFile { id, fileName, contentType, sizeBytes, uploadedAt }`.
- New `interface CandidateNote { id, kind, text, createdBy, createdAt }`.
- New `export const CANDIDATE_NOTE_KINDS = ['Reassign', 'ReturnFromReserve', 'InterviewRescheduled'] as const`
  plus `export type CandidateNoteKind = (typeof CANDIDATE_NOTE_KINDS)[number]`.
  Read it with the existing `oneOf(...)` helper. **Important:** `oneOf` falls
  back silently, so a value missing from this array becomes the first one and
  the timeline mislabels the row — the exact failure `CANDIDATE_STATUSES`
  documents for `Reserve`. Keep the array in sync with the C# enum.
- `toCandidate` / `toCandidateListItem`: read the new fields with the existing
  `text` / typed helpers; arrays default to `[]` when absent.
- `CandidateFilters` gains `contactDue?: boolean` and `rejected?: boolean`;
  `fetchCandidates` puts them in the query string only when `true`.
- Replace `uploadResume` / `fetchResume` / `deleteResume` with
  `uploadCandidateFile(id, file)` → `CandidateFile`,
  `fetchCandidateFile(id, fileId)` → `{ blob, disposition }`,
  `deleteCandidateFile(id, fileId)` → `void`. Keep the two comments that
  explain (a) why `Content-Type` is not set manually on `FormData` and (b) why
  files are fetched as blobs rather than linked with `<a href>` (the
  `Authorization` header cannot ride on a plain link).
- `recordCandidateStage`, `inviteCandidateToInterview`, `reassignCandidate`,
  `returnCandidateFromReserve`, `closeCandidate`: add the new body fields.
  `returnCandidateFromReserve` changes from "no body" to "optional body".

`api/recruiting-queries.ts`:

- Replace `useUploadResume` / `useDownloadResume` / `useDeleteResume` with
  `useUploadCandidateFile` / `useDownloadCandidateFile` / `useDeleteCandidateFile`.
  Keep using `fileNameFromDisposition` + `saveBlobAs` for downloads.
- Add `useCandidateFileBlob(candidateId, fileId, enabled)` for the lightbox.
  Its cache key **must end in `-photo`** — e.g.
  `['candidate-file-photo', candidateId, fileId]` — because `providers.tsx`
  invalidates the whole cache after every successful write except keys whose
  root ends in `-photo`. Without that suffix, every stage note written on the
  card would re-download the open image. Document the reason at the key, as
  `recruitingKeys.photo` already does.
- `useRecordStage`, `useInviteToInterview`, `useReassignCandidate`,
  `useReturnCandidateFromReserve`, `useCloseCandidate`: widen their variable
  types for the new fields.
- `useMoveCandidateToStage` — untouched (§5.1.6).
- Do **not** add manual `invalidateQueries` calls; `providers.tsx` handles it,
  and the file's comment explains why hand-written invalidation lists rot.

### 5.7 i18n — all four locale files

Files: `web/src/shared/i18n/locales/{en,ru,uz-Cyrl,uz-Latn}.json`.
`uz-Latn.json` is the reference file for `scripts/check-locales.mjs`; a key
present in one file and missing in another fails `./bonvi check`.

**Added** under `recruiting.`:

| Key | English text (translate the other three) |
|---|---|
| `nextContact` | Next contact |
| `nextContactHint` | Leave empty if no callback is planned |
| `nextContactDue` | Contact due |
| `contactDue` | Contact needed today |
| `contactDueHint` | Today or overdue |
| `lastActivity` | What happened last |
| `stageMoveTo` | Move to “{{stage}}” |
| `noteKind.reassign` | Moved to another vacancy |
| `noteKind.returnFromReserve` | Returned from reserve |
| `noteKind.interviewRescheduled` | Interview rescheduled |
| `filesTitle` | Files |
| `filesEmpty` | No files attached |
| `filesAdd` | Add files |
| `filesCount` | {{n}} of {{max}} |
| `filesFullHint` | The 10-file limit is reached — delete one before adding another |
| `filesUploadHint` | PDF or image, up to 20 MB each. Images are downscaled on the server. |
| `filesDeleteTitle` | Delete the file? |
| `filesDeleteText` | The file is removed from both the database and the disk. This cannot be undone. |
| `filesViewTitle` | Image |

**Changed value, same key** (all four files):

| Key | Why |
|---|---|
| `stageMoveHint` | no longer one click; now "each button opens a short form: date, outcome, note and the next contact date" |
| `statusFilterHint` | must now say hired, rejected **and withdrawn** are hidden |
| `candidateNotFoundText` | same correction |
| `candidateNoRejectedText` | the section now holds both "we said no" and "they withdrew" |
| `hasResume` | paperclip aria-label → "Has attached files" |
| `interviewNoteHint` | the note stays internal in every case, not only when it lands on the stage timeline |

**Removed** from all four files: `recruiting.resume`, `recruiting.resumeEmpty`,
`recruiting.resumeReplace`, `recruiting.resumeUpload`,
`recruiting.resumeDeleteTitle`, `recruiting.resumeDeleteText`. Their component
is gone; `check-locales.mjs` reports keys present in one file and absent in
another, so all four must be edited together.

**Added** under `errors.` (backend codes — `check-locales.mjs` part 3 fails
the build if a backend `Error.*` code has no translation):

| Code | English |
|---|---|
| `nomzod.rezyume_kop` | A candidate can have at most 10 files. Delete one before adding another. |
| `nomzod.rezyume_joy` | The candidate's files exceed the total size limit (60 MB). |

Reused without change: `nomzod.rezyume_katta`, `nomzod.rezyume_yaroqsiz`,
`nomzod.rezyume_topilmadi`, `nomzod.rezyume_bosh`, `nomzod.topilmadi`,
`nomzod.jarayonda_emas`, `nomzod.rezervda_emas`, `bosqich.topilmadi`,
`bosqich.faol_emas`.

Reused labels — do **not** invent duplicates: `recruiting.note`,
`recruiting.reason`, `recruiting.candidateReasonHint`,
`recruiting.conclusion`, `recruiting.conclusionPlaceholder`,
`recruiting.stageDate`, `recruiting.outcome.*`, `common.save`,
`common.cancel`, `common.delete`, `documents.download`.

### 5.8 `web/scripts/check-locales.mjs`

`t(\`recruiting.noteKind.${kind}\`)` is a template call. Add a
`DYNAMIC_KEY_SOURCES` entry mirroring the four existing recruiting entries:

```js
{
  file: 'src/features/recruiting/api/recruiting-api.ts',
  values: (text) => constArrayValues(text, 'CANDIDATE_NOTE_KINDS'),
  keysFor: (value) => [`recruiting.noteKind.${lowerFirst(value)}`],
},
```

Without it the script only prints an "unrecognised template" warning (it does
not fail), so a missing translation would ship as a raw key on screen — the
failure mode the script's own header describes.

---

## 6. UI permission matrix

| Surface | `recruiting.view` | `recruiting.manage` |
|---|---|---|
| Candidate list, next-contact column, "contact needed today" filter | visible | visible |
| "Rad etilganlar" section (nav item + page) | visible | visible |
| Next-contact field in `PersonalCard` | visible, read-only | visible, read-only |
| Files card — list, download, image lightbox | visible | visible |
| Files card — "Add files", delete | hidden | visible |
| Stage picker (whole card) + the "what happened last" strip | hidden | visible |
| Stage dialog, interview dialog, action dialogs | hidden | visible |
| Stages tab timeline | visible | visible + "Record the outcome" button |

The stage-picker card is already gated on `canManage && inProgress` in
`candidate-card-page.tsx`; that is why the neutral next-contact value lives in
`PersonalCard` (§5.3) — a view-only user would otherwise never see it.

**Do not build a second home for candidate notes to close the view-only gap.**
Candidate notes appear only in the manager-only strip (§5.2), so in theory a
`recruiting.view`-only user cannot read them. In practice that user does not
exist: every seeded role that has `recruiting.view` also has
`recruiting.manage` — `SuperAdmin`, `MainBoss` (everything except
`settings.manage`), `HrManager` and `RecruitingBot` (checked in
`api/src/Bonvi.Domain/Identity/Permissions.cs`). The gap is reachable only via
a hand-made custom role, and solving it would mean a second rendering of the
same text on the same screen.

No new permission. The recruiting module is deliberately two permissions wide
and `CandidateEndpoints`' remark explains why a third would be dead weight.

---

## 7. Internal-only fields — never sent to the candidate

**Client-stated requirement.** These fields are internal and must never reach
the Telegram bot:

- `CandidateStageEntry.Conclusion` — every stage conclusion
- `CandidateNote.Text` — every reassign / return-from-reserve / reschedule note
- `Candidate.NextContactDate`
- `Candidate.ClosingNote` and `Candidate.Note` (already internal today)
- `candidate_files` — no file, name or size is ever sent

Verified against the code, not assumed:

- The bot consumes exactly four endpoints:
  `GET /candidates/application-check`, `GET /candidates/rejection-notices`,
  `GET /candidates/interview-notices`, plus the two `*-notice-sent` marks
  (`api/src/Bonvi.Bot/Bonvi/BonviClient.cs`).
- `RejectionNoticeDto` carries `CandidateId, ChatId, Language, FullName, VacancyTitle`
  — nothing else. Its XML doc already states that phone, address and internal
  notes are deliberately absent.
- `InterviewNoticeDto` adds only `InterviewAt`.
- `CandidateApplicationRow` carries `Status, ClosedAt, AppliedAt, VacancyTitle,
  StageName` and `ApplicationCheckDto` carries `Allowed, Reason, AppliedAt,
  VacancyTitle, StageName, AvailableFrom` — no note, no callback date, no file.
  Both records' XML docs already state that internal notes are deliberately
  excluded.
- `BotTexts.RejectionNotice(fullName)` takes **one** argument and the message
  is a fixed bilingual template. Its remark records the 2026-08-31 decision:
  the rejection reason is not merely omitted from the message, it is not even
  given to the bot, because otherwise HR would start writing every internal
  note as if the candidate would read it.
- `BotTexts.InterviewNotice(...)` takes the interview instant plus two values
  from system settings (`bot.interview.phone`, `bot.interview.address`).

**These four DTOs must not gain a single field in this release.** A
`test-backend` assertion must pin that (§8.1). The mechanism that keeps the
promise is the DTO boundary itself: the bot never reads `CandidateDto`, so
adding `notes` and `nextContactDate` there cannot leak.

---

## 8. Test plan

### 8.1 `test-backend`

**Domain (`api/tests/Bonvi.Domain.Tests/Recruiting/CandidateTests.cs`)**

- `AttachFile` accepts up to ten files; the eleventh returns
  `nomzod.rezyume_kop`
- `AttachFile` returns `nomzod.rezyume_joy` when the stored total would exceed
  `MaxFilesTotalBytes`; a file that exactly reaches the limit is accepted
- `AttachFile` returns `nomzod.rezyume_bosh` for a blank key, a blank content
  type or a non-positive size
- `AttachFile` works on a **closed** candidate (a diploma copy arrives after
  the rejection)
- `RemoveFile` removes the row, returns the key for disk deletion, and frees a
  slot so an eleventh upload then succeeds; an unknown id returns
  `nomzod.rezyume_topilmadi`
- `RemoveResume` / `AttachResume` and the four properties no longer exist
  (the old tests must be replaced, not deleted-and-forgotten)
- `SetNextContactDate(null)` clears; a past date is **accepted** (§2.1.3)
- `Reject`, `Withdraw`, `MarkHired` clear `NextContactDate`
- `MoveToReserve` **keeps** `NextContactDate`, and sets it when given one
- `ReturnFromReserve` clears `ClosedAt` / `ClosingNote` (existing) and leaves
  `NextContactDate` untouched when none is supplied
- `AddNote` trims, stores the kind, and refuses to create a row for
  whitespace-only text
- `RecordStage`'s "fill the last Pending entry instead of appending" behaviour
  is unchanged — a regression here doubles every timeline

**Filter rules (`api/tests/Bonvi.Application.Tests/Recruiting/CandidateSearchFilterTests.cs`)**

Extend the existing file; it is cheap and touches no database.

- default filter: `HideHired`, `HideRejected`, **`HideWithdrawn`**, `HideArchive`
  all true
- an explicit `Status` opens all hide rules, including for `Withdrawn`
- `RejectedSection = true` ⇒ `HideRejected` and `HideWithdrawn` false
- **`Source = Excel` and `IncludeArchive = true` each leave `HideWithdrawn`
  false** — the archive carve-out now covers both statuses, and this is the
  test that stops the archive rendering short
- `Source = TelegramBot` (any non-archive source) leaves both hide rules
  **true** — the existing rule that only a *status* choice opens them
- `ContactDueOn` defaults to `null`

**Handlers (`api/tests/Bonvi.Application.Tests/Recruiting/CandidateHandlerTests.cs`)**

- stage recording with a note and a next-contact date persists both
- `clearNextContact = true` clears; both fields absent leave the stored date
  untouched (this is the drag-and-drop regression test)
- `interview-invite` **with** a stage → the text lands in the stage entry's
  conclusion and no note row is created
- `interview-invite` **without** a stage → the text lands in a
  `candidate_notes` row of kind `InterviewRescheduled` and no stage entry is
  added
- `reassign` with a note → one note row of kind `Reassign`; with an empty note
  → no row
- `return-from-reserve` with no body behaves exactly as before

**Integration (`api/tests/Bonvi.Api.IntegrationTests/RecruitingApiTests.cs`
and a new `CandidateFileApiTests.cs`)**

- **RBAC on every new endpoint — mandatory.** For each of
  `POST /{id}/files`, `GET /{id}/files/{fileId}`,
  `DELETE /{id}/files/{fileId}`: no token → **401**; a token with no
  permissions → **403**; a token with **only** `recruiting.view` → 403 on the
  two writes and **200 on the download**; the admin client → 200 on all three.
  Follow the existing `Tekshiruv_FAQAT_bot_hisobiga_ochiq` style: assert the
  door is *closed for the wrong caller* **and** open for the right one, so the
  test cannot pass against a route that does not exist.

  How to mint a view-only token: the seeded roles all carry both recruiting
  permissions (`Permissions.cs`), so there is no user to log in as. Copy the
  existing `RuxsatsizTokenAsync()` helper in `RecruitingApiTests.cs`, which
  calls `ITokenService.CreateTokens(user, [], emptySet)`, and pass
  `new HashSet<string>(StringComparer.Ordinal) { Permissions.RecruitingView }`
  instead of the empty set. Name it `KoruvchiTokenAsync()` and keep it next to
  its sibling.
- `GET /{id}/files/{fileId}` with a `fileId` belonging to **another**
  candidate → 404, not 200. This is the deviation in §4.3.2 and it is the one
  that would leak a passport scan.
- upload a real JPEG (use `TestImages.cs`) larger than 2000 px → the stored
  `sizeBytes` is **smaller** than the uploaded length, the returned
  `contentType` is `image/jpeg`, and the downloaded bytes decode as an image
  whose longest edge is exactly 2000
- upload a PNG → stored as `image/jpeg` (re-encoded)
- upload a PDF → `contentType` is `application/pdf` and the downloaded bytes
  are **byte-identical** to what was uploaded
- upload a `.jpg`-named text file → 400 `nomzod.rezyume_yaroqsiz` (content
  sniffing, not the extension)
- upload 21 MB → 400 `nomzod.rezyume_katta`
- upload an 11th file → 400 `nomzod.rezyume_kop`
- delete a file → 204, then download it → 404, and the count drops so an
  eleventh upload succeeds
- `GET /{id}` returns `files` in **upload order** and `notes` in creation
  order
- list: `?rejected=true` returns rejected **and** withdrawn and nothing else;
  the default list contains neither; `?status=Withdrawn` returns only
  withdrawn; `?rejected=true&status=Withdrawn` returns only withdrawn
- list: `?source=Excel` still returns the archive's rejected **and** withdrawn
  rows (the carve-out, end to end)
- list: `?contactDue=true` returns candidates whose date is today or earlier
  and excludes future dates and NULLs
- list: `?sort=nextContact` and `?sort=-nextContact` both put rows without a
  date **last**
- list `total` and the returned page agree under every new filter — the
  existing "count comes from the same query" guarantee
- **bot contract pinned**: `GET /rejection-notices` and
  `GET /interview-notices` return objects with exactly their documented field
  sets. Assert on the JSON property names so that adding a field to the DTO
  fails this test. This is the mechanical guard for §7.
- the removed routes are gone: `POST|GET|DELETE /{id}/resume` → 404/405

### 8.2 `test-migration` — mandatory, model changed

1. **`upgrade head` on an empty database** — already covered by
   `BonviApiFactory` (`Bootstrap:AutoMigrate=true` runs every migration
   against a fresh database for every integration test assembly). Do not
   remove that; it is the cheapest smoke test in the repo.
2. **Data carry-over, explicitly.** The auto-migrate path cannot test this
   because there is no legacy data. Write a dedicated test that:
   - creates a scratch database and migrates it **to the migration
     immediately before** `NomzodIzohVaFayllar`
     (`dotnet ef database update <previous>`, or `IMigrator.MigrateAsync(targetMigration)`)
   - inserts, with raw SQL, a candidate carrying all four resume columns —
     plus a second candidate with `resume_file_key` set but
     `resume_content_type` NULL, and a third with no resume at all
   - migrates to head
   - asserts: candidate 1 has exactly one `candidate_files` row with the same
     `file_key`, `file_name`, `content_type` and `size_bytes`, and
     `created_at = candidates.created_at`; candidate 2 has one row with
     `content_type = 'application/octet-stream'`; candidate 3 has none; and
     the four `candidates.resume_*` columns no longer exist
     (`information_schema.columns`)
3. **`Down` then `Up` again.** After `Down`: the four columns exist again,
   candidate 1's four values are restored from the earliest file, and
   `candidate_files` / `candidate_notes` are gone. Then `Up` again must
   succeed and re-create the carried-over row.
4. **Multi-file `Down`.** With three files attached, `Down` restores the
   **earliest** one (deterministic `DISTINCT ON (candidate_id) ... ORDER BY
   candidate_id, created_at, id`) — not an arbitrary row.
5. **No spurious drops.** Read the generated migration line by line and assert
   in review that the only `DropColumn` calls are the four resume columns and
   that no other table appears in `Up`.
6. `next_contact_date`'s partial index exists after `Up` and is gone after
   `Down` (`pg_indexes`).

### 8.3 `test-frontend`

- `npm run build` (or the project's command) passes with the removed DTO
  fields — TypeScript is the main guard here and it will catch every stale
  read of `resumeFileName`.
- `./bonvi check` (locale completeness + backend error-code coverage) is
  green. This is not optional: it is the only thing that stops a raw key
  appearing on screen.
- `./bonvi gen` regenerates `web/src/shared/api/generated/` after the API is
  up. If `/openapi/v1.json` returns 500, the cause is almost certainly a
  `default` on a non-nullable value type in a new request record (§4).

---

## 9. Build order and parallelism

### 9.1 Sequential — shared files, one author each, never in parallel

| File | Why |
|---|---|
| `api/src/Bonvi.Infrastructure/Persistence/Migrations/*` + `AppDbContextModelSnapshot.cs` | EF serialises the whole model into one snapshot; two migrations authored in parallel conflict irreconcilably |
| `api/src/Bonvi.Application/Recruiting/RecruitingRegistration.cs` | one DI list |
| `api/src/Bonvi.Application/Common/DependencyInjection.cs` | one DI list |
| `api/src/Bonvi.Api/Endpoints/CandidateEndpoints.cs` | one file, ten endpoints |
| `api/src/Bonvi.Application/Recruiting/Dtos/RecruitingDtos.cs` | all three backend units touch it |
| `api/src/Bonvi.Application/Recruiting/Abstractions/IRecruitingReadModel.cs` | filter + row, both shared |
| `web/src/features/recruiting/api/recruiting-api.ts` + `recruiting-queries.ts` | all three frontend units depend on them |
| `web/src/shared/i18n/locales/*.json` (4) + `web/scripts/check-locales.mjs` | one author, or two agents will write the same key twice with different text |
| `web/src/app/layout/nav-items.ts` | one line, shared file |

### 9.2 Units

| Unit | Touches | Depends on | Parallel? |
|---|---|---|---|
| **B1 — schema** | `Bonvi.Domain/Recruiting/{Candidate,CandidateFile,CandidateNote}.cs`; `Configurations/RecruitingConfigurations.cs`; `AppDbContext.cs`; `Repositories/RecruitingRepositories.cs`; the migration | — | **No.** First, alone. Everything else compiles against it. |
| **B2 — image pipeline** | `Abstractions/IImageProcessor.cs`; `Infrastructure/Storage/SkiaImageProcessor.cs`; `Application/Common/Documents/ScaledDocumentStore.cs` | — | **Yes**, with B1. Disjoint files, no shared type. |
| **B3 — use cases, DTOs, read model, endpoints** | `Application/Recruiting/**`; `ReadModels/RecruitingReadModel.cs`; `Api/Endpoints/CandidateEndpoints.cs`; `RecruitingRegistration.cs`; `Common/DependencyInjection.cs` | B1, B2 | **No.** One agent — the six files above are shared by all four features. |
| **F1 — client contract** | `api/recruiting-api.ts`; `api/recruiting-queries.ts`; `lib/resume-file.ts` | this SPEC only (not a running backend) | **Yes**, with B1/B2/B3. §4 is precise enough to type against. |
| **F2 — dialogs** | `candidate-stage-dialog.tsx`; `candidate-stage-picker.tsx`; `candidate-interview-dialog.tsx`; `candidate-action-dialog.tsx` | F1 | **Yes**, with F3 and F4 — disjoint files |
| **F3 — files card** | `candidate-files-card.tsx` (new); delete `candidate-resume-card.tsx`; one import in `candidate-card-page.tsx` | F1 | **Yes**, with F2 and F4 |
| **F4 — list, filter, section** | `candidates-page.tsx`; `candidates-toolbar.tsx`; `lib/candidates-search.ts`; `lib/recruiting-presentation.ts` (if a tone helper is added); `app/layout/nav-items.ts` | F1 | **Yes**, with F2 and F3 |
| **W — wiring** | `candidate-card-page.tsx` (the next-contact field + the import), locale files ×4, `check-locales.mjs` | F2, F3, F4 | **No.** Single author, after the three frontend units land. |
| **T — tests** | `api/tests/**` | B3 | **No.** After the backend is green. |

`candidate-card-page.tsx` is touched by both F3 (one import) and W (one
field). Give it to **W** and have F3 hand over the import line as a note, or
accept one trivial conflict. Do not give it to two agents at once.

### 9.3 Order

```
[ B1 | B2 | F1 ]  →  B3  →  T
       F1         →  [ F2 | F3 | F4 ]  →  W
```

B1, B2 and F1 have no dependencies on each other and start together. B3 waits
for B1 and B2. F2/F3/F4 wait only for F1, so the whole frontend can proceed
while the backend is still being written — §4 is the contract both sides build
against.

After B3 and W: `./bonvi gen` (API must be running), then `./bonvi check`,
then `./bonvi test`, then the frontend build.

---

## 10. Open risks and pre-existing issues

1. **The stage timeline may reorder.** §5.2 only reads the two streams; if a
   later change merges them into one date-sorted list, note that
   `CandidateStageEntry.Resolve` overwrites `RecordedAt` with `UtcNow`, so
   filling in an old Pending entry moves it. The existing timeline avoids this
   by using insertion order, and this release keeps that. **Recommendation:
   leave the timeline's ordering exactly as it is** and render notes only in
   the §5.2 strip in this release; merging the two streams into the timeline
   is a separate, testable change.
2. **Free-text length is capped only by the column.** `CandidateNote.Text`
   (2000), `Candidate.Note` (2000) and `CandidateStageEntry.Conclusion` (4000)
   have no domain-level length validation, so an oversized value becomes a
   database error and therefore a 500. This is pre-existing and the new field
   inherits it. **Recommendation: cap in the UI with `maxLength` (§5.1) now,
   and add domain validation for all three fields in a separate change** —
   doing it here would mean three new error codes and twelve translations for
   a problem nobody has hit.
3. **`Down` leaves orphan files on disk.** Documented and accepted (§2.5).
   **Recommendation: no cleanup job** — `Down` is a development escape hatch;
   a job that deletes files based on a missing row is exactly how production
   data gets lost.
4. **Total-size cap is not visible before upload.** The client cannot compute
   the stored size, so a tenth upload can be rejected with
   `nomzod.rezyume_joy` after the bytes have travelled. **Recommendation:
   accept it** — the case requires roughly 60 MB of PDFs on one candidate and
   the error message names the limit.
5. **No file-level virus or macro scanning.** Unchanged from today: content is
   sniffed to PDF/JPEG/PNG/WebP and images are re-encoded (which incidentally
   strips anything hiding in an image container), but PDFs are stored
   verbatim. **Recommendation: unchanged for this release**; note it for
   `qa-security`.
6. **Old bookmarks to `?status=Rejected`.** They keep working and show only
   rejected candidates. **Recommendation: no redirect** — a URL that means
   what it says is not a bug, and a redirect would break the ability to filter
   to exactly one status.
7. **`60 MB` and `2000 px` are judgement calls.** Both are single constants
   (`Candidate.MaxFilesTotalBytes`, the `maxEdge` argument in
   `ScaledDocumentStore`). **Recommendation: leave them as constants, not
   system settings** — a settings row invites tuning a storage rule from a
   screen, and the numbers have no per-installation meaning here (one client,
   one server).
8. **`hasResume` now means "has files".** The name is kept deliberately
   (§4.8). **Recommendation: leave the name; fix the two doc comments.**
   Renaming it costs a TS reader change and a test change and buys nothing
   HR can see.

---

## 11. Definition of Done

- [ ] `./bonvi test` green — including the new RBAC tests for all three file
      endpoints and the pinned bot-DTO shape test
- [ ] the data carry-over migration test passes: `up` → legacy row becomes a
      `candidate_files` row → `down` restores the four columns → `up` again
- [ ] the migration is applied to a **restored backup** of production (from
      `backups/`, into a scratch database on the dev machine) and the
      pre-existing resume file is still visible on the candidate card
      afterwards. ⚠️ Never run this against the production database itself —
      touching prod needs the user's explicit go-ahead first, and the point of
      this check is to find the surprise *before* that conversation.
- [ ] the generated migration contains **no** `DropColumn` other than the four
      resume columns
- [ ] `./bonvi check` green — 4 locales aligned, both new error codes
      translated, no template key unaccounted for
- [ ] `./bonvi gen` succeeds (i.e. `/openapi/v1.json` is not 500)
- [ ] frontend build passes with the three removed `CandidateDto` fields
- [ ] a stage button opens the dialog, and **Saqlash with an empty note
      saves** — verified by hand
- [ ] a next-contact date set from the stage dialog appears in the table
      column, and `?due=true` finds the candidate on that date and after it
- [ ] rejecting the candidate clears the next-contact date; moving them to
      reserve does not
- [ ] ten files upload; the eleventh is refused with a message naming the
      limit; a 4 MB phone photo is stored at well under 1 MB; a PDF downloads
      byte-identical
- [ ] clicking an image file opens it large; clicking a PDF downloads it
- [ ] a withdrawn candidate disappears from `/nomzodlar` and appears under
      "Rad etilganlar" with the yellow "O‘zi voz kechgan" badge, while
      `?status=Rejected` still returns only rejected candidates
- [ ] the Excel archive (`?source=Excel`) is **not** empty and still shows its
      rejected and withdrawn rows
- [ ] the `IRecruitingReadModel` remark about `Withdrawn` has been rewritten
      and no longer contradicts the code
- [ ] no stage conclusion, note or next-contact date is present in any bot
      response — checked against `RejectionNoticeDto`, `InterviewNoticeDto`
      and `ApplicationCheckDto`
- [ ] one line appended to `docs/ASSUMPTIONS.md` for each decision in §12 that
      the user did not answer

---

## 12. Open questions — each with a standing decision

If the user says nothing, these decisions stand. Log them in
`docs/ASSUMPTIONS.md` in the existing one-line format.

1. **Does the funnel drag-and-drop also need the note dialog?**
   *Decision: no* (§5.1.6). The client's complaint is about the card's quick
   row; a modal would destroy the drag's optimistic patch, and the entry it
   creates is `Pending` so the note can still be filled from the timeline.
2. **Per-candidate total size cap — 60 MB?**
   *Decision: 60 MB of stored bytes* (§2.6). Ten downscaled scans cost about
   4 MB, so the cap only ever bites on long PDFs. Without it the per-file
   limit alone permits 200 MB per candidate.
3. **Longest image edge — 2000 px?**
   *Decision: 2000 px* (§4.4.2), the top of the suggested range, because
   passport numbers and diploma fine print must stay readable and 1600 px
   starts to smear stamps.
4. **Are the four resume columns dropped now?**
   *Decision: dropped in the same migration, after the copy* (§2.5). One
   deployment target, API and web ship together, and a kept column is a trap.
5. **Does a reserved candidate keep their next-contact date?**
   *Decision: yes* (§2.1.2). Reserve is a postponement, not an exit — "we call
   them when a slot opens" is the documented meaning of the status.
6. **Should the notes appear in the "Bosqichlar" timeline as well as the
   strip?**
   *Decision: not in this release* (§10.1). The strip answers "what happened
   last" where the decision is made; merging two streams into the timeline
   risks reordering existing rows and deserves its own change.
7. **Does the "Rad etilganlar" nav label change now that it holds withdrawals
   too?**
   *Decision: no.* The client asked for exactly this grouping, and the
   per-row badge (red vs yellow) already distinguishes the two.
8. **A separate `Note` outcome instead of a `candidate_notes` table?**
   *Decision: no* (§2.3.1). `StageOutcome` is also read by the employee card's
   council archive, which is out of scope and where a candidate note has no
   meaning.
