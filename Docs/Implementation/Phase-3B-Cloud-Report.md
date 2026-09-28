# Phase 3B — Cloud Report

> نطاق هذا الملف مطابق لبند "2B. Cloud/Local Workflow" — بيوثّق بالتحديد إيه اللي اتعمل في الـCloud
> Session (Linux) بس. التفاصيل التقنية الكاملة (الكيانات، الـCommands، الـControllers، القرارات)
> موجودة في `Phase-3B-Final.md` — الملف ده بيركّز على الـCheckpoints المطلوبة من الـWorkflow نفسه.

---

## 1. تنفيذ الكود ✅

Phase 3B (3B.2 → 3B.8) كامل — Domain، Application، API، Background Job، File Import، Frontend،
Tests. التفاصيل الكاملة في `Phase-3B-Final.md`.

## 2. Build للـ Backend ✅

`dotnet build` نظيف على كل الـ6 مشاريع (Domain, Application, Infrastructure, API,
IntegrationTests, ApiTests) — صفر Error. (.NET SDK 8.0/10.0 اتثبَّتوا في الـSession عن طريق
`apt-get` عشان أصلًا مكنش فيه `dotnet` في البيئة دي.)

## 3. Migration Generation ✅

Migration `AttendanceDevices` (`20260928051730_AttendanceDevices`) اتولّدت عن طريق
`dotnet ef migrations add` ضد الـModel بس.

## 4. تحديث مهم: بنية اختبارات جديدة (Fix #3) — بقى ممكن نشغّل Tests فعليًا في الـCloud

بعد كتابة هذا التقرير لأول مرة، اتحدد إن `LocalDB` (اللي كل الـTest Fixtures كانت بتستخدمها
Hardcoded) تقنية Windows-only بحتة — مش قيد بيئة عابر، مستحيلة تقنيًا على Linux
(`PlatformNotSupportedException`). الحل الدائم: `TestSqlServer.cs` (نسخة في كل مشروع اختبارات) —
`OperatingSystem.IsWindows()` بيختار LocalDB زي ما كان دايمًا، أو Container حقيقي (SQL Server 2022
عن طريق Testcontainers) على أي حاجة تانية. تفاصيل كاملة في `Docs/Setup/Testing.md` (جديد) و
Commit `4d2bff6`.

**نتيجة عملية**: بقى ممكن نشغّل `dotnet test` فعليًا في الـCloud Session دلوقتي (مش بس
`dotnet build`)، طالما Docker شغّال في الـSession. **ده لسه مش بديل عن Migration Apply على الـReal
DB الحقيقي (`HabbakErp` على جهاز المستخدم)** — الـCloud مفيهوش وصول شبكة لجهاز المستخدم أصلًا ولا
هيكون — بس بقى ممكن نتحقق من الـSchema/الـTests ضد SQL Server حقيقي (Container مؤقت) قبل ما
الكود يوصل للـLocal أصلًا، زيادة ثقة مش بديل.

## 5. الإصلاحات الثلاثة (Fixes، بعد Local Verification الأولى)

| # | المشكلة | الحل | Commit |
|---|---|---|---|
| 1 🔴 | `Vault:Token: ""` (فاضي مش null) في appsettings.json — `??=` بيتخطاها فمفيش Fallback لـDev Mode | `IsNullOrWhiteSpace` بدل `??=` + اختبار `EmptyToken_UsesDevFallback` | `d736ddc` |
| 2 🟡 | `xlsx` من CDN (`cdn.sheetjs.com`) — ممكن يترفض من بعض الـNetwork Policies، والبديل من npm registry (0.18.5) فيه CVE عالي الخطورة غير مُصلَّح | استبدال كامل بـ`exceljs` (npm رسمي) + قارئ/كاتب CSV يدوي بسيط + اختبارات Round-trip | `a1c4cc9` |
| 3 🟢 | LocalDB Windows-only → الاختبارات مستحيلة على Linux | `TestSqlServer.cs` (OS-aware: LocalDB/Testcontainers) في المشروعين + `Docs/Setup/Testing.md` | `4d2bff6` |

