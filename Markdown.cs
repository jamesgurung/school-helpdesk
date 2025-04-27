using Markdig;
using ReverseMarkdown;

namespace SchoolHelpdesk;

public static class Markdown
{
  private static readonly Converter _converter = new(new Config()
  {
    GithubFlavored = true,
    RemoveComments = true,
    SmartHrefHandling = true,
    UnknownTags = Config.UnknownTagsOption.Bypass
  });

  private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

  public static string ToHtml(string markdown)
  {
    return Markdig.Markdown.ToHtml(markdown, _pipeline);
  }

  public static string FromHtml(string html)
  {
    return _converter.Convert(html);
  }
}
