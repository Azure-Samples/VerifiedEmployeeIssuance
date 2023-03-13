using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Graph;
using MyAccountPage.Models;
using System.Net.Http;
using Microsoft.Identity.Web;

using static Microsoft.Graph.Constants;
using System.Net.Http.Headers;
using System.Net.Http;
using Microsoft.Identity.Client;
using System.Security.Cryptography.X509Certificates;
using static System.Net.WebRequestMethods;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using System.Data;

namespace MyAccountPage.Pages
{
    [Authorize(Policy = "alloweduser")]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}