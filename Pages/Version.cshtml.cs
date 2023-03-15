using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Graph;

namespace MyAccountPage.Pages
{
    public class VersionModel : PageModel
    {
        private readonly ILogger<VersionModel> _logger;
        private readonly IConfiguration _configuration;

        private string? _apiKey;

        public VersionModel(ILogger<VersionModel> logger,
                          IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;

        }
        public void OnGet()
        {
            
            ViewData["VerifiedEmployeeId:manifest"] = _configuration["VerifiedEmployeeId:manifest"];
            ViewData["VerifiedEmployeeId:Authority"] = _configuration["VerifiedEmployeeId:Authority"];
            ViewData["VerifiedIDService:ClientId"] = _configuration["VerifiedIDService:ClientId"];
            ViewData["VerifiedIDService:TenantId"] = _configuration["VerifiedIDService:TenantId"];

        }
    }
}
