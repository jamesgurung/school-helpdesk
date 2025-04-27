namespace SchoolHelpdesk;

public class School
{
  public static School Instance { get; set; }

  public string Name { get; set; }
  public string AppWebsite { get; set; }
  public IList<string> AdminUsers { get; set; }
}