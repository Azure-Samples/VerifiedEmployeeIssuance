using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Cookies;
using MyAccountPage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var initialScopes = builder.Configuration["MicrosoftGraph:Scopes"]?.Split(' ');

// Add services to the container.
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
    .AddMicrosoftGraph(builder.Configuration.GetSection("MicrosoftGraph"))
    .AddInMemoryTokenCaches(); //we might need to change this to scale the app

builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options => options.Events = new RejectSessionCookieWhenAccountNotInCacheEvents());
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options => options.AccessDeniedPath = "/AccessDenied");

builder.Services.AddAuthorization(options =>
{
    // By default, all incoming requests will be authorized according to the default policy.
    options.FallbackPolicy = options.DefaultPolicy;

});

//if the access to the webapp needs to be limited to a specific role, set the role in the appsettings.json
//if the role is not set, the webapp will be open to all authenticated users
//this allows you to show a friendly access denied message with optional instructions for your users
//how to get access if they want to or if they can
//this access policy is set on the index.html and on the controller through  [Authorize(Policy = "alloweduser")] attribute
var requireUserRoleforAccess = builder.Configuration["AzureAd:AllowedUsersRole"];
if (!String.IsNullOrEmpty(requireUserRoleforAccess))
{
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("alloweduser", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(requireUserRoleforAccess);
        });
    });
}
else
{
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("alloweduser", policy =>
        {
            policy.RequireAuthenticatedUser();
        });
    });
}



builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(1);//You can set Time   
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
});

builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

builder.Services.AddHttpClient(); // use iHttpFactory as best practice, should be easy to use extra retry and hold off policies in the future


var app = builder.Build();

//this setting is used when you use tools like ngrok or reverse proxies like nginx which connect to http://localhost
//if you don't set this setting the sign-in redirect will be http instead of https
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});



//// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHsts();
    app.UseExceptionHandler("/Error");
}

app.UseSession();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseCookiePolicy(new CookiePolicyOptions
{
    Secure = CookieSecurePolicy.Always
});


app.UseEndpoints(endpoints =>
{
    endpoints.MapRazorPages();  // If Razor pages
    endpoints.MapControllers(); // Needs to be added
});

// generate an api-key on startup that we can use to validate callbacks
System.Environment.SetEnvironmentVariable("API-KEY", Guid.NewGuid().ToString());

app.Run();

