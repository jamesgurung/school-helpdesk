using Azure;
using Azure.AI.OpenAI;
using OpenAI.Responses;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
      Follow the guidance closely and do not offer additional follow-up actions beyond what is stated in the guidance.
      Use gentle, considerate language and include kind expressions and pleasantries to ensure the message feels warm and is received positively.
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

  public static async Task<Parent> InferParentAsync(string body, List<Parent> parents, string ticketId)
  {
    var parentNames = parents.Select(o => o.Name.Replace("\"", string.Empty, StringComparison.OrdinalIgnoreCase)).ToList();

    var instructions = """
      You are an experienced receptionist in a UK secondary school. You will be shown a parent enquiry received by email.
      The sender's email address is shared by multiple parents of the same student.
      Identify the parent or carer who sent the email, from the list of names provided.
      Match based on the parent's name, title, initials, stated relationship to the student, or any other details they provide.
      Make reasonable inferences even if the name is not explicitly stated.
      Respond in a JSON object with the parent's name if it can be inferred, or null if it is impossible to tell.
      For example, if the options were "Mr A Smith" and "Mrs A Jones" and the email was signed "Andrew", you would respond with { "parentName": "Mr A Smith" }.
      """;

    var userMessage = ResponseItem.CreateUserMessageItem($"# Email received:\n\n{body}\n\n# Parent names:\n\n" + string.Join("\n", parentNames));

    var schema = BinaryData.FromString("""
    {
      "type": "object",
      "properties": {
        "parentName": {
          "anyOf": [
            { "type": "string", "enum": ["[PARENT_NAMES]"] },
            { "type": "null" }
          ]
        }
      },
      "required": ["parentName"],
      "additionalProperties": false
    }
    """.Replace("[PARENT_NAMES]", string.Join("\", \"", parentNames), StringComparison.Ordinal));

    var options = new ResponseCreationOptions
    {
      Instructions = instructions,
      ReasoningOptions = new ResponseReasoningOptions { ReasoningEffortLevel = ResponseReasoningEffortLevel.Low },
      StoredOutputEnabled = false,
      EndUserId = $"helpdesk-{ticketId}",
      TextOptions = new ResponseTextOptions { TextFormat = ResponseTextFormat.CreateJsonSchemaFormat("inference", schema, jsonSchemaIsStrict: true) }
    };

    var response = await _client.CreateResponseAsync([userMessage], options);
    var text = response.Value.OutputItems.Select(o => o as MessageResponseItem).FirstOrDefault(o => o is not null)?.Content.FirstOrDefault()?.Text;
    if (text is null) return null;
    var parentName = JsonDocument.Parse(text).RootElement.GetProperty("parentName").GetString();
    return parentName is null ? null : parents.FirstOrDefault(p => p.Name.Equals(parentName, StringComparison.OrdinalIgnoreCase));
  }

  public static async Task<Student> InferStudentAsync(string body, List<Student> students, string ticketId)
  {
    var studentNames = students.Select(o => $"{o.FirstName} {o.LastName} {o.TutorGroup}".Replace("\"", string.Empty, StringComparison.OrdinalIgnoreCase).Trim()).ToList();

    var instructions = """
      You are an experienced receptionist in a UK secondary school. You will be shown a parent enquiry received by email.
      The parent has multiple children at the school.
      Identify the child from the list of names provided.
      Match based on the child's name, year group, tutor group, gender, or any other details provided.
      Make reasonable inferences even if the name is not explicitly stated.
      Respond in a JSON object with the child's name if it can be inferred, or null if it is impossible to tell.
      For example, if the options were "John Smith 7ABC" and "Lauren Smith 8DEF" and the email referred to "my son", you would respond with { "studentName": "John Smith 7ABC" }.
      """;

    var userMessage = ResponseItem.CreateUserMessageItem($"# Email received:\n\n{body}\n\n# Student names:\n\n" + string.Join("\n", studentNames));

    var schema = BinaryData.FromString("""
    {
      "type": "object",
      "properties": {
        "studentName": {
          "anyOf": [
            { "type": "string", "enum": ["[STUDENT_NAMES]"] },
            { "type": "null" }
          ]
        }
      },
      "required": ["studentName"],
      "additionalProperties": false
    }
    """.Replace("[STUDENT_NAMES]", string.Join("\", \"", studentNames), StringComparison.Ordinal));

    var options = new ResponseCreationOptions
    {
      Instructions = instructions,
      ReasoningOptions = new ResponseReasoningOptions { ReasoningEffortLevel = ResponseReasoningEffortLevel.Low },
      StoredOutputEnabled = false,
      EndUserId = $"helpdesk-{ticketId}",
      TextOptions = new ResponseTextOptions { TextFormat = ResponseTextFormat.CreateJsonSchemaFormat("inference", schema, jsonSchemaIsStrict: true) }
    };

    var response = await _client.CreateResponseAsync([userMessage], options);
    var text = response.Value.OutputItems.Select(o => o as MessageResponseItem).FirstOrDefault(o => o is not null)?.Content.FirstOrDefault()?.Text;
    if (text is null) return null;
    var studentName = JsonDocument.Parse(text).RootElement.GetProperty("studentName").GetString();
    return studentName is null ? null : students.FirstOrDefault(s => $"{s.FirstName} {s.LastName} {s.TutorGroup}".Trim().Equals(studentName, StringComparison.OrdinalIgnoreCase));
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
