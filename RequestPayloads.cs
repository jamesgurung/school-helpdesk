namespace SchoolHelpdesk;

public class ChangeAssigneePayload
{
  public string AssigneeEmail { get; set; }
  public string NewAssigneeEmail { get; set; }
}

public class ChangeStudentPayload
{
  public string AssigneeEmail { get; set; }
  public string StudentFirst { get; set; }
  public string StudentLast { get; set; }
  public string StudentTutorGroup { get; set; }
}

public class ChangeStatusPayload
{
  public string AssigneeEmail { get; set; }
  public bool IsClosed { get; set; }
}

public class ChangeTitlePayload
{
  public string AssigneeEmail { get; set; }
  public string NewTitle { get; set; }
}

public class NewMessagePayload
{
  public string AssigneeEmail { get; set; }
  public string Content { get; set; }
  public bool IsPrivate { get; set; }
}