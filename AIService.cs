using System.Text;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Responses;

namespace SchoolHelpdesk;

#pragma warning disable OPENAI001

public static partial class AIService
{
  private static OpenAIResponseClient _client;

  public static void Configure(string endpoint, string deployment, string apiKey)
  {
    var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
    _client = azureClient.GetOpenAIResponseClient(deployment);
  }

  public static async Task<string> GenerateReplyAsync(string studentName, List<Message> messages, string guidance, string ticketId)
  {
    var instructions = """
      You are an experienced teacher in a UK secondary school. You have received a parent enquiry and need to write a response.
      You will be shown the conversation history in chronological order, starting with the oldest message. You will then be given guidance on how to respond.
      Write a helpful response to the parent, in a warm, kind, and professional tone. Use British English spelling and terminology.
      Respond with the email content ONLY. Write short plaintext paragraphs with no headings, bullet points, or formatting.
      DO NOT include a greeting or sign-off. Only include the main body of the response.
      """;

    var history = string.Join("\n\n", messages.Where(m => !m.IsPrivate).Select(
      (m, i) => $"## {(m.IsEmployee ? (i == 0 ? "Receptionist on behalf of the parent" : "Teacher") : "Parent")}:\n\n{m.Content}")
    );

    var userMessage = ResponseItem.CreateUserMessageItem(
      $"# Student name:\n\n{studentName}\n\n# Conversation history:\n\n{history}\n\n# Guidance on how to respond:\n\n{guidance}");

    var options = new ResponseCreationOptions
    {
      Instructions = instructions,
      ReasoningOptions = new ResponseReasoningOptions { ReasoningEffortLevel = ResponseReasoningEffortLevel.Low },
      StoredOutputEnabled = false,
      EndUserId = $"helpdesk-{ticketId}"
    };

    var response = await _client.CreateResponseAsync([userMessage], options);
    return NormaliseText(response.Value.OutputItems.Select(o => o as MessageResponseItem).First(o => o is not null).Content.First().Text);
  }

  public static async Task<string> GenerateTitleAsync(string subject, string body, string ticketId)
  {
    var instructions = """
      You are an experienced receptionist in a UK secondary school. You will be shown a parent enquiry received by email.
      Write a concise title for the enquiry that summarises the main issue or request. Use no more than 3 words.
      Write in sentence case, without a full stop at the end. Do not include any reference to the student name or any staff names or roles.
      Avoid words like "complaint", "issue", "request", or "urgent". The title must be neutral and factual, suitable for display in a helpdesk ticketing system.
      Only include the title text in your response. Do not include any formatting or additional commentary.
      Examples of good titles: "Late bus", "Science homework", "Unwell today", "New contact details", "Sport Studies trip", "Lost property", "Password reset", "ADHD support".
      """;

    var userMessage = ResponseItem.CreateUserMessageItem($"# Email subject:\n\n{subject}\n\n# Email body:\n\n{body}");

    var options = new ResponseCreationOptions
    {
      Instructions = instructions,
      ReasoningOptions = new ResponseReasoningOptions { ReasoningEffortLevel = ResponseReasoningEffortLevel.Low },
      StoredOutputEnabled = false,
      EndUserId = $"helpdesk-{ticketId}"
    };

    var response = await _client.CreateResponseAsync([userMessage], options);
    var title = NormaliseText(response.Value.OutputItems.Select(o => o as MessageResponseItem).First(o => o is not null).Content.First().Text);
    if (title.Length > 40) title = title[..37].Trim() + "...";
    return title;
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
