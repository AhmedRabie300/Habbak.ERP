# Remarks7 — التقرير النهائي: ربط أمر الشراء بطلب الشراء

> **التاريخ**: 2026-09-25
> **المرجع**: `Docs/My Remarks/Remarks7.md` (نظير `Remarks6.md` — بس على مستوى أمر شراء ← طلب شراء بدل فاتورة ← أمر شراء).
> **الحالة**: ✅ **مقفول** — كل المراحل الخمسة خلصت، الـ Migration اتطبّقت على الداتابيز الحقيقية، والتدفق الكامل اتأكد يدويًا.

---

## 1. الملخص التنفيذي

قبل Remarks7، أمر الشراء كان بيتربط بطلب الشراء على مستوى **الهيدر بس** (`PurchaseOrder.PurchaseRequestId`) — من غير تحميل تلقائي للسطور، ومن غير أي سقف على الكمية، ومن غير تتبع دقيق لقد إيه اتحوّل من كل صنف. أول أمر شراء بيتعمل من أي طلب كان بيقفله بالكامل (`Converted`) حتى لو غطّى جزء بسيط منه بس — يعني **مستحيل تحويل طلب شراء لأكتر من أمر شراء جزئي** كان مبني في النظام أصلًا (Bug موجود من قبل، اتصلح كجزء من نفس الشغل).

Remarks7 بنى نفس معمارية Remarks6 (فاتورة الشراء ↔ أمر الشراء) بالظبط، على مستوى (أمر الشراء ↔ طلب الشراء):
- عمود محفوظ `PurchaseRequestLine.OrderedQuantity` بيتحدّث Transaction بـ Transaction.
- ربط صريح لكل سطر (`PurchaseOrderLine.PurchaseRequestLineId`).
- حالة جديدة `PurchaseRequestStatus.PartiallyConverted` بجانب `Converted` الموجودة.
- سقف كمية بيرفض أي تجاوز (`PUR-ORDER-QTY-EXCEEDS-REQUEST`).
- تحميل تلقائي لسطور الطلب في شاشة أمر الشراء، بنفس نمط `PurchaseInvoiceEditPage.tsx`.

---

## 2. المراحل الخمسة — ملخص كل مرحلة

### المرحلة 1 — Domain + Migration + Backfill
- `PurchaseRequestLine.OrderedQuantity` (جديد)، `PurchaseOrderLine.PurchaseRequestLineId` (جديد)، `PurchaseRequestStatus.PartiallyConverted = 8` (جديد، مفيش تعارض في القيم)، `PurchaseCycleSettings.AllowManualOrderLines` (جديد، افتراضي `true`).
- Migration `20260925141556_PurchaseOrderRequestLinking` — 3 خطوات backfill SQL (ربط بشرط "سطر واحد لكل صنف" → تجميع الكميات → تصحيح حالة الطلب) — نفس نمط Migration Remarks6 حرفيًا.

### المرحلة 2 — Commands + منطق مشترك
- `PurchaseOrderRequestLinking.cs` (جديد) — نظير `PurchaseOrderInvoicing.cs` حرفيًا (`PlanAsync`, `ApplyAsync`, `PlanOf`, `RefreshRequestStatusAsync`).
- `CreatePurchaseOrderCommand`, `UpdatePurchaseOrderCommand`, `CancelPurchaseOrderCommand`, `RejectPurchaseOrderCommand` — كلهم اتعدّلوا يربطوا/يفكّوا الكمية مع الطلب.
- 4 أكواد خطأ جديدة: `PUR-ORDER-QTY-EXCEEDS-REQUEST`, `PUR-ORDER-MANUAL-LINE-NOT-ALLOWED`, `PUR-ORDER-LINE-NOT-IN-REQUEST`, `PUR-ORDER-LINE-ITEM-MISMATCH`.

### المرحلة 3 — `GetPurchaseOrderSourceRequestQuery`
- اتعدّل (مش اتبنى من جديد) ليقرا من العمود المحفوظ الجديد بدل الحساب الديناميكي القديم بمطابقة الصنف عبر كل الأوامر — نفس الـ Response Shape بالظبط، مفيش Breaking Changes.

### المرحلة 4 — Frontend
- `requestLinkingApi.ts` (جديد) + Endpoint جديدين (`open-for-order`, `{id}/order-lines`) على `PurchaseRequestsController`.
- `PurchaseOrderEditPage.tsx`: `chooseRequest`/`loadRequestLines`/`remainingOf` + عمود "المتبقي في الطلب"، بنفس نمط `PurchaseInvoiceEditPage.tsx`.
- اكتُشف وانصلح Bug صامت في `reset()` effect (كان بيفصل السطور عن الطلب بصمت عند أي تعديل) — موثّق في `Known-Issues.md`.

### المرحلة 5 — 16 اختبار
- 15 اختبار في `Habbak.ERP.IntegrationTests` (14 method، منهم Theory بحالتين).
- 2 اختبار في `Habbak.ERP.ApiTests/OperationalCycle/PurchaseOrderRequestLinkingBackfillTests.cs` (Backward Compat + Consistency).
- اكتُشف وانصلح Regression حقيقي (تغيير سلوك متعمّد) في اختبار قديم `PurchasingQaBatch20260921Tests`.
- **النتائج النهائية**: `Habbak.ERP.ApiTests` 243/243 ✅، `Habbak.ERP.IntegrationTests` 104/104 ✅.

---

## 3. Migration على الداتابيز الحقيقية

