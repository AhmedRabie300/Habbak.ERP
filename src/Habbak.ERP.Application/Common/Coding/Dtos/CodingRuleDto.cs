namespace Habbak.ERP.Application.Common.Coding.Dtos;

public class CodingRuleDto
{
    public string ScreenCode { get; set; } = null!;
    public string ScreenLabel { get; set; } = null!;
    public bool IsAutomatic { get; set; }
    public string Format { get; set; } = null!; // "NumbersOnly" | "LettersOnly" | "LettersAndNumbers"
    public string? Prefix { get; set; }
    public int SequenceLength { get; set; }
    public bool IsAttachmentMandatory { get; set; }
    public bool IsDescriptionMandatory { get; set; }
}
