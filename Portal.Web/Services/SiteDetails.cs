//namespace Portal.Web.Services;

///// <summary>
///// In-memory implementation of the subsite service
///// Replace with a database-backed implementation in production
///// </summary>
//public class SiteDetails : ISiteDetails
//{
//    private readonly List<SubsiteInfo> _subsites;

//    public string Name => "Test Portal";

//    public string Description => string.Empty;

//    public SiteDetails()
//    {
//        // Initialize with sample data
//        // In production, this would load from a database or API
//        _subsites = new List<SubsiteInfo>
//        {
//            new SubsiteInfo
//            {
//                Id = "1",
//                Name = "Employee Portal",
//                Description = "Access your personal information, benefits, and company policies",
//                Url = "/employee",
//                Icon = "bi bi-person-circle",
//                Category = "HR",
//                TargetAudience = "All Employees",
//                UsageInstructions = @"## Getting Started

//Log in with your **corporate credentials** to access your employee profile and personal information.

//### What You Can Do

//- **View and update** your personal information
//- **Access** pay stubs and tax documents
//- **Submit** time-off requests
//- **Review** benefits enrollment
//- **Access** company policies and procedures

//### Quick Tips

//> 💡 Remember to update your contact information when you move or change phone numbers.

//For assistance, contact HR at hr@company.com",
//                Features = new List<string>
//                {
//                    "View and update personal information",
//                    "Access pay stubs and tax documents",
//                    "Submit time-off requests",
//                    "Review benefits enrollment",
//                    "Access company policies and procedures"
//                },
//                Contact = "hr@company.com",
//                RequiresPermission = false
//            },
//            new SubsiteInfo
//            {
//                Id = "2",
//                Name = "IT Service Desk",
//                Description = "Submit IT tickets and track your requests",
//                Url = "/servicedesk",
//                Icon = "bi bi-headset",
//                Category = "IT",
//                TargetAudience = "All Employees",
//                UsageInstructions = @"## How to Submit a Ticket

//1. Click **New Ticket** in the main menu
//2. Select the appropriate **category** (Hardware, Software, Access, etc.)
//3. Provide a **clear description** of your issue
//4. Attach **screenshots** if applicable
//5. Click **Submit**

//### Tracking Your Requests

//Navigate to **My Tickets** to:
//- View ticket status
//- Add comments or updates
//- Communicate with IT staff
//- Close resolved tickets

//### Response Times

//| Priority | Response Time |
//|----------|---------------|
//| Critical | 1 hour |
//| High | 4 hours |
//| Medium | 1 business day |
//| Low | 3 business days |

//> 🔔 You'll receive email notifications for all ticket updates.",
//                Features = new List<string>
//                {
//                    "Submit and track support tickets",
//                    "Request software installations",
//                    "Report hardware issues",
//                    "Browse knowledge base articles",
//                    "Live chat with IT support"
//                },
//                Contact = "helpdesk@company.com",
//                RequiresPermission = false
//            },
//            new SubsiteInfo
//            {
//                Id = "3",
//                Name = "Document Management",
//                Description = "Access and share company documents securely",
//                Url = "/documents",
//                Icon = "bi bi-file-earmark-text",
//                Category = "General",
//                TargetAudience = "All Employees",
//                UsageInstructions = @"## Organizing Your Documents

//### Creating Folders
//1. Navigate to your workspace
//2. Click **New Folder**
//3. Name your folder and set permissions
//4. Start uploading files

//### Sharing Documents
//- Click the **Share** icon next to any file or folder
//- Enter email addresses or select groups
//- Set permissions: `View`, `Edit`, or `Admin`
//- Click **Send Invitation**

//### Version Control
//All documents are **automatically versioned**. To view previous versions:
//1. Right-click on the file
//2. Select **Version History**
//3. Preview or restore any previous version

//### Best Practices
//- Use descriptive file names
//- Organize with folders and tags
//- Set appropriate permissions
//- Delete old versions when no longer needed",
//                Features = new List<string>
//                {
//                    "Secure document storage",
//                    "Version control and history",
//                    "Collaborative editing",
//                    "Advanced search capabilities",
//                    "Custom folder permissions"
//                },
//                Contact = "support@company.com",
//                RequiresPermission = false
//            },
//            new SubsiteInfo
//            {
//                Id = "4",
//                Name = "Financial Dashboard",
//                Description = "View budgets, expense reports, and financial analytics",
//                Url = "/finance",
//                Icon = "bi bi-graph-up",
//                Category = "Finance",
//                TargetAudience = "Finance Team, Department Heads",
//                UsageInstructions = @"## Dashboard Overview

//Access **real-time financial data** through customizable dashboards.