| الخطوة | النتيجة |
|---|---|
| Backup كامل (`HabbakErp` قبل Remarks7) | ✅ `Docs/Migrations/Backups/2026-09-25-Pre-Remarks7/HabbakErp_2026-09-25-PreRemarks7.bak` (~47 ميجا) |
| نسخة تجريبية (`HabbakErp_Trial_Remarks7`) | ✅ اتعملت، الـ Migration اتأكدت عليها الأول، بعدين اتمسحت |
| تطبيق على `HabbakErp` الحقيقية | ✅ نجح (بعد تجاوز `DesignTimeDbContextFactory` بـ `--connection` صريح — تفاصيل في Bug-004) |
| استعلامات التحقق (4) | ✅ كلهم منطقيين، صفر شذوذ (`OrderedQuantity > Quantity` = 0 صف) |
| تنظيف (Trial DB + القاعدة المعزولة العرضية) | ✅ اتمسحوا الاتنين بعد التأكد إنهم مش مستخدمين |

**نتائج الاستعلامات على `HabbakErp` الحقيقية**: سطور اتربطت = 2، سطور طلب فيها `OrderedQuantity > 0` = 2، توزيع الحالات: `Approved`=2، `Converted`=2، شذوذ = 0.

---

## 4. Frontend Manual Test

اتعمل على بيانات حقيقية (مش Test DB) بعد الـ Migration مباشرة:

1. **إنشاء**: أمر شراء `PO-00010` من طلب `PR-00010` — "تحميل سطور الطلب" عبّى السطر تلقائيًا (صنف، كمية، وحدة، سعر مقترح من `SupplierPriceHistory`/`StandardCost`) — البانل عرض "في الأمر ده: 1,000 / المتبقي: 0" صح.
2. **تعديل**: الكمية 1000 → 600 — البانل اتحدّث فورًا لـ "في الأمر ده: 600 / المتبقي: 400" — ده أكّد إن إصلاح الـ `reset()` bug شغال على بيانات حقيقية مش بس في الاختبارات.
3. **إلغاء**: الأمر اتلغى، الداتابيز اتأكدت مباشرة: `OrderedQuantity = 0`, `PurchaseRequest.Status = Approved` (رجعت من `PartiallyConverted`) — صح 100%.

---

## 5. النقاط المفتوحة (4) — موثّقة كـ Bugs/Tasks منفصلة

| # | العنوان | الملف | الأولوية |
|---|---|---|---|
| 1 | Panel refresh after Cancel/Reject | [`Docs/My Remarks/Bugs/Bug-003-RequestPanel-Refresh.md`](../../Docs/My%20Remarks/Bugs/Bug-003-RequestPanel-Refresh.md) | 🟢 صغير |
| 2 | `DesignTimeDbContextFactory` Hardcoded | [`Docs/My Remarks/Bugs/Bug-004-DesignTimeFactory.md`](../../Docs/My%20Remarks/Bugs/Bug-004-DesignTimeFactory.md) | 🔴 عالي |
| 3 | `AllowManualOrderLines` UI ناقصة | [`Docs/My Remarks/Tasks/Task-001-ManualOrderLines-UI.md`](../../Docs/My%20Remarks/Tasks/Task-001-ManualOrderLines-UI.md) | 🟡 متوسط |
| 4 | تزاحم LocalDB (مش محتاج إصلاح) | موثّق في [`Known-Issues.md`](Known-Issues.md) | — |

---

## 6. الملفات المتأثرة (مرجع سريع)

**Backend**:
`PurchaseRequest.cs`, `PurchaseOrder.cs`, `PurchaseCycleSettings.cs`, `PurchaseOrderConfiguration.cs`, `PurchaseRequestConfiguration.cs`, `20260925141556_PurchaseOrderRequestLinking.cs` (+ Designer)، `PurchaseOrderRequestLinking.cs` (جديد)، `CreatePurchaseOrderCommand.cs`, `UpdatePurchaseOrderCommand.cs`, `CancelPurchaseOrderCommand.cs`, `RejectPurchaseOrderCommand.cs`, `PurchaseOrderDtos.cs`, `PurchaseOrderLineBuilder.cs`, `GetPurchaseOrderByIdQuery.cs`, `GetPurchaseOrderSourceRequestQuery.cs`, `GetOpenPurchaseRequestsQuery.cs` (جديد)، `GetPurchaseRequestLinesForOrderQuery.cs` (جديد)، `PurchaseOrdersController.cs`, `PurchaseRequestsController.cs`.

**Frontend**:
`requestLinkingApi.ts` (جديد)، `PurchaseOrderEditPage.tsx`, `api.ts`, `types.ts`, `ar.json`, `en.json`.

**Tests**:
`PurchaseOrderRequestLinkingTests.cs` (جديد، IntegrationTests)، `PurchaseOrderRequestLinkingBackfillTests.cs` (جديد، ApiTests)، `PurchasingQaBatch20260921Tests.cs` (تعديل لتصحيح Regression).

**Docs**:
`Known-Issues.md`، `Next-Steps.md`، هذا التقرير، + 3 ملفات Bug/Task منفصلة.

---

## 7. القرار

**Remarks7 مقفول.** التنفيذ اتبع نمط Remarks6 حرفيًا من الـ Domain لحد الـ Frontend والاختبارات، والنقاط المفتوحة الأربعة اتوثّقت بشكل منفصل عشان تُتناول في مهام مستقلة لاحقًا. مفيش أي تعديل إضافي هيتعمل على الكود من غير تعليمات جديدة.
