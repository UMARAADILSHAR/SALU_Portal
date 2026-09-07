# SALU Exam Portal — Full-Proof Suggestions

**Document type:** Implementation blueprint (not a changelog)  
**Scope:** Payments, immutable database/ledger, UI/UX, security, exams, ops  
**Codebase reviewed:** ASP.NET Core 10 + Blazor Interactive Server, EF Core SQL Server, Identity, MudBlazor  
**Date:** 2026-09-05  

This document is written so a team can harden the portal for **university exam fees, enrollment records, and admit-card issuance** without rewriting the product. Items are ordered by **risk first**, then **user trust**.

---

## 1. What the system already does well

Keep these; they are the right foundations.

| Area | Existing strength |
|------|-------------------|
| Enrollment | Draft vs submit, window checks, CNIC/mobile validation, atomic submit + challan in a transaction |
| Fees (intent) | Unique `ChallanNumber`, `FeeStatus` enum (`Unpaid` → `PendingVerification` → `Paid` / `Verified` / `Expired`), notes with fee breakdown |
| Integrity (intent) | Maker-checker service, TOTP for checkers, `RowVersion` concurrency tokens, admit-card HMAC QR, reconciliation worker |
| Auth | Lockout, password policy, auth rate limits, file magic-byte validation |
| UX (partial) | Login/register polish, student dashboard glass cards, skeleton loaders, dark mode, toasts |

The gaps below are where the product **claims** a ledger and **behaves** like a mutable CRUD app.

---

## 2. Critical findings (must fix before real money)

These are production blockers for a university treasury.

### 2.1 Payments are not a payment system

Today:

- Students only **print a 3-copy HBL/NBP challan**.
- Admins click **Mark Paid** in `FeesHub` with **no bank reference**, **no maker-checker**, **no TOTP**, and `LastModifiedBy` hardcoded to `"SuperAdmin"`.
- Challan UI **hardcodes** `3,840.00` and the words *“Three Thousand Eight Hundred Forty Only”* even when `Fee.Amount` includes migration/late fee.
- If no `Fee` row exists, the page **invents** a challan number and amount in the browser.
- `challan_validity_days` exists in settings; submit still uses **hardcoded 7 days**.
- `FeeStatus.PendingVerification`, `Verified`, and `Expired` are **almost unused** in the admin UI.
- Admit cards are generated for `FeeStatus.Paid` **without** requiring enrollment `Approved` or `Verified` fee.
- Enrollment **cascade-deletes fees**, destroying payment history if a user/enrollment is removed.

That is not bank-grade. A single admin click can mint a paid student and trigger admit-card issuance via `ExamIntegrityReconciliationWorker`.

### 2.2 The database is not immutable

Today:

- `AuditLog` inherits `AuditableEntity` (so it has `LastModifiedAt` / `LastModifiedBy`) and **EF allows UPDATE/DELETE**.
- `DATABASE_SECURITY_SETUP.sql` talks about `tr_AuditLogPreventModification`, but that trigger is **not in EF migrations**. Weekly scripts **DELETE audit logs older than 1 year**.
- `Fee` rows are **updated in place** (amount, challan number, status) on resubmit and on Mark Paid.
- Dual-control **approves a JSON payload but does not apply it** to the target entity.
- Uploads are served as **public static files** (`/uploads/...`), bypassing `DownloadsController`.
- SQL setup recommends `db_owner` for the app login and contains a **plaintext password**.
- `appsettings.json` contains **SeedAdmin password**; initializer **resets admin password on every startup**.
- Admit-card HMAC falls back to a **hardcoded secret** in source.

Until append-only ledgers and DB triggers exist, “immutable” is marketing, not a guarantee.

### 2.3 UI/UX trust gaps

- Student dashboard falls back to **fake CNIC / phone** (`45206-4702131-9`, `0300-0000000`) when data is missing.
- Challan **dated** uses `DateTime.Now` (print time), not challan issue date.
- Fees hub has **no search, filter, pagination, or dual confirmation**.
- Heavy emoji/jargon (“Command Center”, “Anti-Skip”) vs students who need **Urdu + English**, print reliability, and clear next steps.
- No student path to **upload paid challan / enter bank scroll**.
- `JS.InvokeVoidAsync("eval", ...)` for theme is brittle and a CSP blocker.

---

## 3. Target architecture (full-proof payments)

Treat money as an **event stream**. The `Fees` table becomes a **current snapshot**; truth lives in an **append-only payment ledger**.

