using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using MyAccountPage.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Azure.Core;
using Azure.Identity;

namespace MyAccountPage
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class IssuerController : ControllerBase
    {
        const string ISSUANCEPAYLOAD = "issuance_request_config.json";

        private readonly GraphServiceClient _graphServiceClient;
        private readonly IConfiguration _configuration;
        protected readonly IHttpClientFactory _httpClientFactory;
        protected IMemoryCache _cache;
        protected readonly ILogger<IssuerController> _log;

        private string _apiKey;
        private UserData? _userdata;

        public IssuerController(ILogger<IssuerController> logger,
                                GraphServiceClient graphServiceClient,
                                IConfiguration configuration,
                                IHttpClientFactory httpClientFactory,
                                IMemoryCache memoryCache)
        {
            _log = logger;
            _graphServiceClient = graphServiceClient;
            _configuration = configuration;
            _apiKey = System.Environment.GetEnvironmentVariable("API-KEY");
            _httpClientFactory = httpClientFactory;
            _cache = memoryCache;
        }

        /// <summary>
        /// This method is called from the UI to initiate the issuance of the verifiable credential
        /// </summary>
        /// <returns>JSON object with the address to the presentation request and optionally a QR code and a state value which can be used to check on the response status</returns>
        [Authorize(Policy = "alloweduser")]
        [HttpGet("/api/issuer/issuance-request")]
        public async Task<ActionResult> IssuanceRequest()
        {
            //retrieve information from the user to be able to create the payload for the idtokenhint issuance request
            _userdata = new UserData();
            Microsoft.Graph.User user;
            try
            {
                user = await _graphServiceClient.Me
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
                    var photo = await _graphServiceClient.Me.Photos["648x648"].Content
                        .Request()
                        .GetAsync();
                    if (photo != null)
                    {
                        var photoArray = (photo as MemoryStream).ToArray();
                        var encoded = Base64UrlEncoder.Encode(photoArray);
                        _userdata.photo = encoded;
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Error getting photo");
                }

            }
            catch (ServiceException ex)
            {
                return BadRequest(new { error = "400", error_description = "Something went wrong retrieving the user profile: " + ex.Message });
            }

            try
            {
                //they payload template is loaded from disk and modified in the code below to make it easier to get started
                //and having all config in a central location appsettings.json. 
                //if you want to manually change the payload in the json file make sure you comment out the code below which will modify it automatically
                //
                string jsonString = "";

                string payloadpath = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), ISSUANCEPAYLOAD);
                _log.LogTrace("IssuanceRequest file: {0}", payloadpath);
                if (!System.IO.File.Exists(payloadpath))
                {
                    _log.LogError("File not found: {0}", payloadpath);
                    return BadRequest(new { error = "400", error_description = ISSUANCEPAYLOAD + " not found" });
                }
                jsonString = System.IO.File.ReadAllText(payloadpath);
                if (string.IsNullOrEmpty(jsonString))
                {
                    _log.LogError("Error reading file: {0}", payloadpath);
                    return BadRequest(new { error = "400", error_description = ISSUANCEPAYLOAD + " error reading file" });
                }
                JObject payload = JObject.Parse(jsonString);
                if (payload == null)
                {
                    _log.LogError("Error parsing jsonstring: {0}", jsonString);
                    return BadRequest(new { error = "400", error_description = ISSUANCEPAYLOAD + " error parsing payload" });
                }


                string state = Guid.NewGuid().ToString();

                //modify payload with new state, the state is used to be able to update the UI when callbacks are received from the VC Service
                if (payload["callback"]["state"] != null)
                {
                    payload["callback"]["state"] = state;
                }

                //modify the callback method to make it easier to debug 
                //with tools like ngrok since the URI changes all the time
                //this way you don't need to modify the callback URL in the payload every time
                //ngrok changes the URI

                if (payload["callback"]["url"] != null)
                {
                    //localhost hostname can't work for callbacks, if you are using localhost the callback
                    //in the payload will not be overwritten. Make sure you update the issuance_request-config.json
                    //with the correct callback URL to test this on your dev machine
                    //this happens for example when testing with sign-in to an IDP and https://localhost:5000/ is used as redirect URI
                    string host = GetRequestHostName();
                    if (!host.Contains("//localhost"))
                    {
                        payload["callback"]["url"] = String.Format("{0}/api/issuer/issuanceCallback", host);
                    }
                }

                // set our api-key in the request so we can check it in the callbacks we receive
                payload["callback"]["headers"]["api-key"] = this._apiKey;

                //get the manifest from the appsettings, this is the URL to the Verified Employee credential created in the azure portal. 
                //the ? parameter is needed for the myaccount page to work with the Verified Employee credential. This will force the system
                //to use accept an idtokenhint payload and ignore the accesstoken flow which is the default for employment credentials
                payload["manifest"] = _configuration["VerifiedEmployeeId:manifest"] + "?manifestType=claimInjection";

                //get the IssuerDID from the appsettings
                payload["authority"] = _configuration["VerifiedEmployeeId:authority"];

                //update the payload with user profile information
                JObject claims = (JObject)payload["claims"];

                //displayname is mandatory
                if (!String.IsNullOrEmpty(_userdata.displayName)) { payload["claims"]["displayName"] = _userdata.displayName; }
                else
                {
                    _log.LogError(String.Format("Mandatory profile data missing: displayName"));
                    return BadRequest(new { error = "missing profile data", error_description = "displayname is mandatory data for this Verifiable Credential" });
                }

                //revocationId is mandatory
                //for backwards compatibility the userPrincipalName claim is used to pass the revocationId
                //the value in this claim will be copied to the revocationId which can be used
                //to search for the credentials to be able to revoke them
                //there is no userPrincipalName claim stored in the VC
                if (!String.IsNullOrEmpty(_userdata.revocationId))
                {
                    payload["claims"]["userPrincipalName"] = _userdata.revocationId;
                }
                else
                {
                    _log.LogError(String.Format("Mandatory profile data missing: userprincipalname"));
                    return BadRequest(new { error = "missing profile data", error_description = "userprincipalname is mandatory data for this Verifiable Credential" });
                }

                //check if the property of _userdata is empty. only add the values to the payload if the content isnt null or empty
                //the issuance API doesn't accept empty or null value for claims.
                if (!String.IsNullOrEmpty(_userdata.givenName)) payload["claims"]["givenName"] = _userdata.givenName;
                if (!String.IsNullOrEmpty(_userdata.surname)) payload["claims"]["surname"] = _userdata.surname;
                if (!String.IsNullOrEmpty(_userdata.mail)) payload["claims"]["mail"] = _userdata.mail;
                if (!String.IsNullOrEmpty(_userdata.jobtitle)) payload["claims"]["jobTitle"] = _userdata.jobtitle;
                if (!String.IsNullOrEmpty(_userdata.preferredLanguage)) payload["claims"]["preferredLanguage"] = _userdata.preferredLanguage;
                if (!String.IsNullOrEmpty(_userdata.photo)) payload["claims"]["photo"] = _userdata.photo;

                jsonString = JsonConvert.SerializeObject(payload);

                //CALL REST API WITH PAYLOAD
                HttpStatusCode statusCode = HttpStatusCode.OK;
                string response = null;

                try
                {
                    //The VC Request API is an authenticated API. We need to clientid and secret (or certificate) to create an access token which 
                    //needs to be send as bearer to the VC Request API
                    var accessToken = await GetAccessToken();
                    if (accessToken.Item1 == String.Empty)
                    {
                        _log.LogError(String.Format("failed to acquire accesstoken: "+ accessToken.error + ":" + accessToken.error_description));
                        return BadRequest(new { error = accessToken.error, error_description = accessToken.error_description });
                    }


                    HttpClient client = new HttpClient();
                    var defaultRequestHeaders = client.DefaultRequestHeaders;
                    defaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.token);

                    HttpResponseMessage res = await client.PostAsync(_configuration["VerifiedIDService:Endpoint"] + "verifiableCredentials/createIssuanceRequest", new StringContent(jsonString, Encoding.UTF8, "application/json"));
                    response = await res.Content.ReadAsStringAsync();
                    client.Dispose();
                    statusCode = res.StatusCode;

                    if (statusCode == HttpStatusCode.Created)
                    {
                        _log.LogDebug("succesfully called Request API");
                        JObject requestConfig = JObject.Parse(response);
                        requestConfig.Add(new JProperty("id", state));
                        jsonString = JsonConvert.SerializeObject(requestConfig);

                        //We use in memory cache to keep state about the request. The UI will check the state when calling the presentationResponse method

                        var cacheData = new
                        {
                            status = "notscanned",
                            message = "Request ready, please scan with Authenticator",
                            expiry = requestConfig["expiry"].ToString()
                        };
                        _cache.Set(state, JsonConvert.SerializeObject(cacheData));

                        return new ContentResult { ContentType = "application/json", Content = jsonString };
                    }
                    else
                    {
                        _log.LogError("Unsuccesfully called Request API: " + response);
                        return BadRequest(new { error = statusCode, error_description = "Something went wrong calling the API: " + response });
                    }

                }
                catch (Exception ex)
                {
                    return BadRequest(new { error = "400", error_description = "Something went wrong calling the API: " + ex.Message });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "400", error_description = ex.Message });
            }
        }

        /// <summary>
        /// This method is called by the VC Request API when the user scans a QR code and accepts the issued Verifiable Credential
        /// </summary>
        /// <returns></returns>
        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult> IssuanceCallback()
        {
            try
            {
                string content = await new System.IO.StreamReader(this.Request.Body).ReadToEndAsync();
                _log.LogTrace("callback!: " + content);
                this.Request.Headers.TryGetValue("api-key", out var apiKey);
                if (!string.Equals(this._apiKey, apiKey))
                {
                    _log.LogTrace("api-key wrong or missing");
                    return new ContentResult() { StatusCode = (int)HttpStatusCode.Unauthorized, Content = "api-key wrong or missing" };
                }
                JObject issuanceResponse = JObject.Parse(content);
                var state = issuanceResponse["state"].ToString();

                //there are 2 different callbacks. 1 if the QR code is scanned (or deeplink has been followed)
                //Scanning the QR code makes Authenticator download the specific request from the server
                //the request will be deleted from the server immediately.
                //That's why it is so important to capture this callback and relay this to the UI so the UI can hide
                //the QR code to prevent the user from scanning it twice (resulting in an error since the request is already deleted)
                if (issuanceResponse["requestStatus"].ToString() == "request_retrieved")
                {
                    var cacheData = new
                    {
                        status = "request_retrieved",
                        message = "QR Code is scanned. Waiting for issuance...",
                    };
                    _cache.Set(state, JsonConvert.SerializeObject(cacheData));
                }

                //
                //This callback is called when issuance is completed.
                //
                if (issuanceResponse["requestStatus"].ToString() == "issuance_successful")
                {
                    var cacheData = new
                    {
                        status = "issuance_successful",
                        message = "Credential successfully issued",
                    };
                    _cache.Set(state, JsonConvert.SerializeObject(cacheData));
                }
                //
                //We capture if something goes wrong during issuance. See documentation with the different error codes
                //
                if (issuanceResponse["requestStatus"].ToString() == "issuance_error")
                {
                    var cacheData = new
                    {
                        status = "issuance_error",
                        payload = issuanceResponse["error"]["code"].ToString(),
                        //at the moment there isn't a specific error for incorrect entry of a pincode.
                        //So assume this error happens when the users entered the incorrect pincode and ask to try again.
                        message = issuanceResponse["error"]["message"].ToString()

                    };
                    _cache.Set(state, JsonConvert.SerializeObject(cacheData));
                }

                return new OkResult();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "400", error_description = ex.Message });
            }
        }

        //
        //this function is called from the UI polling for a response from the AAD VC Service.
        //when a callback is recieved at the issuanceCallback service the session will be updated
        //this method will respond with the status so the UI can reflect if the QR code was scanned and with the result of the issuance process
        //
        [HttpGet("/api/issuer/issuance-response")]
        public ActionResult IssuanceResponse()
        {
            try
            {
                //the id is the state value initially created when the issuanc request was requested from the request API
                //the in-memory database uses this as key to get and store the state of the process so the UI can be updated
                string state = this.Request.Query["id"];
                if (string.IsNullOrEmpty(state))
                {
                    return BadRequest(new { error = "400", error_description = "Missing argument 'id'" });
                }
                JObject value = null;
                if (_cache.TryGetValue(state, out string buf))
                {
                    value = JObject.Parse(buf);

                    Debug.WriteLine("check if there was a response yet: " + value);
                    return new ContentResult { ContentType = "application/json", Content = JsonConvert.SerializeObject(value) };
                }

                return new OkResult();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "400", error_description = ex.Message });
            }
        }

        //some helper functions
        protected async Task<(string token, string error, string error_description)> GetAccessToken()
        {
            string _clientid;
            string _clientsecret;
            string _tenantid;
            string? _certificatename;
            string _authority;

            // You can run this app using ClientSecret or Certificate. The code will differ only when instantiating the IConfidentialClientApplication
            bool isUsingClientSecret = false;
            bool isUsingManagedIdentity = false;
            bool isUsingDifferentAccountforVCService = false;

            //check if we need to use a different account for the VC service or reuse the clientid and secret/cert from the webapp

            //figure out how the VC service access token needs to be retrieved, based on the configuration
            //secret, cert, managed identity
            switch (_configuration["VerifiedIDService:SourceType"])
            {
                case "Secret":
                    {
                        isUsingClientSecret = true;
                        isUsingDifferentAccountforVCService = true;
                        break;
                    }
                case "SignedAssertionFromManagedIdentity":
                    {
                        isUsingManagedIdentity = true;
                        isUsingDifferentAccountforVCService = true;
                        break;
                    }
                default:
                    {
                        isUsingDifferentAccountforVCService = false;
                        break;
                    }
            }

            if (isUsingDifferentAccountforVCService)
            {
                _log.LogInformation("Using different account for VC Service");
                if (isUsingManagedIdentity)
                {
                    _log.LogInformation("Using Managed Identity to get access token for VC Service");
                    var at = GetManagedIdentityAccessToken();
                    _log.LogInformation("Managed Identity access token retrieved");
                    return (at.Result);
                    //return (at.Result.Item1.ToString(), String.Empty, String.Empty);
                }

                _clientid = _configuration["VerifiedIDService:ClientId"];
                _clientsecret = _configuration["VerifiedIDService:ClientSecret"];
                _tenantid = _configuration["VerifiedIDService:TenantId"];
                _certificatename = _configuration["VerifiedIDService:CertificateName"];
            }
            else
            {
                _log.LogInformation("Reusing settings from webapp clientid");
                _clientid = _configuration["AzureAd:ClientId"];
                _clientsecret = _configuration["AzureAd:ClientSecret"];
                if(!string.IsNullOrEmpty(_clientsecret)) isUsingClientSecret = true;
                _tenantid = _configuration["AzureAd:TenantId"];
                _certificatename = _configuration["AzureAd:CertificateName"];
            }
            _authority = String.Format(CultureInfo.InvariantCulture, "https://login.microsoftonline.com/{0}", _tenantid);


            // Since we are using application permissions this will be a confidential client application
            IConfidentialClientApplication app;
            if (isUsingClientSecret)
            {
                app = ConfidentialClientApplicationBuilder.Create(_clientid)
                    .WithClientSecret(_clientsecret)
                    .WithAuthority(new Uri(_authority))
                    .Build();
            }
            else
            {
                X509Certificate2 certificate = ReadCertificate(_certificatename);
                app = ConfidentialClientApplicationBuilder.Create(_clientid)
                    .WithCertificate(certificate)
                    .WithAuthority(new Uri(_authority))
                    .Build();
            }

            //configure in memory cache for the access tokens. The tokens are typically valid for 60 seconds,
            //so no need to create new ones for every web request
            app.AddDistributedTokenCache(services =>
            {
                services.AddDistributedMemoryCache();
                services.AddLogging(configure => configure.AddConsole())
                .Configure<LoggerFilterOptions>(options => options.MinLevel = Microsoft.Extensions.Logging.LogLevel.Debug);
            });

            // With client credentials flows the scopes is ALWAYS of the shape "resource/.default", as the 
            // application permissions need to be set statically (in the portal or by PowerShell), and then granted by
            // a tenant administrator. 
            string[] scopes = new string[] { _configuration["VerifiedIDService:VCServiceScope"] };

            AuthenticationResult result = null;
            try
            {
                result = await app.AcquireTokenForClient(scopes)
                    .ExecuteAsync();
            }
            catch (MsalServiceException ex) when (ex.Message.Contains("AADSTS70011"))
            {
                // Invalid scope. The scope has to be of the form "https://resourceurl/.default"
                // Mitigation: change the scope to be as expected
                _log.LogError("Failed!: Scope provided is not supported");
                return (string.Empty, "500", "Scope provided is not supported");
            }
            catch (MsalServiceException ex)
            {
                // general error getting an access token
                _log.LogError("Failed!:" + ex.Message);
                return (String.Empty, "500", "Something went wrong getting an access token for the client API:" + ex.Message);
            }

            return (result.AccessToken, String.Empty, String.Empty);
        }

        private async Task<(string token, string error, string error_description)> GetManagedIdentityAccessToken()
        {
            _log.LogInformation("trying to get new accesstoken through MSI");
            var credential = new ChainedTokenCredential(
                new ManagedIdentityCredential(),
                new EnvironmentCredential());
            try
            {
                var token = credential.GetToken(
                new Azure.Core.TokenRequestContext(
                    new[] { "3db474b9-6a0c-4840-96ac-1fceb342124f/.default" }));
                var accessToken = token.Token;
                _log.LogInformation("Getting Access token from MSI succes");
                return (accessToken, String.Empty, String.Empty);
            }
            catch (Exception ex)
            {
                _log.LogError("Failed!:"+ ex.Message);
                return (String.Empty, "500", "Something went wrong getting an access token for the client API:" + ex.Message);
            }
        }
        protected string GetRequestHostName()
        {
            string scheme = "https";// : this.Request.Scheme;
            string originalHost = this.Request.Headers["x-original-host"];
            string hostname = "";
            if (!string.IsNullOrEmpty(originalHost))
                hostname = string.Format("{0}://{1}", scheme, originalHost);
            else hostname = string.Format("{0}://{1}", scheme, this.Request.Host);
            return hostname;
        }
        public X509Certificate2 ReadCertificate(string certificateName)
        {
            if (string.IsNullOrWhiteSpace(certificateName))
            {
                throw new ArgumentException("certificateName should not be empty. Please set the CertificateName setting in the appsettings.json", "certificateName");
            }
            CertificateDescription certificateDescription = CertificateDescription.FromStoreWithDistinguishedName(certificateName);
            DefaultCertificateLoader defaultCertificateLoader = new DefaultCertificateLoader();
            defaultCertificateLoader.LoadIfNeeded(certificateDescription);
            return certificateDescription.Certificate;
        }

    }
}
