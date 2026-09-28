# Task-001 — إضافة `AllowManualOrderLines` لشاشة إعدادات دورة المشتريات

> **التاريخ**: 2026-09-25
> **المصدر**: Follow-up مؤجّل من Remarks7 (ربط أمر الشراء بطلب الشراء)
> **الأولوية**: 🟡 متوسطة
> **الحالة**: مفتوح — Backend شغال بالكامل، الـ UI بس ناقص

---

## 1. الوصف

`PurchaseCycleSettings.AllowManualOrderLines` (نظير `AllowManualInvoiceLines` من Remarks6) اتضاف بالكامل في الـ Backend كجزء من Remarks7:

- **Domain**: `PurchaseCycleSettings.AllowManualOrderLines` (`bool`, افتراضي `true`).
- **Migration**: العمود موجود فعليًا على `HabbakErp` (اتطبّق 2026-09-25).
- **Validation**: `PurchaseOrderRequestLinking.PlanAsync` بيرفض أي سطر في أمر شراء مرتبط بطلب شراء ومالوش `PurchaseRequestLineId` لو الإعداد `false`، بكود `PUR-ORDER-MANUAL-LINE-NOT-ALLOWED`.

**الناقص بس**: الإعداد ده **مش معروض في شاشة إعدادات دورة المشتريات** (screen #11). حاليًا:
- القيمة الافتراضية `true` لكل شركة (مفيش طريقة تتغيّر غير عن طريق الداتابيز مباشرة).
- زرار "إضافة سطر يدوي" في `PurchaseOrderEditPage.tsx` **ظاهر دايمًا من غير شرط** — لو حد قفل الإعداد من الداتابيز مباشرة، المستخدم هيقدر يضيف السطر في الواجهة لكن الحفظ هيترفض برسالة واضحة (مش فشل صامت).

---

## 2. ليه اتأجّلت؟

إضافتها كانت هتبقى "Backend change" برّه نطاق مرحلة الـ Frontend المتفق عليها وقت تنفيذ Remarks7 (اللي كانت محدودة بـ endpoint اتنين بس: `open-for-order` و`{id}/order-lines`). بدل ما نوسّع نطاق المرحلة، اتفق إننا نسجّلها كمهمة منفصلة.

---

## 3. المطلوب بالظبط

نفس النمط اللي `allowManualInvoiceLines` ماشي بيه حرفيًا — 6 خطوات:

### Backend
1. **`PurchaseCycleSettingsDto.cs`**: إضافة `public required bool AllowManualOrderLines { get; init; }`.
2. **`GetPurchaseCycleSettingsQuery.cs`**: `AllowManualOrderLines = settings.AllowManualOrderLines` في الـ mapping.
3. **`UpdatePurchaseCycleSettingsCommand.cs`**: إضافة `public required bool AllowManualOrderLines { get; init; }` + `settings.AllowManualOrderLines = request.AllowManualOrderLines;`.
4. **`PurchasingSettingsController.cs`**: إضافة `AllowManualOrderLines` لـ Request record + الـ mapping في `Update`.

### Frontend
5. **`frontend/src/features/purchasing/settings/types.ts`**: إضافة `allowManualOrderLines: boolean`.
6. **شاشة الإعدادات** (React component بتاعة screen #11): إضافة Checkbox/Toggle جنب `allowManualInvoiceLines` الموجود بالفعل — بنفس الـ Label pattern.
7. **`PurchaseOrderEditPage.tsx`**: تغيير زرار "إضافة سطر يدوي" ليكون شرطي بـ `usePurchaseCycleSettings().allowManualOrderLines` (نفس نمط `allowManualInvoiceLines` في `PurchaseInvoiceEditPage.tsx` بالظبط):
   ```tsx
   {(allowManualOrderLines || watchedRequestId === '') && (
     <Button type="button" variant="ghost" onClick={() => append(emptyLine())}>...</Button>
   )}
   ```

---

## 4. الاختبارات المطلوبة (بعد التنفيذ)

1. `GetPurchaseCycleSettingsQuery` بيرجّع `allowManualOrderLines` صح.
2. `UpdatePurchaseCycleSettingsCommand` بيحدّث القيمة صح.
3. شاشة الإعدادات: تفعيل/تعطيل الـ Checkbox وحفظه بيتعكس في الداتابيز.
4. `PurchaseOrderEditPage.tsx`: زرار "إضافة سطر يدوي" بيختفي لما `allowManualOrderLines = false` وفيه طلب شراء مختار.

---

## 5. مصدر الحقيقة الحالي (لحد ما تتعمل المهمة دي)

الـ Backend **شغال بالكامل ومحمي** — الفجوة UI بس، ومحميّة بالفعل بـ Validation واضح لو حد حاول يتحايل عليها.
