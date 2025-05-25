const parents = [
  {
    email: "sam.smith@example.com",
    name: "Sam Smith",
    relationship: "Father",
    children: [
      {
        firstName: "Alex",
        lastName: "Smith",
        tutorGroup: "Group B"
      },
      {
        firstName: "Emily",
        lastName: "Smith",
        tutorGroup: "Group D"
      }
    ]
  },
  {
    email: "pat.williams@example.com",
    name: "Pat Williams",
    relationship: "Parent",
    children: [
      {
        firstName: "Taylor",
        lastName: "Williams",
        tutorGroup: "Group C"
      }
    ]
  },
  {
    email: "robin.brown@example.com",
    name: "Robin Brown",
    relationship: "Guardian",
    children: [
      {
        firstName: "Morgan",
        lastName: "Brown",
        tutorGroup: "Group A"
      },
      {
        firstName: "Riley",
        lastName: "Brown",
        tutorGroup: "Group E"
      }
    ]
  },
  {
    email: "jordan.johnson@example.com",
    name: "Jordan Johnson",
    relationship: "Mother",
    children: [
      {
        firstName: "Jamie",
        lastName: "Johnson",
        tutorGroup: "Group D"
      },
      {
        firstName: "Quinn",
        lastName: "Johnson",
        tutorGroup: "Group A"
      }
    ]
  },
  {
    email: "terry.davis@example.com",
    name: "Terry Davis",
    relationship: "Father",
    children: [
      {
        firstName: "Casey",
        lastName: "Davis",
        tutorGroup: "Group E"
      }
    ]
  }
];

const staff = [
  { 
    email: "z.richards@school.edu", 
    name: "Ms. Z Richards", 
    role: "IT Support" 
  },
  { 
    email: "j.edwards@school.edu", 
    name: "Mr. J Edwards", 
    role: "IT Manager" 
  },
  { 
    email: "v.thompson@school.edu", 
    name: "Mrs. V Thompson", 
    role: "Admin" 
  },
  { 
    email: "t.patel@school.edu", 
    name: "Dr. T Patel", 
    role: "Teacher" 
  },
  { 
    email: "d.wilson@school.edu", 
    name: "Mr. D Wilson", 
    role: "Principal" 
  }
];

const tickets = [
  {
    id: "ticket-1001",
    title: "Cannot access online learning platform",
    closed: false,
    created: "2025-04-15T09:30:00Z",
    updated: "2025-04-28T14:45:00Z",
    studentFirstName: "Alex",
    studentLastName: "Smith",
    tutorGroup: "Group B",
    assigneeName: "Ms. Z Richards",
    assigneeEmail: "z.richards@school.edu",
    parentName: "Sam Smith",
    parentEmail: "sam.smith@example.com",
    parentRelationship: "Father"
  },
  {
    id: "ticket-1002",
    title: "Missing assignment submission",
    closed: false,
    created: "2025-04-20T15:22:00Z",
    updated: "2025-04-27T11:15:00Z",
    studentFirstName: "Taylor",
    studentLastName: "Williams",
    tutorGroup: "Group C",
    assigneeName: "Dr. T Patel",
    assigneeEmail: "t.patel@school.edu",
    parentName: "Pat Williams",
    parentEmail: "pat.williams@example.com",
    parentRelationship: "Parent"
  },
  {
    id: "ticket-1003",
    title: "School email login issues",
    closed: false,
    created: "2025-04-25T08:10:00Z",
    updated: "2025-04-25T10:35:00Z",
    studentFirstName: "Morgan",
    studentLastName: "Brown",
    tutorGroup: "Group A",
    assigneeName: "Ms. Z Richards",
    assigneeEmail: "z.richards@school.edu",
    parentName: "Robin Brown",
    parentEmail: "robin.brown@example.com",
    parentRelationship: "Guardian"
  },
  {
    id: "ticket-1004",
    title: "Request for extra learning resources",
    closed: true,
    created: "2025-03-15T13:45:00Z",
    updated: "2025-04-01T09:20:00Z",
    studentFirstName: "Jamie",
    studentLastName: "Johnson",
    tutorGroup: "Group D",
    assigneeName: "Mrs. V Thompson",
    assigneeEmail: "v.thompson@school.edu",
    parentName: "Jordan Johnson",
    parentEmail: "jordan.johnson@example.com",
    parentRelationship: "Mother"
  },
  {
    id: "ticket-1005",
    title: "Internet connectivity in Room 204",
    closed: true,
    created: "2025-03-20T10:15:00Z",
    updated: "2025-03-22T15:40:00Z",
    studentFirstName: "Casey",
    studentLastName: "Davis",
    tutorGroup: "Group E",
    assigneeName: "Mr. J Edwards",
    assigneeEmail: "j.edwards@school.edu",
    parentName: "Terry Davis",
    parentEmail: "terry.davis@example.com",
    parentRelationship: "Father"
  }
];

