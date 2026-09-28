# صلاحيات الحقول والأزرار على مستوى الشاشة

**التاريخ:** 2026-09-19 · **Migration:** `20260919095303_FieldAndButtonPermissions` (اتطبّقت على LocalDB وعلى SQL Server الحقيقي)

## الفكرة في سطرين
- **صلاحية الحقل بقت لكل شاشة:** نفس الحقل (مثلًا تليفون العميل) ممكن يبان في شاشة العملاء ويتخفي في شاشة فواتير المبيعات لنفس الدور.
- **الأزرار الخاصة ليها صلاحية مستقلة** (ترحيل، رفض، إلغاء، تعليق شيك، تعديل سعر…)، وكل ضغطة بتتسجّل في سجل المراجعة إلا لو الدور قافل التسجيل للزرار ده.
  `ScreenPermission` زي ما هي، وبتفضل تتحكم في أزرار الـ ActionBar العامة (حفظ، حذف، طباعة، تصدير).

## 1. قاعدة البيانات
| الجدول | التغيير |
|---|---|
| `FieldPermissions` | عمود جديد `ScreenCode nvarchar(100) NOT NULL`. الـ unique index القديم `(RoleId, EntityType, FieldName)` اتشال، واتعمل مكانه `(RoleId, ScreenCode, EntityType, FieldName)` بفلتر `IsDeleted = 0`. |
| `ButtonPermissions` (جديد) | `CompanyId, RoleId (FK Restrict), ScreenCode, ButtonCode, IsEnabled, RequiresAuditLog` + أعمدة المراجعة. عليه unique index `(RoleId, ScreenCode, ButtonCode)` بفلتر `IsDeleted = 0`. |
| `AuditActionType` | قيمة جديدة `ButtonPress = 11`. |

### ترحيل البيانات القديمة (بالترتيب)
1. العمود بيتضاف nullable.
2. كل قاعدة قديمة كانت بتسري على كل الشاشات اللي بتعرض الحقل، فبتتحوّل **لصف لكل شاشة من الشاشات دي**: أول شاشة بتاخد الصف الأصلي، والباقي بياخدوا نسخ. الـ mapping محفوظ جوه الـ migration نفسها كـ snapshot، مش مقروء من الكلاس الحي.
3. أي قاعدة لحقل مابقاش موجود في أي شاشة (Employee وFixedAsset، لأن مالهمش شاشات) بتتحفظ **soft-deleted** تحت `UNMAPPED`، يعني البيانات مابتضيعش.
4. العمود بيتحوّل NOT NULL، وبعدها الـ index الجديد وجدول الأزرار.
5. الـ `Down` بيرجّع كل حقل لصف واحد قبل ما يرجّع الـ index القديم.

اتجرّبت الـ migration على قاعدة مؤقتة فيها صفوف قديمة: `Customer.Phone` بقى صفين (SALES_CUSTOMERS وSALES_INVOICES)، و`Shift.ExpectedClosingCashAmount` بقى صفين (POS_SHIFT_CONSOLE وPOS_SHIFTS)، و`Employee.Salary` اتحفظ UNMAPPED/محذوف. الـ Down كمان اتجرّب، والقاعدة المؤقتة اتمسحت بعدها. القاعدتين الحقيقيتين كان فيهم 0 صف.

## 2. الكتالوجات (المرجع الوحيد)
**الحقول** في `Domain/Settings/Permissions/FieldPermission.cs` → `FieldPermissionCatalog.SensitiveFields` (شاشة → كيان → حقول):

| الشاشة | الحقول |
|---|---|
| SALES_CUSTOMERS | Customer: CreditLimit, Phone, Email |
| SALES_INVOICES | Customer: CreditLimit, Phone, Email · SalesInvoice: DiscountAmount |
| PURCHASING_SUPPLIERS | Supplier: CreditLimit, Phone, Email |
| POS_SHIFT_CONSOLE, POS_SHIFTS | Shift: ExpectedClosingCashAmount |
| POS_TABLE_BOARD, POS_RETURNS | POSPayment: CardTransactionReference |

