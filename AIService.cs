using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using System.Text;
using System.Text.RegularExpressions;

namespace SchoolHelpdesk;

public static partial class AIService
{
  private static IChatClient _client;
  private static ChatOptions _options;

  public static void Configure(string endpoint, string apiKey, string deployment)
  {
    var azureClient = new ChatCompletionsClient(new Uri(endpoint), new AzureKeyCredential(apiKey), new AzureAIInferenceClientOptions());
    _client = azureClient.AsIChatClient(deployment);
    _options = new ChatOptions { Temperature = 0.2f };
  }

  public static async Task<string> GenerateReplyAsync(string studentName, List<Message> messages, string guidance)
  {
    var systemMessage = new ChatMessage(Microsoft.Extensions.AI.ChatRole.System,
      "You are an experienced teacher in a UK secondary school. You have received a parent enquiry and need to write a response.\n\n" +
      "You will be shown the conversation history in chronological order, starting with the oldest message. You will then be given guidance on how to respond.\n\n" +
      "Write a helpful response to the parent, in a warm, kind, and professional tone. Use British English spelling and terminology.\n\n" +
      "Respond with the email content ONLY. Write short plaintext paragraphs with no headings, bullet points, or formatting.\n\n" +
      "DO NOT include a greeting or sign-off. Only include the main body of the response.");

    var history = string.Join("\n\n", messages.Where(m => !m.IsPrivate).Select(
      (m, i) => $"## {(m.IsEmployee ? (i == 0 ? "Receptionist on behalf of the parent" : "Teacher") : "Parent")}:\n\n{m.Content}")
    );

    var userMessage = new ChatMessage(Microsoft.Extensions.AI.ChatRole.User,
      $"# Student name:\n\n{studentName}\n\n# Conversation history:\n\n{history}\n\n# Guidance on how to respond:\n\n{guidance}");

    var response = await _client.GetResponseAsync([systemMessage, userMessage], _options);
    return NormaliseText(response.Text);
  }

  private static string NormaliseText(string text)
  {
    if (string.IsNullOrWhiteSpace(text)) return string.Empty;
    var sb = new StringBuilder(text.Length);
    foreach (var c in text)
    {
      switch (c)
      {
        case '\r':     // Carriage return
        case '\u200B': // Zero-width space
        case '\u200C': // Zero-width non-joiner
        case '\u200D': // Zero-width joiner
        case '\u2060': // Word joiner
        case >= '\u202A' and <= '\u202E': // Bidirectional control characters
          break;
        case '\u2014': // Em dash
          sb.Append(" \u2013 ");
          break;
        case '\u00A0': // Non-breaking space
        case '\u202F': // Narrow no-break space
        case '\u2009': // Thin space
        case '\u200A': // Hair space
          sb.Append(' ');
          break;
        case '\u201C': // Left double quotation mark
        case '\u201D': // Right double quotation mark
          sb.Append('"');
          break;
        case '\u2018': // Left single quotation mark
        case '\u2019': // Right single quotation mark
          sb.Append('\'');
          break;
        case '\u2026': // Horizontal ellipsis
          sb.Append("...");
          break;
        default:
          sb.Append(c);
          break;
      }
    }
    return MultiSpaceRegex().Replace(sb.ToString(), " ").Trim();
  }

  [GeneratedRegex(@" {2,}")]
  private static partial Regex MultiSpaceRegex();
}
