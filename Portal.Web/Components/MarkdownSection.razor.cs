using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Portal.Web.Components;

public partial class MarkdownSection : FluentComponentBase
{
    [Parameter]
    public MarkupString HtmlContent { get; set; }
}