```
Student submits enrollment
        │
        ▼
FeeChallan issued (immutable header: number, amount, due, hash)
        │
        ├── Offline: HBL/NBP 3-copy + optional 1Link / IBAN QR
        └── Online (phase 2): JazzCash / Easypaisa / HBL Konnect / 1Bill
        │
        ▼
PaymentAttempt / BankScroll / Webhook (append-only)
        │
        ▼
Maker posts "propose paid"  →  Checker + TOTP  →  FeeStatus.Verified
        │
        ▼
Only Verified + Approved enrollment  →  Admit card engine
```

### 3.1 New tables (recommended)

**`FeeChallans` (issue once, never mutate amount/number)**

| Column | Rule |
|--------|------|
| `Id` | PK |
| `EnrollmentId` | FK, Restrict (never cascade-delete money) |
| `ChallanNumber` | Unique, allocated from a sequence (not random 4-digit) |
| `BaseFee`, `LateFee`, `MigrationFee`, `TotalAmount` | `decimal(18,2)`, CHECK `Total = Base+Late+Migration` |
| `Currency` | `PKR` |
| `IssuedAtUtc`, `DueAtUtc` | From `challan_validity_days` |
| `AmountInWordsEn`, `AmountInWordsUr` | Generated at issue time, stored |
| `BankName`, `AccountTitle`, `AccountNumber`, `Iban` | From `SystemSettings`, snapshotted |
| `Status` | `Issued` / `Superseded` / `Expired` (supersede = new challan row, never rewrite) |
| `ContentSha256` | Hash of canonical challan payload |
| `PreviousChallanId` | If regenerated after expiry |

**`PaymentLedger` (append-only; this is the legal money trail)**

| Column | Rule |
|--------|------|
| `Id` | Sequential bigint (ordered) |
| `FeeChallanId` | FK Restrict |
| `EventType` | `Issued`, `BankPosted`, `StudentReceiptUploaded`, `Reconciled`, `Reversed`, `Expired` |
| `Amount` | Signed decimal; reverse = negative event, never UPDATE old row |
| `BankTxnId` / `ScrollNo` / `BranchCode` / `PaidAtBank` | Nullable per event |
| `Channel` | `HBL`, `NBP`, `JazzCash`, `Easypaisa`, `1Bill`, `Manual` |
| `IdempotencyKey` | Unique (gateway txn id or `scroll+challan`) |
| `ActorUserId`, `Ip`, `UserAgent` | Required |
| `PayloadJson` | Gateway raw body or bank file row |
| `RowSha256`, `PrevRowSha256` | Hash chain |
| **No UPDATE/DELETE** | INSTEAD OF triggers + `db_denydatawriter` except INSERT |

**`BankReconciliationBatches`**

- Upload HBL/NBP scroll (CSV/Excel/MT940-style).
- Auto-match: exact `ChallanNumber` + exact `Amount`.
- Exceptions queue: amount mismatch, unknown challan, duplicate txn.

**`PaymentGateways` (config, not secrets in git)**

- Merchant IDs, return URLs, HMAC secrets in **Azure Key Vault / User Secrets / env**.
- Never log full PAN or CNIC in gateway payloads.

### 3.2 Status machine (enforce in DB + app)

```
Unpaid (Issued)
   → PendingVerification   (student uploaded receipt OR bank file unmatched)
   → Paid                  (bank match OR gateway success — still not admit-card eligible)
   → Verified              (checker + TOTP; only this unlocks admit card)
   → Expired               (due passed, unpaid) → issue NEW challan
   → Reversed              (append reverse event; Verified cannot go back to Paid without dual control)
```

Rules:

1. **Never** set `Paid`/`Verified` from a student account.
2. **Never** set `Verified` without maker-checker (two distinct university officers).
3. **Never** generate admit cards on `Paid` alone; require `Verified` **and** `EnrollmentStatus.Approved`.
4. One **active** challan per enrollment; old ones `Superseded` with a ledger event.

### 3.3 Offline bank challan (phase 1 — do this first)

Pakistan universities still collect via HBL/NBP. Make that **correct** before adding wallets.

