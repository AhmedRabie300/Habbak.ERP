/clear

اقرأ docs/Reports-UPDATED/00-System-Overview-UPDATED.md بس.

عايز تقرير محدّث ومفصل عن:
1. موديول الحسابات العامة والحركات المالية
2. موديول الإعدادات والتنظيم

اكتبه في: docs/Reports-UPDATED/01-Accounting-Settings.md

المحتوى:

## الجزء الأول: الحسابات العامة

1. الـ Entities الفعلية + العلاقات
2. الـ Commands / Queries — قوائم فعلية
3. الـ Validators
4. الـ Posting Logic (الربط بـ PostingTemplateEngine)
5. الـ Frontend Pages + Components
6. الـ Database Schema
7. الـ Business Rules المطبقة
8. الـ Risks / Gaps
9. الـ Tests

## الجزء الثاني: الإعدادات والتنظيم

1. الـ Entities:
   - Company / Branch / Currency
   - MenuItem / CodingRule / SystemSettings
   - AuditLog / AuditLogArchive
2. الـ Commands / Queries
3. الـ Frontend Pages
4. الـ Business Rules
5. الـ Risks / Gaps
6. الـ Tests

## الجزء الثالث: Cross-Module

- إزاي الحسابات بتشتغل مع الإعدادات؟
- Coding Rules بتأثر على كل الموديولات إزاي؟
- Audit Log بيتكتب من مين؟

القواعد:
- اقرأ من الكود الفعلي
- مختصر (400 سطر max)
- مسارات الملفات الفعلية
- وثّق أي حاجة اتعملت من آخر تقرير

لما تخلص، قولي "خلصت التقرير 1".