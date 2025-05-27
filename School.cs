namespace SchoolHelpdesk;

public class School
{
  public static School Instance { get; set; }

  public string Name { get; set; }
  public string AppWebsite { get; set; }
  public string HelpdeskEmail { get; set; }
  public IList<string> Admins { get; set; }
  public IList<string> Managers { get; set; }

  public Dictionary<string, Parent> ParentsByEmail { get; set; }
  public Dictionary<string, Staff> StaffByEmail { get; set; }

  public string UsersJson { get; set; }
  public string UsersJsonHash { get; set; }

  public string HtmlEmailTemplate { get; set; }
  public string TextEmailTemplate { get; set; }
}

public class CsvStudent
{
  public string FirstName { get; set; }
  public string LastName { get; set; }
  public string TutorGroup { get; set; }
  public string Relationship { get; set; }
  public string ParentTitle { get; set; }
  public string ParentFirstName { get; set; }
  public string ParentLastName { get; set; }
  public string ParentEmailAddress { get; set; }
}

public class CsvStaff
{
  public string Email { get; set; }
  public string Title { get; set; }
  public string FirstName { get; set; }
  public string LastName { get; set; }
}

public class Parent
{
  public string Email { get; set; }
  public string Name { get; set; }
  public string Relationship { get; set; }
  public List<Student> Children { get; set; }
}

public class Student
{
  public string FirstName { get; set; }
  public string LastName { get; set; }
  public string TutorGroup { get; set; }
}

public class Staff
{
  public string Email { get; set; }
  public string Name { get; set; }
}