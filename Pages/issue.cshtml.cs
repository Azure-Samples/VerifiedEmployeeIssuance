using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using MyAccountPage.Models;
using System.Net.Http;

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
using System.Security.Claims;
using System.Diagnostics;
using Microsoft.AspNetCore.Authentication;

namespace MyAccountPage.Pages
{
    [Authorize]
    public class IssueModel : PageModel
    {
        //private readonly GraphServiceClient _graphServiceClient;
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;

        private UserData? _userdata;

        public IssueModel(ILogger<IndexModel> logger,
                         //GraphServiceClient graphServiceClient,
                         IConfiguration configuration)
        {
            _logger = logger;
            //_graphServiceClient = graphServiceClient;
            _configuration = configuration;
        }

        public async Task<IActionResult> OnGet()
        {
            
            
            _userdata = new UserData();
            //var tokenValue = await HttpContext.GetTokenAsync("id_token");
            var claims = User.Claims;

            foreach(var claim in claims)
            {
                Debug.WriteLine(claim.Type + ":" + claim.Value);
            }

            _userdata.displayName = claims?.FirstOrDefault(x => x.Type.Equals("name", StringComparison.OrdinalIgnoreCase))?.Value; ;
            _userdata.surname = claims?.FirstOrDefault(x => x.Type.Equals("family_name", StringComparison.OrdinalIgnoreCase))?.Value;
            _userdata.givenName = claims?.FirstOrDefault(x => x.Type.Equals("given_Name", StringComparison.OrdinalIgnoreCase))?.Value;
            _userdata.jobtitle = claims?.FirstOrDefault(x => x.Type.Equals("title", StringComparison.OrdinalIgnoreCase))?.Value;
            _userdata.mail = claims?.FirstOrDefault(x => x.Type.Equals(ClaimTypes.Email, StringComparison.OrdinalIgnoreCase))?.Value;

            _userdata.preferredLanguage = claims?.FirstOrDefault(x => x.Type.Equals(ClaimTypes.Locality, StringComparison.OrdinalIgnoreCase))?.Value; ;
            _userdata.revocationId = _userdata.mail;
            _userdata.photo = _userdata.defaultUserPhoto;
            //try
            //{
            //    var photo = await _graphServiceClient.Me.Photo.Content
            //        .Request()
            //        .GetAsync();
            //    if (photo != null)
            //    {
            //        _userdata.photo = Convert.ToBase64String((photo as MemoryStream).ToArray());
            //    }
            //    else
            //    {
            //        _userdata.photo = _userdata.defaultUserPhoto;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, "Error getting photo");
            //    _userdata.photo = _userdata.defaultUserPhoto;
            //}

            //}
            //catch (ServiceException ex)
            //{
            //    _logger.LogError(ex, "Error getting user data:"+ ex.Message);
            //    return RedirectToPage("/Error");
            //}

            ViewData["GraphApiResult"] = _userdata;
            return Page();
        }
    }
}