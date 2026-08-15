using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using SchoolHelpdesk;
using System.Security.Cryptography;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var appConfigEndpoint = builder.Configuration["AppConfigurationEndpoint"];
var appConfigConnectionString = builder.Configuration.GetConnectionString("AppConfiguration");
if (appConfigEndpoint is not null || appConfigConnectionString is not null)
{
  builder.Configuration.AddAzureAppConfiguration(options =>
  {
    if (appConfigEndpoint is not null)
    {
      options.Connect(new Uri(appConfigEndpoint), new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned));
    }
    else
    {
      options.Connect(appConfigConnectionString);
    }
    options
      .Select("Shared:*")
      .Select("SchoolHelpdesk:*")
      .TrimKeyPrefix("Shared:")
      .TrimKeyPrefix("SchoolHelpdesk:");
  });
}

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
  o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
  o.KnownIPNetworks.Clear();
  o.KnownProxies.Clear();
});

builder.Services.AddDataProtection().PersistKeysToAzureBlobStorage(new Uri(builder.Configuration["DataProtectionBlobUri"]));

var storageAccountName = builder.Configuration["StorageAccountName"];
var storageAccountKey = builder.Configuration["StorageAccountKey"];
var connectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey};EndpointSuffix=core.windows.net";

School.Instance = new()
{
  Admins = builder.Configuration["Admins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
  AppWebsite = builder.Configuration["AppWebsite"],
  DebugEmail = builder.Configuration["DebugEmail"],
  Dispatchers = builder.Configuration["Dispatchers"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
  HelpdeskEmail = builder.Configuration["HelpdeskEmail"],
  Managers = builder.Configuration["Managers"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [],
  NotifyFirstManager = builder.Configuration.GetValue<bool>("NotifyFirstManager"),
  SchoolName = builder.Configuration["SchoolName"],
  SyncApiKey = builder.Configuration["SyncApiKey"]
};
BlobService.Configure(connectionString, storageAccountName, storageAccountKey);
BackupService.Configure(connectionString);
QueueService.Configure(connectionString);
TableService.Configure(connectionString);
EmailService.Configure(builder.Configuration["PostmarkServerToken"], builder.Configuration["PostmarkInboundAuthKey"], School.Instance.DebugEmail);
AIService.Configure(builder.Configuration["AIFoundryEndpoint"], builder.Configuration["AIFoundryDeployment"], builder.Configuration["AIFoundryApiKey"]);

await BlobService.LoadConfigAsync();
await TableService.HydrateCacheAsync();

builder.ConfigureAuth();
builder.Services.AddResponseCompression(options => { options.EnableForHttps = true; });
builder.Services.AddAntiforgery(options => { options.HeaderName = "X-XSRF-TOKEN"; });
builder.Services.Configure<RouteOptions>(options => { options.LowercaseUrls = true; });
builder.Services.Configure<JsonOptions>(options => { options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; });
builder.Services.AddRazorPages(options =>
{
  options.Conventions.AllowAnonymousToPage("/login");
  options.Conventions.AllowAnonymousToPage("/denied");
});

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