// Function to simulate fetching ticket conversation 
function getTicketMessages(ticketId) {
  // Simulated message data for each ticket
  const ticketMessages = {
    "ticket-1001": [
      {
        timestamp: "2025-04-15T09:30:00Z",
        authorEmail: "sam.smith@example.com",
        authorName: "Sam Smith",
        isEmployee: false,
        content: "My child cannot log into the online learning platform. We've tried resetting the password but it still doesn't work.",
        attachments: []
      },
      {
        timestamp: "2025-04-28T14:45:00Z",
        authorEmail: "z.richards@school.edu",
        authorName: "Ms. Z Richards",
        isEmployee: true,
        content: "Thank you for reporting this. I've checked the account and it appears there was an issue with the account activation. I've fixed this now. Please try logging in again and let me know if you still have problems.",
        attachments: []
      }
    ],
    "ticket-1002": [
      {
        timestamp: "2025-04-20T15:22:00Z",
        authorEmail: "pat.williams@example.com",
        authorName: "Pat Williams",
        isEmployee: false,
        content: "Taylor submitted the math assignment last Friday but it's not showing as received in the system.",
        attachments: [
          {
            fileName: "submission_screenshot.png",
            url: "https://example.com/files/submission_screenshot.png"
          }
        ]
      },
      {
        timestamp: "2025-04-27T11:15:00Z",
        authorEmail: "t.patel@school.edu",
        authorName: "Dr. T Patel",
        isEmployee: true,
        content: "I'll check our submission records. Sometimes there's a delay in the system updating. Can you confirm which assignment exactly and when it was submitted?",
        attachments: []
      }
    ],
    "ticket-1003": [
      {
        timestamp: "2025-04-25T08:10:00Z",
        authorEmail: "morgan.brown@student.school.edu",
        authorName: "Morgan Brown",
        isEmployee: false,
        content: "I can't access my school email. It says my account is locked.",
        attachments: []
      },
      {
        timestamp: "2025-04-25T10:35:00Z",
        authorEmail: "z.richards@school.edu",
        authorName: "Ms. Z Richards",
        isEmployee: true,
        content: "I'll unlock your account right away. For security reasons, accounts get locked after multiple incorrect password attempts. Would you like me to reset your password as well?",
        attachments: []
      }
    ],
    "ticket-1004": [
      {
        timestamp: "2025-03-15T13:45:00Z",
        authorEmail: "jordan.johnson@example.com",
        authorName: "Jordan Johnson",
        isEmployee: false,
        content: "Jamie would benefit from some additional resources for the upcoming science project. Are there any available?",
        attachments: []
      },
      {
        timestamp: "2025-03-18T14:30:00Z",
        authorEmail: "v.thompson@school.edu",
        authorName: "Mrs. V Thompson",
        isEmployee: true,
        content: "I've added Jamie to our extended learning platform which has additional science resources. Login details have been sent to your registered email address.",
        attachments: [
          {
            fileName: "science_resources_guide.pdf",
            url: "https://example.com/files/science_resources_guide.pdf"
          }
        ]
      },
      {
        timestamp: "2025-04-01T09:20:00Z",
        authorEmail: "jordan.johnson@example.com",
        authorName: "Jordan Johnson",
        isEmployee: false,
        content: "Thank you! We received the login details and Jamie is already using the resources. This has been very helpful.",
        attachments: []
      }
    ],
    "ticket-1005": [
      {
        timestamp: "2025-03-20T10:15:00Z",
        authorEmail: "d.wilson@school.edu",
        authorName: "Mr. D Wilson",
        isEmployee: true,
        content: "The internet connection in Room 204 has been intermittent all week. This is affecting our ability to use online resources during class.",
        attachments: []
      },
      {
        timestamp: "2025-03-21T09:00:00Z",
        authorEmail: "j.edwards@school.edu",
        authorName: "Mr. J Edwards",
        isEmployee: true,
        content: "Thank you for reporting this. I've scheduled a technician to check the wireless access point in that room tomorrow morning.",
        attachments: []
      },
      {
        timestamp: "2025-03-22T15:40:00Z",
        authorEmail: "j.edwards@school.edu",
        authorName: "Mr. J Edwards",
        isEmployee: true,
        content: "The technician has replaced the faulty access point in Room 204. Please let me know if you experience any further issues.",
        attachments: [
          {
            fileName: "repair_report.pdf",
            url: "https://example.com/files/repair_report.pdf"
          }
        ]
      }
    ]
  };
  
  const ticket = tickets.find(t => t.id === ticketId) || {};
  const children = parents.find(p => p.email === ticket.parentEmail)?.children || [];
  return {
    messages: ticketMessages[ticketId] || [],
    children
  };
}