1. **Remove all hardcoded 3840** from `Challan.razor`. Render only stored line items + stored amount-in-words.
2. **Issue date** = `IssuedAtUtc` (Pakistan Standard Time display), not print time.
3. **QR on each copy:** `CHALLAN|number|amount|due|sha256` so cashiers and campus counters can scan.
4. **IBAN + account title** from settings; print MICR-style account line.
5. **Student actions:** Download PDF (QuestPDF/DinkToPdf), not only `window.print`.
6. **Upload stamped copy** (JPEG/PDF, magic-byte validated) → `PendingVerification`.
7. **College clerk** can confirm “university copy received” as a *document* event, not as paid.

### 3.4 Online payments (phase 2)

Integrate **after** the ledger exists so gateways only append events.

| Channel | Why | Notes |
|---------|-----|--------|
| **1Bill / 1Link** | Matches existing challan number culture | Student pays anywhere using challan as consumer number |
| **HBL Konnect / HBL Pay** | Same bank as printed A/C | Easier reconciliation |
| **JazzCash / Easypaisa** | Student convenience | Use official merchant APIs; verify HMAC on return **and** IPN |
| Card (optional) | Diaspora / late | PCI: hosted checkout only, never card fields on SALU servers |

Implementation rules:

- Create `PaymentAttempt` **before** redirect (`Pending`).
- On IPN: verify signature, amount, currency, merchant id; **idempotent** insert into `PaymentLedger`.
- UI return URL is **not** trusted; only IPN/webhook marks `Paid`.
- Timeouts: abandon attempts after 30 minutes; student can retry with new attempt, same challan.
- Daily auto-recon: gateway settlement file vs ledger.

### 3.5 Admin Fees Hub (replace Mark Paid)

Current one-click paid is the highest fraud risk in the repo.

Replace with:

1. Search: challan, CNIC, ref no, college, status, date range.
2. Pagination (never load all fees).
3. Row actions:
   - **Propose paid** (maker): requires bank txn id + amount + paid date.
   - **Approve / reject** (checker): TOTP, cannot be same user.
4. Exception workbench for unmatched scrolls.
5. Export: paid register PDF/Excel for Controller of Examinations / Treasurer.
6. Show hash-chain verification badge (“ledger intact”).

Wire this through `IDualControlApprovalService` **and actually apply** the payload on approve (today approval is a status flag only).

### 3.6 Fee calculation correctness

- Affiliated-district list must live in **DB** (`AffiliatedDistricts`), not a `HashSet` in `FeeCalculationService`.
- Snapshot fee components **on the challan at issue time**. Later tariff changes must not rewrite old challans.
- Late fee: freeze using **server UTC** vs window, show countdown on student dashboard.
- Amount in words: library + unit tests (Urdu optional via a small mapping table).

---

## 4. Database immutability (full-proof ledger)

Application checks are necessary but **not sufficient**. A DBA or compromised admin password can still `UPDATE Fees`.

### 4.1 Principles

1. **Insert-only** for `AuditLogs`, `PaymentLedger`, `MakerCheckerRequests` (after terminal state, freeze).
2. **Soft-delete** everywhere else (`IsDeleted`, `DeletedAtUtc`, `DeletedBy`); never hard-delete enrollments, fees, users.
3. **Restrict** FKs on money and identity; **no cascade** from `AspNetUsers` → `Enrollments` → `Fees`.
4. **Temporal tables** (SQL Server system-versioned) on `Enrollments`, `AdmitCards`, `SystemSettings`.
5. **Hash chain** on audit + payment ledger; nightly job verifies `PrevRowSha256`.
6. **Least privilege:** app login `db_datareader` + EXECUTE on stored procedures / INSERT on ledger tables only.

### 4.2 EF + SQL changes

**AuditLog**

- Stop inheriting mutable audit fields for the log itself (or ignore them).
- Map as:

```sql
CREATE TRIGGER tr_AuditLogs_PreventMutation
ON AuditLogs
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    THROW 50001, 'AuditLogs are append-only.', 1;
END;
```

Put this in an **EF migration**, not a loose SQL file that ops may skip. **Do not DELETE** old audits in weekly jobs; archive to a read-only filegroup or Azure Blob / WORM storage.

**Fees / challans**

```sql
ALTER TABLE Fees ADD CONSTRAINT CK_Fees_Amount_Positive CHECK (Amount > 0);
-- App role cannot UPDATE Amount, ChallanNumber, EnrollmentId
```

Prefer: **no UPDATE of amount/challan**; status changes only via stored procedure `usp_AppendPaymentEvent` that inserts ledger + updates snapshot in one transaction.

**Concurrency**

