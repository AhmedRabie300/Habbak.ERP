/clear

اقرأ docs/Reports-UPDATED/00-System-Overview-UPDATED.md بس.

عايز تقرير محدّث ومفصل عن موديول "المخازن والتصنيع".
اكتبه في: docs/Reports-UPDATED/02-Inventory-Manufacturing.md

المحتوى:

1. الـ Entities الفعلية + العلاقات:
   - Items + ItemGroups + POSCategories
   - UnitsOfMeasure + ItemUnitConversion
   - Warehouses + ItemWarehouseSettings + BranchItemLimit
   - StockBalance + StockTransaction
   - WarehouseDocument (7 أنواع)
   - BranchRequest + BranchRequestLine
   - InventoryCount + InventoryCountLine
   - Recipe + RecipeLine
   - ProductionOrder
   - WasteRecord
   - CustodyOfficer
   - ProductionSalesModeSetting + ShortagePolicy

2. الـ Commands / Queries — قوائم فعلية
3. الـ Validators
4. محرك التكلفة:
   - AverageCost Per Warehouse
   - Weighted Average Equation
   - Cost Preservation Across Transfers
5. استهلاك الوصفات (RealTime vs Stocked)
6. الـ Frontend Pages + Components
7. الـ Database Schema
8. الـ Business Rules
9. الـ Risks / Gaps
10. الـ Tests

القواعد:
- اقرأ من الكود الفعلي
- مختصر (400 سطر max)
- مسارات الملفات الفعلية
- وثّق أي حاجة اتعملت من آخر تقرير

لما تخلص، قولي "خلصت التقرير 2".