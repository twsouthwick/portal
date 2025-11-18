using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Parsers.Inlines;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;

namespace Portal.Web.Services;

internal sealed class MarkdownConverter : IMarkdownConverter, IMarkdownExtension
{
    private readonly MarkdownPipeline _pipeline;
    private static readonly object Key = new();

    public MarkdownConverter()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Use(this)
            .Build();
    }

    public string Convert(SiteInfo info, string markdown)
    {
        var context = new MarkdownParserContext();
        context.Properties.Add(Key, info);

        return Markdown.ToHtml(markdown, _pipeline, context);
    }

    void IMarkdownExtension.Setup(MarkdownPipelineBuilder pipeline)
    {
        pipeline.InlineParsers.Insert(0, new ContextSetter());
    }

    private sealed class ContextSetter : LinkInlineParser
    {
        public override bool Match(InlineProcessor processor, ref StringSlice slice)
        {
            if (base.Match(processor, ref slice))
            {
                if (processor.Inline is LinkInline { IsImage: true } inline && processor.Context is { } context && context.Properties.TryGetValue(Key, out var value) && value is SiteInfo info)
                {
                    inline.SetData(Key, info);
                }
                return true;
            }

            return false;
        }
    }

    void IMarkdownExtension.Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer.ObjectRenderers.FindExact<LinkInlineRenderer>() is { } link)
        {
            link.TryWriters.Remove(SiteImgRoute);
            link.TryWriters.Add(SiteImgRoute);
        }
    }

    private bool SiteImgRoute(HtmlRenderer renderer, LinkInline obj)
    {
        if (obj.IsImage && obj.Url is { } && obj.GetData(Key) is SiteInfo details)
        {
            obj.Url = $"{details.Id}/{obj.Url}";
        }

        return false;
    }
}