**الأزرار** في `Application/Settings/Access/ButtonPermissionCatalog.cs`. كل زرار ليه `FallbackAction`، ودي صلاحية الشاشة اللي الـ endpoint بتاعه كان بيطلبها أصلًا، فمفيش حد اتغيّر عليه حاجة لما الكتالوج اتضاف:

| الشاشة | الأزرار (Fallback) |
|---|---|
| POS_TABLE_BOARD | Hold (Edit) · Cancel (Approve) · ApplyManualDiscount (Edit) · RedeemLoyalty (Edit) · FireToKitchen (Edit) · EditPrice (Edit) · VoidSentLine (Delete) · SplitBill (Edit، في الشاشة بس) · Reprint (Print، في الشاشة بس) |
| POS_CHECKS_OPEN / POS_CHECKS_HELD | Merge (Edit) |
| POS_SHIFT_CONSOLE | CloseShift (Edit) |
| POS_SHIFTS | ApproveClose · DayClose (Approve) |
| POS_DRAWER_MOVEMENTS | Approve · Reject (Approve) |
| SALES_INVOICES | Post · Reject · Cancel (Approve) |
| PURCHASING_PURCHASE_INVOICES | Submit (Edit) · Post · Reject · Cancel (Approve) |
| PURCHASING_PURCHASE_ORDERS | Send · Confirm (Edit) · Reject · Cancel (Approve) |
| INVENTORY_COUNTS | Start · SubmitForSettlement · Close (Edit) · SettleLine · CompleteSettlement · Reject · Cancel (Approve) |
| INVENTORY_PRODUCTION_ORDERS | Start · Complete (Edit) · Cancel (Approve) |

## 3. الطلب جاي من أنهي شاشة؟
- الـ frontend بيبعت مع كل طلب header اسمه `X-Screen-Code` فيه شاشة التاب المفتوحة.
- `ScreenPermissionFilter` بيقبل الـ header ده **بس لو المستخدم يقدر يشوف الشاشة دي** (أو صلاحياته كاملة). غير كده بيستخدم أول شاشة في `[Screen]` بتاع الـ controller، وبيحفظ الناتج في `ICurrentScreen`.
- ده بيمنع إن حد يكتب شاشة مش من حقه في الـ header عشان يهرب من قاعدة شاشته.
- حدود الحل ده: الطلب اللي بيطلع من تاب مخفي (polling) بياخد شاشة التاب المفتوح. ده مش تسريب، لأن المستخدم عنده صلاحية على الشاشة دي أصلًا.

## 4. التطبيق في السيرفر
**الحقول:**
- `[MaskFields(entity)]` بيفضّي الحقل حسب قاعدة الشاشة الحالية. اتضاف على `SalesInvoicesController` لـ SalesInvoice وCustomer.
- حفظ العميل والمورد بيحترم `CanEdit` للشاشة الحالية.
- خصم فاتورة المبيعات:
  - لو التعديل مقفول، الإنشاء بيتعمل من غير خصم (لا خصم الفاتورة ولا خصم البنود).
  - والتعديل بيحتفظ بالخصم المتخزّن (خصم البند بيتاخد من البند المتخزّن لنفس الصنف).
- سجل المراجعة للحقل بيتبع `RequiresAuditLog` للشاشة الحالية، والقيم الحساسة بتتسجّل `[REDACTED]` زي الأول.

**الأزرار:**
- `[ScreenButton(screen, code)]` على 34 endpoint (الدمج عليه اتنين: شيكات مفتوحة ومعلّقة).
- لو فيه صف للدور، هو اللي بيقرّر لوحده: `IsEnabled && View على الشاشة`. يعني ممكن يدّي الزرار لدور معندوش صلاحية الـ Fallback، أو يمنعه عن دور عنده الصلاحية. ولو مفيش صف، بيتطبّق فحص الشاشة العادي.
- الرفض بيرجع كود `BUTTON-DENIED`، والأدوار كاملة الصلاحية مابيتحطّلهاش صفوف (`SET-ROLE-FULL-ACCESS`).
- الضغطة الناجحة بتتسجّل `ButtonPress`، وفي `AdditionalData` بيبقى `{"screen","button","action"}`، إلا لو الصف فيه `RequiresAuditLog = false`. زرار الاعتماد اللي تسجيله مقفول بيرجع لتسجيل الاعتماد العادي.
- **EditPrice وVoidSentLine** مالهمش endpoint لوحدهم، لأنهم جزء من تعديل البند وحذفه. عشان كده `ButtonGuard` جوه الـ handler هو اللي بيفحصهم ويسجّلهم:
  - EditPrice: لما السعر يتغيّر أو يتعلّم يدوي.
  - VoidSentLine: لما البند يكون اتبعت للمطبخ.

