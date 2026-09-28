using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Habbak.ERP.Application.Attendance.AttendanceDevices.Commands;
using Habbak.ERP.Application.Common.Exceptions;

namespace Habbak.ERP.Application.Attendance.AttendanceDevices;

/// <summary>
/// Docs/Implementation/Phase-3B-Research.md §2/§6 (3B.5) — Fallback شغّال من اليوم الأول (تصدير USB
/// يدوي) لحد ما بروتوكول الـPush يتأكد على جهاز حقيقي. نفس شكل الأعمدة المتوقّع في البروتوكول
/// (PIN, Time, Status?, VerifyType?) — .dat/.txt بمحدد Tab (تصدير ZKTeco المعتاد)، .csv بفاصلة،
/// .xlsx (ClosedXML) لو عميل معيّن صدّر من برنامج إدارة الجهاز. صف أول مش قابل للتفسير (التاريخ
/// فشل الـParsing) بيتعامل معاه كـHeader ويتخطّى بهدوء، مش خطأ.
/// </summary>
public static class RawPunchFileParser
{
    public static IReadOnlyList<RawPunchLineInput> Parse(string fileName, byte[] content)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".xlsx" => ParseExcel(content),
            ".csv" => ParseDelimited(content, ","),
            ".txt" or ".dat" => ParseDelimited(content, "\t"),
            _ => throw new BusinessRuleException("HR-DEVICE-IMPORT-UNSUPPORTED-FORMAT", "صيغة الملف غير مدعومة. المدعوم: csv, txt, dat, xlsx.")
        };
    }

    private static IReadOnlyList<RawPunchLineInput> ParseDelimited(byte[] content, string delimiter)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = false,
            MissingFieldFound = null,
            BadDataFound = null,
            IgnoreBlankLines = true
        };

        using var stream = new MemoryStream(content);
        using var streamReader = new StreamReader(stream);
        using var csv = new CsvReader(streamReader, config);

        var result = new List<RawPunchLineInput>();
        while (csv.Read())
        {
            var line = TryParseRow(
                deviceUserId: csv.TryGetField<string>(0, out var v0) ? v0 : null,
                rawTimestamp: csv.TryGetField<string>(1, out var v1) ? v1 : null,
                rawStatus: csv.TryGetField<string>(2, out var v2) ? v2 : null,
                rawVerifyType: csv.TryGetField<string>(3, out var v3) ? v3 : null);

            if (line is not null)
            {
                result.Add(line);
            }
        }

        return result;
    }

    private static IReadOnlyList<RawPunchLineInput> ParseExcel(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First();

        var result = new List<RawPunchLineInput>();
        foreach (var row in worksheet.RowsUsed())
        {
            var line = TryParseRow(
                deviceUserId: row.Cell(1).GetString(),
                rawTimestamp: row.Cell(2).GetString(),
                rawStatus: row.Cell(3).GetString(),
                rawVerifyType: row.Cell(4).GetString());

            if (line is not null)
            {
                result.Add(line);
            }
        }

        return result;
    }

    /// <summary>سطر مش قابل للتفسير (Header، أو بيانات ناقصة/تالفة) بيترجع null ويتخطّى بهدوء —
    /// نفس فلسفة الـProtocol نفسه (§2): الملف خام زي ما هو، مفيش رفض للدفعة كلها بسبب سطر واحد.</summary>
    private static RawPunchLineInput? TryParseRow(string? deviceUserId, string? rawTimestamp, string? rawStatus, string? rawVerifyType)
    {
        if (string.IsNullOrWhiteSpace(deviceUserId) || string.IsNullOrWhiteSpace(rawTimestamp))
        {
            return null;
        }

        if (!DateTime.TryParse(rawTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
        {
            return null; // على الأرجح Header.
        }

        var status = int.TryParse(rawStatus, out var s) ? s : (int?)null;
        var verifyType = int.TryParse(rawVerifyType, out var v) ? v : (int?)null;

        return new RawPunchLineInput(deviceUserId.Trim(), DateTime.SpecifyKind(timestamp, DateTimeKind.Utc), status, verifyType);
    }
}
