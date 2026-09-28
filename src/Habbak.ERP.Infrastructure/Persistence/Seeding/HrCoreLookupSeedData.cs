using Habbak.ERP.Domain.HR;
using Habbak.ERP.Domain.Organization;

namespace Habbak.ERP.Infrastructure.Persistence.Seeding;

/// <summary>
/// Data rows (not menu items) for the 5 "seed-only" lookups that have no admin screen in this phase
/// (Docs/Implementation/HR-Core-Plan.md §1.1, Hybrid decision 2026-09-26) — wired into
/// SystemDataSeeder the same way as MenuItemSeedData/FieldLabelSeedData: inserted once, only if the
/// table is still empty. Nationality is deliberately seeded without a CountryId link — linking all of
/// these to real Country rows would require seeding a full country list too, which was not asked for
/// and is out of this batch's scope; Country itself gets a real admin screen (1.1.4/1.5) to populate.
/// </summary>
public static class HrCoreLookupSeedData
{
    public static List<Nationality> BuildNationalities() =>
    [
        new() { Code = "EGY", NameAr = "مصري", NameEn = "Egyptian", IsActive = true },
        new() { Code = "SAU", NameAr = "سعودي", NameEn = "Saudi", IsActive = true },
        new() { Code = "ARE", NameAr = "إماراتي", NameEn = "Emirati", IsActive = true },
        new() { Code = "KWT", NameAr = "كويتي", NameEn = "Kuwaiti", IsActive = true },
        new() { Code = "JOR", NameAr = "أردني", NameEn = "Jordanian", IsActive = true },
        new() { Code = "SDN", NameAr = "سوداني", NameEn = "Sudanese", IsActive = true },
        new() { Code = "SYR", NameAr = "سوري", NameEn = "Syrian", IsActive = true },
        new() { Code = "LBN", NameAr = "لبناني", NameEn = "Lebanese", IsActive = true },
        new() { Code = "PSE", NameAr = "فلسطيني", NameEn = "Palestinian", IsActive = true },
        new() { Code = "YEM", NameAr = "يمني", NameEn = "Yemeni", IsActive = true },
        new() { Code = "IRQ", NameAr = "عراقي", NameEn = "Iraqi", IsActive = true },
        new() { Code = "LBY", NameAr = "ليبي", NameEn = "Libyan", IsActive = true },
        new() { Code = "MAR", NameAr = "مغربي", NameEn = "Moroccan", IsActive = true },
        new() { Code = "DZA", NameAr = "جزائري", NameEn = "Algerian", IsActive = true },
        new() { Code = "TUN", NameAr = "تونسي", NameEn = "Tunisian", IsActive = true },
        new() { Code = "QAT", NameAr = "قطري", NameEn = "Qatari", IsActive = true },
        new() { Code = "BHR", NameAr = "بحريني", NameEn = "Bahraini", IsActive = true },
        new() { Code = "OMN", NameAr = "عماني", NameEn = "Omani", IsActive = true }
    ];

    // Both genders included for completeness (an emergency contact can be a daughter or a wife, not
    // only a son or husband) — the request's own list only gave one side of each pair.
    public static List<RelationshipType> BuildRelationshipTypes() =>
    [
        new() { Code = "FATHER", NameAr = "أب", NameEn = "Father", IsActive = true },
        new() { Code = "MOTHER", NameAr = "أم", NameEn = "Mother", IsActive = true },
        new() { Code = "HUSBAND", NameAr = "زوج", NameEn = "Husband", IsActive = true },
        new() { Code = "WIFE", NameAr = "زوجة", NameEn = "Wife", IsActive = true },
        new() { Code = "SON", NameAr = "ابن", NameEn = "Son", IsActive = true },
        new() { Code = "DAUGHTER", NameAr = "ابنة", NameEn = "Daughter", IsActive = true },
        new() { Code = "BROTHER", NameAr = "أخ", NameEn = "Brother", IsActive = true },
        new() { Code = "SISTER", NameAr = "أخت", NameEn = "Sister", IsActive = true }
    ];

    public static List<MilitaryStatus> BuildMilitaryStatuses() =>
    [
        new() { Code = "COMPLETED", NameAr = "أدّى الخدمة", NameEn = "Completed", IsActive = true },
        new() { Code = "EXEMPTED", NameAr = "معافى", NameEn = "Exempted", IsActive = true },
        new() { Code = "DEFERRED", NameAr = "مؤجل", NameEn = "Deferred", IsActive = true },
        new() { Code = "NOT_APPLICABLE", NameAr = "لا ينطبق", NameEn = "Not Applicable", IsActive = true }
    ];

    // "دبلوم" added beyond the request's list — very common for F&B/retail staff (cashiers, baristas)
    // in Egypt, between Secondary and Bachelor's; a client this size will hit it immediately.
    public static List<QualificationType> BuildQualificationTypes() =>
    [
        new() { Code = "SECONDARY", NameAr = "ثانوي", NameEn = "Secondary", IsActive = true },
        new() { Code = "DIPLOMA", NameAr = "دبلوم", NameEn = "Diploma", IsActive = true },
        new() { Code = "BACHELOR", NameAr = "بكالوريوس", NameEn = "Bachelor's", IsActive = true },
        new() { Code = "MASTER", NameAr = "ماجستير", NameEn = "Master's", IsActive = true },
        new() { Code = "DOCTORATE", NameAr = "دكتوراه", NameEn = "Doctorate", IsActive = true }
    ];

    // Codes mirror TerminationType (Docs/Modules/10-Module-HR-Payroll.md §2.6) for easy correlation —
    // this table itself is a plain lookup, not wired to that enum in this batch.
    public static List<TerminationReason> BuildTerminationReasons() =>
    [
        new() { Code = "RESIGNATION", NameAr = "استقالة", NameEn = "Resignation", IsActive = true },
        new() { Code = "DISMISSAL", NameAr = "إنهاء", NameEn = "Dismissal", IsActive = true },
        new() { Code = "RETIREMENT", NameAr = "معاش", NameEn = "Retirement", IsActive = true },
        new() { Code = "DEATH", NameAr = "وفاة", NameEn = "Death", IsActive = true },
        new() { Code = "CONTRACT_END", NameAr = "نهاية عقد", NameEn = "Contract End", IsActive = true }
    ];
}
