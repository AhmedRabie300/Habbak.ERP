namespace Habbak.ERP.Application.Common.Navigation.Dtos;

public class MenuTreeNodeDto
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? RouteKey { get; set; }
    public string? IconKey { get; set; }
    public List<MenuTreeNodeDto> Children { get; set; } = [];
}