- `RowVersion` is already on entities — surface `DbUpdateConcurrencyException` in UI (“record changed by another officer”).
- Use `Serializable` only where sequences are allocated (roll numbers, challan sequence); keep payment IPN at `ReadCommitted` + unique idempotency.

**Sequences**

Replace `RandomNumberGenerator` challan suffixes:

```sql
CREATE SEQUENCE dbo.ChallanSeq AS BIGINT START WITH 100000 INCREMENT BY 1;
```

Format: `SALU-{yyyy}-{seq:D8}` — unique, gap-auditable.

**Maker-checker completion**

On `Approved`:

1. Deserialize `ProposedPayloadJson` with `ISecureJsonService`.
2. Apply in the same transaction as the approval audit event.
3. Store `AppliedAtUtc` and `AppliedHash`.
4. Lock the request row (`ReviewStatus` terminal → trigger blocks further UPDATE except no-op).

**PII**

- Encrypt CNIC, phone, TOTP secrets at rest (you already encrypt TOTP).
- Column-level encryption or Always Encrypted for CNIC if required by university policy.
- Masking in UI: `45206-*******-1` for clerks who do not need full CNIC.

### 4.3 What to delete from `DATABASE_SECURITY_SETUP.sql`

That file currently **weakens** immutability:

- `ALTER ROLE [db_owner] ADD MEMBER [SaluPortalAppUser]`
- Hardcoded login password
- `DELETE FROM AuditLogs WHERE CreatedAt < ...`
- `DELETE FROM MakerCheckerRequests` for pending items (expire by **status**, keep the row)
- `DELETE` unverified users without archive
- Setting DB owner to `sa`

Replace with: contained app user, `GRANT INSERT ON AuditLogs`, `GRANT INSERT, SELECT ON PaymentLedger`, `DENY UPDATE, DELETE` on those tables, backups to a **separate** credential.

### 4.4 Backup / WORM

- Full backup daily + log backup every 15 minutes (exam season: 5 minutes).
- Copy backups off-box; enable **immutability** on the backup store (Azure Blob WORM / tape).
- Test restore quarterly (the current restore script is a template, not a drill).
- `RESTORE VERIFYONLY` in CI against a staging copy, not production paths.

---

## 5. UI and UX (students, colleges, university)

Design for **Khairpur affiliated colleges**: slow PCs, shared labs, mobile Jazz 4G, print shops, Urdu speakers, accessibility.

### 5.1 Design system (one source of truth)

Today: Bootstrap + MudBlazor + large per-page CSS + emoji. Inconsistent admin vs student.

- Extract CSS variables already used (`--salu-border`, navy/gold) into `wwwroot/css/salu-tokens.css`.
- Components: `StatusBadge`, `Money`, `EmptyState`, `ConfirmDialog`, `WizardStepper`, `PrintSheet`.
- Prefer **icons (Bootstrap Icons)** over emoji in official documents (emoji print poorly and look unprofessional on challans).
- Dark mode: toggle `data-theme` via `IJSObjectReference` module, **not** `eval`.
- Respect `prefers-reduced-motion` (login animations currently ignore this).

### 5.2 Language and copy

- Bilingual UI: English default + **Sindhi/Urdu** toggle for student flows (enrollment, challan instructions).
- Replace “Student Command Center” with **“My Examination Portal”**.
- Never show placeholder CNIC/phone; show **“Not provided — update profile”**.
- Errors: one sentence + what to do next (e.g. “Challan expired. Generate a new challan; old bank copies will not be accepted.”).

### 5.3 Student journey (fix the happy path)

**Dashboard**

- Vertical timeline: Register → Enroll → Pay → College verify → University approve → Admit card.
- Payment card: amount, due date countdown, **Paid/Unpaid**, download + upload receipt.
- Hide admit card CTA until `Verified` + `Approved`.
- Real photo or initials; never logo as “avatar” implying identity.

**Enrollment wizard**

- Persist draft per step (already partially there); warn on SignalR disconnect (`ReconnectModal` exists — bind it to draft save).
- Progress “Step 3 of 7” + estimated time.
- Fee advisory must match **issued challan**, not a live recalculation that can diverge after submit.
- File upload: progress bar, type/size before upload, preview, OCR status in plain language (“Reading marksheet…”).

**Challan / print**

- Dedicated print CSS (A4, 3 slips, crop marks).
- PDF download for students who print at a shop.
- Paid watermark on reprints; unpaid watermark “NOT A RECEIPT”.
- Do not toast “Fee unpaid” on every page load (noisy); show a persistent banner instead.

