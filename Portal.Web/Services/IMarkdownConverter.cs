namespace Portal.Web.Services;

public interface IMarkdownConverter
{
    string Convert(SiteInfo info, string markdown);
}
