# خطوات تالية — Follow-ups مؤجّلة عمدًا

> مهام صغيرة اتأجّلت أثناء تنفيذ ميزة أكبر، بقرار واعي مش نسيان. كل بند بيقول ليه اتأجّل وإيه المطلوب بالظبط.

---

## بعد ما نخلص Remarks7 — قائمة مجمّعة

| # | المهمة | الأولوية | التفاصيل |
|---|---|---|---|
| 1 | إصلاح `DesignTimeDbContextFactory` (Connection String مكتوب جوه الكود، بيتجاهل أي Override) | 🔴 عالية | [Known-Issues.md § لسه مفتوحة](Known-Issues.md) |
| 2 | إضافة `AllowManualOrderLines` لشاشة إعدادات دورة المشتريات | 🟢 صغيرة | البند التالي في الملف ده |
| 3 | Migration على أي نسخة SQL Server منفصلة (لو فيه بيئة Staging/Production تانية غير `DESKTOP-DBL395G\MSSQLSERVER01`) | 🟡 متوسط | نفس خطوات الـ Backup/Restore/Verify اللي اتعملت هنا (2026-09-25) — كررها بالظبط على أي بيئة تانية قبل ما تطلع لها |

---

## Small Follow-up: Add AllowManualOrderLines to Purchasing Settings UI

**من**: Remarks7 — ربط أمر الشراء بطلب الشراء (2026-09-25)
**الأولوية**: 🟢 صغيرة
**الحالة**: Backend شغال بالكامل، الـ UI بس ناقص.

### السياق

`PurchaseCycleSettings.AllowManualOrderLines` (نظير `AllowManualInvoiceLines` من Remarks6) اتضاف في الـ Domain + Migration + التحقق الكامل في `PurchaseOrderRequestLinking.PlanAsync` — لو الإعداد `false`، أي سطر في أمر شراء مرتبط بطلب شراء ومالوش `PurchaseRequestLineId` بيترفض بـ `PUR-ORDER-MANUAL-LINE-NOT-ALLOWED`.

**الجزء الناقص**: الإعداد ده مش معروض في شاشة إعدادات دورة المشتريات (screen #11 — `PurchasingSettingsController` / `PurchaseCycleSettingsDto` / `GetPurchaseCycleSettingsQuery` / `UpdatePurchaseCycleSettingsCommand` / `frontend/.../settings/types.ts`). حاليًا القيمة الافتراضية `true` لكل شركة، ومفيش طريقة للمستخدم يقفلها غير عن طريق الداتابيز مباشرة.

**ليه اتأجّل؟** إضافته كانت هتبقى "Backend change" برّه نطاق مرحلة الـ Frontend المتفق عليها في Remarks7 (اللي كانت محدودة بـ endpoint اتنين بس). بدل ما نوسّع نطاق المرحلة، اتفقنا نسجّلها كـ follow-up منفصل.

### المطلوب بالظبط

نفس النمط اللي `allowManualInvoiceLines` ماشي بيه حرفيًا:

1. **`PurchaseCycleSettingsDto.cs`**: إضافة `public required bool AllowManualOrderLines { get; init; }`.
2. **`GetPurchaseCycleSettingsQuery.cs`**: `AllowManualOrderLines = settings.AllowManualOrderLines` في الـ mapping.
3. **`UpdatePurchaseCycleSettingsCommand.cs`**: إضافة `public required bool AllowManualOrderLines { get; init; }` + `settings.AllowManualOrderLines = request.AllowManualOrderLines;`.
4. **`PurchasingSettingsController.cs`**: إضافة `AllowManualOrderLines` لـ Request record + الـ mapping في `Update`.
5. **`frontend/src/features/purchasing/settings/types.ts`**: إضافة `allowManualOrderLines: boolean`.
6. شاشة الإعدادات نفسها (React component) — إضافة Checkbox/Toggle جنب `allowManualInvoiceLines` الموجود بالفعل.

### مصدر الحقيقة الحالي (لحد ما يتعمل الـ Follow-up)

- **Backend**: شغال بالكامل، الافتراضي `true` (نفس افتراضي `AllowManualInvoiceLines`).
- **Frontend (`PurchaseOrderEditPage.tsx`)**: زرار "إضافة سطر يدوي" ظاهر دايمًا من غير شرط — لو الإعداد مقفول من الداتابيز مباشرة، المستخدم هيقدر يضيف السطر في الـ UI لكن الحفظ هيترفض بـ `PUR-ORDER-MANUAL-LINE-NOT-ALLOWED` (رسالة خطأ واضحة، مش فشل صامت).