**Exam / admit card**

- Large, high-contrast roll number and photo.
- QR verify page for invigilators (public, rate-limited, no PII beyond name + roll + valid/invalid).
- Offline-friendly: “Save PDF” before exam day.

### 5.4 Admin / college UX

- **CollegeAdmin** should see only scoped students/fees (`ICollegeScopingService` exists — use it on Fees Hub; university-only Mark Paid is currently all records).
- Enrollment Hub: bulk actions with **preview count** + maker-checker for mass approve.
- Settings Hub: dangerous actions (close portal, change fee) behind dual control + “this will not change already-issued challans”.
- Tables: server-side filter, export, column density; MudTable without paging will fail at 50k students.

### 5.5 Accessibility and mobile

- WCAG 2.2 AA: contrast on gold-on-navy, visible focus, labels not placeholder-only.
- Touch targets ≥ 44px on student buttons.
- Enrollment form on phone: one field group per screen optional; at least stacked 7 steps (grid already collapses — verify real devices).
- `aria-live` for fee total and validation errors.
- Keyboard: wizard Next/Back, skip to content.

### 5.6 Trust and empty/error states

- `/health` is mapped — add a public **status** page for “portal open / closed / exam day”.
- Blazor error UI: Urdu+English, “your draft was saved”, reload.
- 429 rate limit: friendly wait message on login, not a blank fail.
- Not-found and access-denied already exist — style them to the design system.

---

## 6. Security (beyond payments)

| Issue | Suggestion |
|-------|------------|
| `AllowedHosts: *` | Set to `exam.salu.edu.pk` in production |
| Seed admin password in `appsettings.json` | User Secrets / Key Vault; **stop resetting password on every boot** |
| HMAC default in source | Fail startup if `Security:AdmitCardHmacSecret` missing in Production |
| Public `/uploads` | Remove static mapping; serve only via authorized controller; store outside web root |
| `DownloadsController` `Contains(fileId)` | Exact match on `EnrollmentDocuments.FileId`; admins via role policy |
| No CSP / no HSTS in dev path | CSP (no `unsafe-eval`), `Permissions-Policy`, HSTS always in prod |
| `IdentityNoOpEmailSender` | SMTP (university) or Graph; password reset is otherwise dead |
| TOTP failed-attempt TODO | Lock checker after 3 failures (code comment already notes this) |
| Email confirmation off | Turn on for students before fee pay |
| Blazor Server | Long-running circuits: idle timeout, re-auth for Mark Paid / approve |
| OCR worker | Isolate Tesseract; virus scan uploads (ClamAV) before OCR |
| Tests | Expand beyond file/CNIC format: payment state machine, hash chain, dual-control self-approve |

`Permissions.cs` should be used in policies (fine-grained: `Fees.Reconcile`, `Fees.Verify`, `Enrollment.Approve`) instead of only four roles.

---

## 7. Exam integrity (tied to money)

`ExamIntegrityReconciliationWorker` currently treats **count(Paid) vs count(AdmitCards)** as fraud. That is too coarse (phantoms, withdrawals, cancelled papers).

Suggestions:

- Reconcile **per enrollment id**, not totals.
- Alert only; **do not auto-issue** cards unless `AntiSkipGatekeeperEnforced` **and** status is `Verified`+`Approved`.
- Seat allocation: store center/room as master data, not free text only.
- Results entity exists but is unused in UX — plan a **locked results ledger** (same append-only pattern) before gazette publication.
- Roll number service commits a transaction **without persisting the roll** — sequence should be written in the same transaction as the enrollment/admit card to avoid gaps and races.

---

## 8. Operations, performance, compliance

- **Environments:** Dev / Staging / Prod; never LocalDB for prod.
- **Observability:** structured logs, correlation id, Application Insights; alert on `Mark Paid`, dual-control violations, hash-chain break, webhook signature fail.
- **Jobs:** expire challans daily; retry failed IPNs; OCR queue already hosted.
- **Scale:** Blazor Server + sticky sessions or Azure SignalR; Fees/Enrollment queries indexed (`Fee.Status`, `Enrollment.UserId`, `ChallanNumber`).
- **PECA / university audit:** retain exam and fee records **for the statutory period** (typically 5–10 years); do not 1-year-delete audits.
- **DPIA:** photos, CNIC, domicile — retention and access matrix (Student / College / Controller / Treasurer).
- **Load test:** enrollment window open day (10k concurrent drafts).

