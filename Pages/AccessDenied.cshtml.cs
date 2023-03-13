using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Graph;
using MyAccountPage.Models;

namespace MyAccountPage.Pages
{
    public class AccessDeniedModel : PageModel
    {

        private readonly ILogger<AccessDeniedModel> _logger;
        private readonly IConfiguration _configuration;
        [BindProperty(SupportsGet = true)]
        public string requiredUserrole { get; set; }

        public AccessDeniedModel(ILogger<AccessDeniedModel> logger,
                                 IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            requiredUserrole = String.Empty;
        }
        public void OnGet()
        {
            requiredUserrole = _configuration["AzureAd:AllowedUsersRole"];
        }
    }
}