كل Fix اتعمله Commit منفصل، واتأكّد بيه Test خاص بيه (أو بالتشغيل الفعلي ضد Docker SQL Server في
حالة #3).

## 6. Full Regression في الـCloud (بعد Fix #3، Docker متاح)

| Suite | Passed | Failed | Skipped | Total |
|---|---|---|---|---|
| `IntegrationTests` | 233 | **0** | 5* | 238 |
| `ApiTests` | 258 | **19** | 0 | 277 |

*\*الـ5 Skipped محتاجين Vault حقيقي شغّال (مش موجود في التشغيلة دي) — لمّا اتشغّل Vault Dev Server
منفصل والاختبارات الخمسة دي اتعادت لوحدها، نجحوا 7/7 (`VaultIntegrationTests` +
`HmacPiiHasherTests`).

### الـ19 فشل في ApiTests — **Known Limitation، مش Code Bug، مش متعلق بـPhase 3B**

القرار (بعد تحليل مفصّل): الـ19 فشل دول **مشكلة إعداد بيئة الـCloud Sandbox نفسها**، مش في الكود:

- **Vault الأساسي شغّال 100%** — `VaultIntegrationTests` + `HmacPiiHasherTests` (اتصال مباشر بـVault): 7/7 ✅.
- **Phase 3B شغّال 100%** — `AttendanceDevicesTests` (8/8) + `AttendanceDevicesApiTests` (4/4) = 12/12 ✅، في كل التشغيلات (قبل وبعد الـFixes الثلاثة).
- الـ19 فشل مجمّعين حوالين تشفير محتاج **Data Protection Keys + JWT Signing** المرتبطين بـVault
  (`TwoFactorTests`, `SecurityTests` [login/session], `EmployeeApiTests.Reveal_pii`, `RealJwtTests`,
  `VaultHealthTests`) — دول محتاجين إعداد إضافي في Vault مش موثّق حاليًا في قسم "وضع التطوير
  المحلي" بـ`Vault-Setup.md` (اللي بيغطي مفتاح الـHMAC بس). جهاز المستخدم المحلي شغّال ومُعَد من
  زمان، فمش هيواجه المشكلة دي.

**القرار المتفق عليه**: اعتبار الـ19 دول Known Limitation خاص بالـCloud Sandbox، **الـLocal Session
هي البيئة المرجعية** للتأكد من الـRegression الكامل الحقيقي — مفيش داعي نحل السبب الجذري دلوقتي.

## 7. اقتراح مؤجَّل (مش عاجل)

توثيق قسم جديد في `Docs/Setup/Vault-Setup.md`: "Configuring Vault for Data Protection + JWT
Signing" — خطوات إضافية لإعداد بيئة Sandbox/CI جديدة تمامًا (غير جهاز التطوير المحلي المُعَد
بالفعل) عشان الـ19 اختبار دول يشتغلوا فيها كمان. **مؤجَّل** — مش لازم قبل اعتماد Phase 3B.

## 8. Commit + Push ✅

كل الكوميتات اتعملت ودُفعت لـ`origin/main-wsqv76` (اتأكّد بمقارنة Hash مباشرة مع GitHub، مش بس
Cache محلي):

```
4d2bff6 Fix: OS-aware test SQL Server — LocalDB on Windows, Docker elsewhere
a1c4cc9 Fix: replace SheetJS (xlsx CDN) with ExcelJS for spreadsheet export/import
d736ddc Fix: Vault dev-mode fallback ignored an empty (not null) configured token
c3d6909 Docs: Phase 3B Cloud Report (2B Cloud/Local workflow checkpoint)
5c614e7 Fix AttendanceDevicesApiTests: HR_ATTENDANCE_DEVICES needs an explicit code
7a7e454 Fix: ProcessCompanyAsync must be public for IntegrationTests to call it
e609037 Docs: Phase 3B final report
087af78 Phase 3B.8: tests + fix a real screen-registration gap in the push controllers
2bc762b Refactor: make AttendanceDeviceJobs testable without a DI container
68e95e9 Refactor: extract RawPunchIngestor so file import doesn't need ISender
514e885 Phase 3B.6-3B.7: attendance device admin screens + employee fingerprint tab
3c13da5 Phase 3B.5: manual file import fallback (CsvHelper + ClosedXML)
a85c81a Phase 3B.4: background job translating RawPunch into TimeEntry
303f4a4 Phase 3B.2-3B.3: fingerprint device domain, admin CRUD, and push ingestion
```

working tree نظيف.

---

## 9. Local Verification Results ✅ (2026-09-28)

الـLocal Session كمّلت التحقق الكامل بعد `git pull` (Commit `340b89e`) — **Approved رسميًا**.

| Suite | Cloud (هذا التقرير) | Local |
|---|---|---|
| `IntegrationTests` | 233/238 (5 skipped — Vault) | **238/238** ✅ |
| `ApiTests` | 258/277 (19 failed) | **277/277** ✅ |
| Frontend (`npm run build` + Vitest) | لم تُشغّل هنا | **9/9** ✅ |
| Phase 3B Tests (`AttendanceDevicesTests` + `AttendanceDevicesApiTests`) | 12/12 | 12/12 ✅ |

**الـ19 فشل في تشغيلة الـCloud نجحوا كلهم على الـLocal** — يؤكد التشخيص في §6: كانت فعلًا مشكلة
إعداد Vault في الـCloud Sandbox بس (Data Protection + JWT Signing غير معدَّين هناك)، مش Code Bug —
جهاز الـLocal مُعَد بالفعل وشغّال صح من الأساس. Migration Apply كامل (Backup → Trial DB
[Up+Down+Re-apply] → Real DB `HabbakErp`) اتعمل بنجاح على الـLocal.

**الخلاصة: Phase 3B معتمدة رسميًا (Approved) — منجزة بالكامل (Cloud + Local).**

---

## 10. القرار — STOP، الانتقال لـPhase 3C

Phase 3B خلصت واعتُمدت. **Phase 3C** (بنود 1-4 من `Docs/My Remarks/Remarks8-HR-Enhancements.md` —
Contract Lines، Contract Attachments، Certification Attachments، Hiring Wizard Contract Lines)
هي المرحلة التالية. الـCloud هيقف هنا وينتظر "كمّل Phase 3C" صريحة قبل بدء أي كود جديد — أول خطوة
هتكون STEP 1 (Research Pass) لما تُطلب.
