using Portal.Web.Models;

namespace Portal.Web.Services;

/// <summary>
/// In-memory implementation of the subsite service
/// Replace with a database-backed implementation in production
/// </summary>
public class SubsiteService : ISubsiteService
{
    private readonly List<SubsiteInfo> _subsites;

    public string Name => "Test Portal";

    public string Description => string.Empty;

    public SubsiteService()
    {
        // Initialize with sample data
        // In production, this would load from a database or API
        _subsites = new List<SubsiteInfo>
        {
            new SubsiteInfo
            {
                Id = "1",
                Name = "Employee Portal",
                Description = "Access your personal information, benefits, and company policies",
                Url = "/employee",
                IconClass = "bi bi-person-circle",
                Category = "HR",
                TargetAudience = "All Employees",
                UsageInstructions = "Log in with your corporate credentials to access your employee profile, view pay stubs, update personal information, and review company policies. You can also submit time-off requests and view your benefits information.",
                Features = new List<string>
                {
                    "View and update personal information",
                    "Access pay stubs and tax documents",
                    "Submit time-off requests",
                    "Review benefits enrollment",
                    "Access company policies and procedures"
                },
                Contact = "hr@company.com",
                RequiresPermission = false
            },
            new SubsiteInfo
            {
                Id = "2",
                Name = "IT Service Desk",
                Description = "Submit IT tickets and track your requests",
                Url = "/servicedesk",
                IconClass = "bi bi-headset",
                Category = "IT",
                TargetAudience = "All Employees",
                UsageInstructions = "Submit support tickets for any IT-related issues including hardware problems, software requests, access issues, or general questions. Track the status of your requests and communicate with IT staff through the ticketing system.",
                Features = new List<string>
                {
                    "Submit and track support tickets",
                    "Request software installations",
                    "Report hardware issues",
                    "Browse knowledge base articles",
                    "Live chat with IT support"
                },
                Contact = "helpdesk@company.com",
                RequiresPermission = false
            },
            new SubsiteInfo
            {
                Id = "3",
                Name = "Document Management",
                Description = "Access and share company documents securely",
                Url = "/documents",
                IconClass = "bi bi-file-earmark-text",
                Category = "General",
                TargetAudience = "All Employees",
                UsageInstructions = "Store, organize, and share documents with your team. Use folders to organize content, set permissions for sensitive documents, and collaborate on files in real-time. All documents are automatically versioned and backed up.",
                Features = new List<string>
                {
                    "Secure document storage",
                    "Version control and history",
                    "Collaborative editing",
                    "Advanced search capabilities",
                    "Custom folder permissions"
                },
                Contact = "support@company.com",
                RequiresPermission = false
            },
            new SubsiteInfo
            {
                Id = "4",
                Name = "Financial Dashboard",
                Description = "View budgets, expense reports, and financial analytics",
                Url = "/finance",
                IconClass = "bi bi-graph-up",
                Category = "Finance",
                TargetAudience = "Finance Team, Department Heads",
                UsageInstructions = "Access real-time financial data, submit expense reports, view budget allocations, and generate financial reports. Department heads can monitor their department budgets and approve expenditures.",
                Features = new List<string>
                {
                    "Real-time budget tracking",
                    "Expense report submission",
                    "Financial analytics and reporting",
                    "Purchase order management",
                    "Invoice tracking"
                },
                Contact = "finance@company.com",
                RequiresPermission = true
            },
            new SubsiteInfo
            {
                Id = "5",
                Name = "Learning Center",
                Description = "Access training materials and professional development courses",
                Url = "/learning",
                IconClass = "bi bi-book",
                Category = "HR",
                TargetAudience = "All Employees",
                UsageInstructions = "Browse and enroll in training courses, complete required compliance training, and track your professional development progress. Courses include on-demand videos, interactive modules, and downloadable resources.",
                Features = new List<string>
                {
                    "Self-paced online courses",
                    "Compliance training tracking",
                    "Certification programs",
                    "Skills assessments",
                    "Learning paths by role"
                },
                Contact = "training@company.com",
                RequiresPermission = false
            },
            new SubsiteInfo
            {
                Id = "6",
                Name = "Network Tools",
                Description = "Network monitoring, configuration, and diagnostic tools",
                Url = "/network",
                IconClass = "bi bi-diagram-3",
                Category = "IT",
                TargetAudience = "IT Department",
                UsageInstructions = "Access network monitoring dashboards, configure network devices, run diagnostic tools, and manage network security settings. This portal is restricted to IT staff with appropriate credentials.",
                Features = new List<string>
                {
                    "Real-time network monitoring",
                    "Device configuration management",
                    "Bandwidth analytics",
                    "Security event monitoring",
                    "Automated alerting"
                },
                Contact = "netadmin@company.com",
                RequiresPermission = true
            }
        };
    }

    public Task<List<SubsiteInfo>> GetAllSubsitesAsync()
    {
        return Task.FromResult(_subsites.ToList());
    }

    public Task<List<SubsiteInfo>> GetSubsitesByCategoryAsync(string category)
    {
        var filtered = _subsites.Where(s => s.Category == category).ToList();
        return Task.FromResult(filtered);
    }

    public Task<SubsiteInfo?> GetSubsiteByIdAsync(string id)
    {
        var subsite = _subsites.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(subsite);
    }

    public Task<List<string>> GetCategoriesAsync()
    {
        var categories = _subsites
            .Select(s => s.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
        return Task.FromResult(categories);
    }
}
