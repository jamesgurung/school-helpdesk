using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using SchoolHelpdesk;
using System.Security.Cryptography;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
  o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
  o.KnownNetworks.Clear();
  o.KnownProxies.Clear();
});

builder.Services.AddDataProtection().PersistKeysToAzureBlobStorage(new Uri(builder.Configuration["Azure:DataProtectionBlobUri"]));

var storageAccountName = builder.Configuration["Azure:StorageAccountName"];
var storageAccountKey = builder.Configuration["Azure:StorageAccountKey"];
var connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};EndpointSuffix=core.windows.net";

School.Instance = builder.Configuration.GetSection(nameof(School)).Get<School>();
TableService.Configure(connectionString);
BlobService.Configure(connectionString, storageAccountName, storageAccountKey);
EmailService.Configure(builder.Configuration["Postmark:ServerToken"], builder.Configuration["Postmark:InboundAuthKey"], School.Instance.DebugEmail);

await BlobService.LoadConfigAsync();
await TableService.LoadLatestTicketIdAsync();

builder.ConfigureAuth();
builder.Services.AddResponseCompression();
builder.Services.AddAntiforgery(options => { options.HeaderName = "X-XSRF-TOKEN"; });
builder.Services.Configure<RouteOptions>(options => { options.LowercaseUrls = true; });
builder.Services.Configure<JsonOptions>(options => { options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; });
builder.Services.AddRazorPages(options => { options.Conventions.AllowAnonymousToFolder("/auth"); });

var isProduction = !builder.Environment.IsDevelopment();

builder.Services.AddWebOptimizer(pipeline =>
{
  if (isProduction)
  {
    pipeline.MinifyCssFiles("css/*.css");
    pipeline.MinifyJsFiles("js/*.js");
    pipeline.AddJavaScriptBundle("js/site.js", "js/core.js", "js/date-utils.js", "js/utils.js", "js/api.js", "js/search.js", "js/conversation.js",
      "js/ticket-list.js", "js/ticket-details.js", "js/ticket-edit.js", "js/modal.js", "js/event-handlers.js");
  }
});

if (isProduction)
{
  builder.Services.AddHostedService<ReminderService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
  app.UseHsts();
  app.Use(async (context, next) =>
  {
    if (context.Request.Path.Value == "/" && context.Request.Headers.UserAgent.ToString().Equals("alwayson", StringComparison.OrdinalIgnoreCase))
    {
      await TableService.WarmUpAsync();
      context.Response.StatusCode = 200;
    }
    else if (!context.Request.Host.Host.Equals(School.Instance.AppWebsite, StringComparison.OrdinalIgnoreCase))
    {
      context.Response.Redirect($"https://{School.Instance.AppWebsite}{context.Request.Path.Value}{context.Request.QueryString}", true);
    }
    else
    {
      await next();
    }
  });

  app.Use(async (context, next) =>
  {
    var cspNonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    context.Items["csp-nonce"] = cspNonce;
    var csp = $"default-src 'self'; script-src 'self' 'nonce-{cspNonce}'; img-src 'self' https://{storageAccountName}.blob.core.windows.net; " +
      "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src https://fonts.gstatic.com; object-src 'none'; base-uri 'self'; " +
      "frame-ancestors 'none'; form-action 'self'; connect-src 'self'; upgrade-insecure-requests;";
    context.Response.Headers.ContentSecurityPolicy = csp;
    await next();
  });
}

app.UseForwardedHeaders();
app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseWebOptimizer();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorPages();
app.MapAuthPaths();
app.MapApiPaths();

await app.RunAsync();