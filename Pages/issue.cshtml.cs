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

namespace MyAccountPage.Pages
{
    [Authorize]
    public class IssueModel : PageModel
    {
        private readonly GraphServiceClient _graphServiceClient;
        private readonly ILogger<IndexModel> _logger;
        private readonly IConfiguration _configuration;

        private UserData? _userdata;

        public IssueModel(ILogger<IndexModel> logger,
                         GraphServiceClient graphServiceClient,
                         IConfiguration configuration)
        {
            _logger = logger;
            _graphServiceClient = graphServiceClient;
            _configuration = configuration;
        }

        public async Task<IActionResult> OnGet()
        {
            _userdata = new UserData();
            try
            {
                var user = await _graphServiceClient.Me
                    .Request()
                    .Select("displayName,givenName,jobTitle,preferredLanguage,surname,mail,userPrincipalName")
                    .GetAsync();

                _userdata.displayName = user.DisplayName;
                _userdata.givenName = user.GivenName;
                _userdata.jobtitle = user.JobTitle;
                _userdata.preferredLanguage = user.PreferredLanguage;
                _userdata.surname = user.Surname;
                _userdata.mail = user.Mail;
                _userdata.revocationId = user.UserPrincipalName;

                try
                {
                    var photo = await _graphServiceClient.Me.Photo.Content
                        .Request()
                        .GetAsync();
                    if (photo != null)
                    {
                        _userdata.photo = Convert.ToBase64String((photo as MemoryStream).ToArray());
                    }
                    else
                    {
                        _userdata.photo = _userdata.defaultUserPhoto;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting photo");
                    _userdata.photo = _userdata.defaultUserPhoto;
                }

            }
            catch (ServiceException ex)
            {
                _logger.LogError(ex, "Error getting user data:"+ ex.Message);
                return RedirectToPage("/Error");
            }
            
            ViewData["GraphApiResult"] = _userdata;
            return Page();
        }
    }
}