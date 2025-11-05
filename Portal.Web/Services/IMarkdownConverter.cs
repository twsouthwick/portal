namespace Portal.Web.Services;

public interface IMarkdownConverter
{
    string Convert(SubsiteInfo info, string markdown);
}