//### Submitting Expense Reports
//1. Navigate to **Expense Reports** → **New Report**
//2. Enter report details (date, purpose, project code)
//3. Add individual expenses with receipts
//4. Submit for approval

//**Important:** Receipts are required for expenses over $25.

//### Budget Monitoring
//Department heads can:
//- View current budget allocation
//- Track spending against budget
//- Approve departmental expenditures
//- Generate custom reports

//### Financial Reports
//Generate reports by:
//- Date range
//- Department
//- Project code
//- Expense category

//> ⚠️ This portal contains sensitive financial information. Access is restricted and monitored.",
//                Features = new List<string>
//                {
//                    "Real-time budget tracking",
//                    "Expense report submission",
//                    "Financial analytics and reporting",
//                    "Purchase order management",
//                    "Invoice tracking"
//                },
//                Contact = "finance@company.com",
//                RequiresPermission = true
//            },
//            new SubsiteInfo
//            {
//                Id = "5",
//                Name = "Learning Center",
//                Description = "Access training materials and professional development courses",
//                Url = "/learning",
//                Icon = "bi bi-book",
//                Category = "HR",
//                TargetAudience = "All Employees",
//                UsageInstructions = @"## Enrolling in Courses

//### Browse Available Courses
//- Use the **search bar** to find specific topics
//- Filter by category, skill level, or department
//- View course ratings and reviews

//### Enrollment Process
//1. Click on a course to view details
//2. Review the course outline and duration
//3. Click **Enroll Now**
//4. Access course materials immediately

//### Completing Training

//**Required Courses:**
//- Must be completed by the specified deadline
//- Automatic reminders sent weekly
//- Certificate issued upon completion

//**Optional Courses:**
//- Self-paced learning
//- No deadlines
//- Earn badges and certificates

//### Tracking Progress
//Your **Learning Dashboard** shows:
//- Courses in progress
//- Completion percentage
//- Earned certificates
//- Recommended courses based on your role

//> 📚 Complete at least 20 hours of professional development annually.",
//                Features = new List<string>
//                {
//                    "Self-paced online courses",
//                    "Compliance training tracking",
//                    "Certification programs",
//                    "Skills assessments",
//                    "Learning paths by role"
//                },
//                Contact = "training@company.com",
//                RequiresPermission = false
//            },
//            new SubsiteInfo
//            {
//                Id = "6",
//                Name = "Network Tools",
//                Description = "Network monitoring, configuration, and diagnostic tools",
//                Url = "/network",
//                Icon = "bi bi-diagram-3",
//                Category = "IT",
//                TargetAudience = "IT Department",
//                UsageInstructions = @"## Network Monitoring Dashboard

//Real-time monitoring of all network devices and infrastructure.

//### Key Sections

//**Device Management**
//- View all network devices
//- Monitor device status and health
//- Configure device settings
//- Update firmware remotely

//**Bandwidth Analytics**
//- Real-time bandwidth usage
//- Historical trends and patterns
//- Identify bandwidth bottlenecks
//- Generate usage reports

//**Security Monitoring**
//- Active threat detection
//- Security event logs
//- Firewall rule management
//- Intrusion detection alerts

//### Common Tasks

//#### Running Diagnostics
//```bash
//# From the Tools menu, select:
//- Ping Test
//- Traceroute
//- Port Scanner
//- DNS Lookup
//```

//### Alert Configuration
//Set up custom alerts for:
//- Device downtime
//- High CPU/memory usage
//- Bandwidth thresholds
//- Security events

//> 🔒 **Access restricted to IT staff only.** All actions are logged and audited.",
//                Features = new List<string>
//                {
//                    "Real-time network monitoring",
//                    "Device configuration management",
//                    "Bandwidth analytics",
//                    "Security event monitoring",
//                    "Automated alerting"
//                },
//                Contact = "netadmin@company.com",
//                RequiresPermission = true
//            }
//        };
//    }

//    public Task<List<SubsiteInfo>> GetAllSubsitesAsync()
//    {
//        return Task.FromResult(_subsites.ToList());
//    }

//    public Task<List<SubsiteInfo>> GetSubsitesByCategoryAsync(string category)
//    {
//        var filtered = _subsites.Where(s => s.Category == category).ToList();
//        return Task.FromResult(filtered);
//    }

//    public Task<SubsiteInfo?> GetSubsiteByIdAsync(string id)
//    {
//        var subsite = _subsites.FirstOrDefault(s => s.Id == id);
//        return Task.FromResult(subsite);
//    }

//    public Task<List<string>> GetCategoriesAsync()
//    {
//        var categories = _subsites
//            .Select(s => s.Category)
//            .Distinct()
//            .OrderBy(c => c)
//            .ToList();
//        return Task.FromResult(categories);
//    }
//}