**الخدمة (`IUserAccessService`):**
- `HasFieldPermissionOnScreenAsync(screen, entity, field, View|Edit)`
- `HasButtonPermissionAsync(screen, button)`
- `ShouldAuditButtonAsync(screen, button)`

## 5. الـ API
| Endpoint | الوصف |
|---|---|
| `GET /api/v1/permissions/field-catalog?screenCode=` | الشاشات وحقولها |
| `GET /api/v1/permissions/button-catalog?screenCode=` | الشاشات وأزرارها (بالـ fallback وهل الزرار في الشاشة بس) |
| `GET /api/v1/permissions/role/{roleId}/fields?screenCode=` | كل حقل وحقوق الدور عليه + `isConfigured` |
| `GET /api/v1/permissions/role/{roleId}/buttons?screenCode=` | كل زرار وهل هو مسموح فعليًا (قاعدة أو fallback) + `isConfigured` |
| `PUT /api/v1/settings/roles/{id}/fields?screenCode=` | بيستبدل قواعد الحقول لشاشة واحدة |
| `PUT /api/v1/settings/roles/{id}/buttons?screenCode=` | بيستبدل قواعد الأزرار لشاشة واحدة |
| `GET /api/v1/auth/me` | `fieldPermissions`: شاشة ← كيان ← حقل ← `{canView, canEdit}` · `buttonPermissions`: شاشة ← زرار ← bool |

كل endpoints الـ `/permissions` محمية بصلاحية شاشة الأدوار.

## 6. الـ Frontend
- `app/api.ts`: الـ interceptor بيحط `X-Screen-Code`، و`AppLayout` بيحدّد الشاشة وقت الـ render عشان طلبات التاب الجديد تطلع بالشاشة الصح.
- الـ hooks في `features/auth/access.tsx`:
  - `useFieldPermission(screen, entity, field, 'view'|'edit')`
  - `useButtonPermission(screen, button)`
  - `useButtonChecker()` لما تكون عايز تفحص أكتر من زرار.
  - `useFieldRights` القديم اتشال، لأنه ماكانش مستخدم في أي مكان.
- **الفورمات بقت بتحترم صلاحيات الحقول** (`useFieldAccess(screen, entity)`):
  - فورم العميل وفورم المورد: التليفون والإيميل وحد الائتمان.
  - فاتورة المبيعات: خصم الفاتورة وخصم البنود.
  - قائمة العملاء: عمود حد الائتمان.
  - الحقل اللي مالوش عرض بيختفي، واللي مالوش تعديل بيتقفل.
  - اتصلّح كمان crash كان بيحصل في قائمة العملاء وفورم العميل لما حد الائتمان يرجع مخفي (`toFixed` على null).
- `ActionBar`: فيه `buttonCode` (و`buttonScreen` اختياري)، ولو اتحدّد بيحلّ محل الربط بالـ key.
- الأزرار اللي اتحمت:
  - فاتورة المبيعات: ترحيل، رفض، إلغاء
  - فاتورة المشتريات: إرسال للاعتماد، ترحيل، رفض، إلغاء
  - أمر الشراء: إرسال، تأكيد، رفض، إلغاء
  - الجرد: بدء، رفع للتسوية، تسوية بند، إنهاء التسوية، إقفال، رفض، إلغاء
  - أمر الإنتاج: بدء، إنهاء، إلغاء
  - الشيك: خصم يدوي وإزالته، إرسال للمطبخ، تعليق واستكمال، إلغاء، استبدال النقاط وإزالته، تعديل السعر، حذف بند اتبعت، إعادة الطباعة
  - الدفع: تقسيم الفاتورة
  - الشيكات المفتوحة والمعلّقة: دمج
  - الوردية: إقفال، اعتماد الإقفال، إقفال اليوم
  - حركات الدرج: اعتماد، رفض
