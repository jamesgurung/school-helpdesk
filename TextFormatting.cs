using Markdig;
using ReverseMarkdown;

namespace SchoolHelpdesk;

public static class TextFormatting
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

  public static string ToParagraphs(string text)
  {
    var paragraphs = $"<p>{text.Trim().Replace("\n", "</p><p>")}</p>";
    return paragraphs.Replace("<p></p>", "<p>&nbsp;</p>");
  }

  public static string AppendSignature(string body, string senderName)
  {
    return string.IsNullOrEmpty(senderName)
      ? $"{body}\n\n\nBest wishes\n\n{School.Instance.Name}"
      : $"{body}\n\n\nBest wishes\n\n{senderName}\n{School.Instance.Name}";
  }
}
