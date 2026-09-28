/clear

عايز تقرير محدّث عن السيستم كامل.
اكتبه في: docs/Reports-UPDATED/00-System-Overview-UPDATED.md

المحتوى:

1. الإحصائيات الحالية (من الكود الفعلي):
   - عدد الموديولات
   - عدد الـ Entities
   - عدد الـ Commands / Queries
   - عدد الـ Controllers
   - عدد الـ Migrations
   - عدد الـ Tests (API + Integration)

2. الـ 8 موديولات (اتأكد إنهم كلهم مبنيين):
   1. الحسابات العامة والحركات المالية
   2. المخازن والتصنيع
   3. المشتريات
   4. المبيعات
   5. نقاط البيع
   6. الأصول الثابتة والصيانة
   7. الصلاحيات والمستخدمين
   8. الإعدادات والتنظيم

3. محرك الترحيل ومحرر القيود:
   - الكيانات الفعلية
   - PostingTemplate / PostingTemplateLine
   - PostingEvaluator
   - Resolvers
   - JournalEntryTemplateSnapshot
   - الشاشات المربوطة
   - القوالب القياسية

4. الـ Tech Stack (Backend + Frontend + DB)

5. الـ Local + Cloud Setup:
   - SQL Server على الكاشير
   - SQL Server مركزي
   - Sync Strategy

6. الـ Offline Behavior لكل موديول

7. الـ Projects في الـ Solution + المسارات

8. الـ Conventions

القواعد:
- اقرأ من الكود الفعلي، مش من ملفات المواصفات
- مختصر (300 سطر max)
- مسارات الملفات الفعلية
- لو لقيت حاجة مش موجودة في تقارير سابقة بس موجودة في الكود → وثّقها

لما تخلص، قولي "خلصت التقرير 0".