- شاشة الدور بقى فيها 3 تابات:
  - **الشاشات**: الجدول القديم.
  - **الحقول حسب الشاشة**: بتختار الشاشة وتحدّد حقولها.
  - **الأزرار حسب الشاشة**: بتختار الشاشة، وجنب كل زرار الـ fallback بتاعه وحالته الحالية للدور، وتقدر تعمل قاعدة خاصة (مسموح / يتسجّل). الأزرار اللي في الشاشة بس عليها علامة.

## 7. الاختبارات
- `ApiTests/FieldButtonPermissionsTests.cs`: 15 اختبار جديد.
  - نفس الحقل على شاشتين بقواعد مختلفة.
  - header لشاشة المستخدم مايقدرش يشوفها بيتجاهَل.
  - قاعدة لحقل مش في كتالوج الشاشة بتترفض، ووضع الحقول حسب الدور.
  - الـ unique index للحقول (نفس الحقل على شاشتين مسموح، والتكرار على نفس الشاشة مرفوض).
  - صف مقفول بيمنع زرار صلاحية الشاشة كانت بتسمح بيه.
  - صف مفتوح بيدّي الزرار لدور عنده عرض بس، ومابيفتحش شاشة الدور مش شايفها.
  - زرار مش في الكتالوج بيترفض، ومفيش صفوف لدور كامل الصلاحية.
  - الـ unique index للأزرار.
  - حالة الأزرار للدور بتبيّن الـ fallback لحد ما تتعمل قاعدة.
  - endpoints الكتالوج ومين يقدر يقراها.
  - شكل `/auth/me`.
  - تسجيل الضغطة، وإلغاؤه لما الصف يقول كده.
  - EditPrice وVoidSentLine جوه الـ handler.
  - كل `[ScreenButton]` لازم يكون في الكتالوج، وشاشته من شاشات الـ controller، والـ fallback بتاعه يطابق فحص الـ endpoint. وكل زرار مكتوب إن السيرفر بيطبّقه، مطبّق فعلًا.
- الإجمالي **ApiTests 136/136 · IntegrationTests 75/75**. اتعدّل اختباران قديمان عشان يضيفوا `ScreenCode`.
- `tsc -b` نضيف، ماعدا الأخطاء اللي كانت موجودة قبل كده في PaymentPage وDeliveryOrdersListPage وPOSReturnEditPage وPriceListEditPage.
- اتجرّب في المتصفح على LocalDB: التابات الـ3 على دور SALES_REP، وحفظ قاعدة زرار رجع 204. بيانات التجربة اتمسحت، والسيرفرات اتقفلت.

## 8. اختلافات عن الطلب
- **أكواد الشاشات هي أكواد MenuItem** (SALES_INVOICES، POS_TABLE_BOARD…) مش أسماء عامة، عشان تبقى نفس أكواد `ScreenPermission`.
- **أزرار وشاشات مش موجودة ماتحطّتش في الكتالوج:**
  - Employee وFixedAsset: مالهمش شاشات.
  - SalesContract: اترفض قبل كده.
  - VoidRefundPayment وConvertToInvoice.
  - Reverse: الإلغاء هو العكس.
  - Print وExport: فضلوا مع ScreenPermission.
  - الزرار اللي ماحدش يقدر يضغطه يبقى مفتاح مش متوصّل بحاجة.
- **SplitBill وReprint في الشاشة بس:** مابيكلّموش السيرفر لوحدهم، فالصلاحية بتخفيهم بس، وده مكتوب جنبهم في شاشة الدور.
- **تحديد الشاشة بالـ header** بدل parameter في كل endpoint.
- **مفيش PublicId ولا FK للشركة** على `ButtonPermission`، زي باقي جداول المشروع.
- **الحقل المخفي بيرجع `null`**، والـ `[REDACTED]` لسجل المراجعة بس.