---

## 9. Implementation roadmap

### Phase A — Stop the bleeding (1–2 weeks)

1. Remove public `/uploads`; fix challan to use **only DB amounts and words**.
2. Disable one-click Mark Paid in production **or** require txn id + confirm modal + current user id (interim).
3. Remove seed password from repo; stop admin password reset on startup.
4. EF migration: AuditLog INSTEAD OF UPDATE/DELETE; Restrict delete on Fees.
5. Admit cards only if `FeeStatus.Verified` **after** you add a proper verify path (or temporarily require TOTP on Mark Paid).
6. Fix student dashboard fake CNIC/phone.
7. HMAC secret required in Production.

### Phase B — Real ledger (3–5 weeks)

1. `FeeChallans` + `PaymentLedger` hash chain + sequences.
2. Bank scroll import + match queue.
3. Maker-checker **applies payload**; Fees Hub uses it.
4. Student receipt upload → `PendingVerification`.
5. PDF challan + QR; settings-driven bank account.
6. Temporal tables on Enrollment.
7. Dual-control for tariff changes.

### Phase C — Online pay + UX (4–6 weeks)

1. 1Bill or HBL hosted pay + JazzCash/Easypaisa.
2. Design system + Urdu/Sindhi student strings.
3. College-scoped finance views.
4. Invigilator QR verify.
5. Accessibility pass and mobile field test at two affiliated colleges.

### Phase D — Hardening

1. WORM backups, restore drills.
2. Always Encrypted CNIC.
3. Fine-grained permissions.
4. Results gazette ledger.
5. Independent security review (OWASP ASVS L2).

---

## 10. Acceptance criteria (definition of “full-proof”)

The portal is full-proof for **payments + immutability + UX** when all of the following are true:

1. **No UI or API can change `PaymentLedger` history**; SQL UPDATE/DELETE throws.
2. **No admit card** exists unless a hash-chained `Verified` event exists for the exact challan amount.
3. **Two distinct officers** (maker/checker + TOTP) are required to verify cash/bank exceptions.
4. Printed challan **always** matches stored amount, words, account, and QR hash.
5. Gateway/bank duplicates **cannot double-count** (unique idempotency).
6. Deleting a student **cannot** delete money or audit rows.
7. Students can complete enroll → pay → receipt on a **mid-range Android** in Urdu/English without calling the exam branch for “what is 3840 vs my total”.
8. Officers can find a challan in **&lt; 10 seconds** and export a day’s collection.
9. Hash-chain job is green; backup restore tested; secrets not in git.
10. Automated tests cover: fee snapshot, expiry/regenerate, dual-control self-approve rejection, webhook replay, cascade-delete forbidden.

---

## 11. File-level map (where to change)

| Area | Primary files |
|------|----------------|
| Challan UI bugs | `Components/Features/Student/Challan.razor` |
| One-click paid | `Components/Features/Administration/FeesHub.razor` |
| Fee issue logic | `Application/Services/EnrollmentServices.cs` |
| Dual control no-op apply | `Application/Services/DualControlApprovalService.cs` |
| Cascade / indexes | `Infrastructure/Persistence/ApplicationDbContext.cs` |
| Public uploads | `Program.cs` |
| HMAC / paid→admit | `Application/Services/ExamAdmitCardEngineService.cs` |
| Coarse recon | `Application/Services/ExamIntegrityReconciliationWorker.cs` |
| Seed secrets | `appsettings.json`, `DbInitializer.cs` |
| Misleading SQL | `DATABASE_SECURITY_SETUP.sql` |
| Fake PII in UI | `Components/Features/Student/Dashboard.razor` |
| Theme `eval` | `Components/Shared/Layout/MainLayout.razor` |

---

## 12. Out of scope (do not confuse with full-proof)

- Replacing Blazor Server with WASM (optional later for scale).
- Blockchain. Hash-chained SQL + WORM backups are the appropriate university control.
- Collecting card numbers on SALU servers.
- Auto-deleting audit logs to “save space”.

---

**Bottom line:** The portal already looks like an examination ledger. To be full-proof, **stop mutating fee rows**, **append payment events with a hash chain and DB deny-update**, **never issue admit cards from a single Mark Paid click**, and **make challan/dashboard show only stored, printable truth** in a bilingual, accessible UI.
