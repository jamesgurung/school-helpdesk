using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using SchoolHelpdesk;
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
TableService.Configure(connectionString);
BlobService.Configure(connectionString, storageAccountName, storageAccountKey);

await BlobService.LoadConfigAsync();

builder.ConfigureAuth();
builder.Services.AddResponseCompression(options => { options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["text/javascript"]); });
builder.Services.AddAntiforgery(options => { options.HeaderName = "X-XSRF-TOKEN"; });
builder.Services.Configure<RouteOptions>(options => { options.LowercaseUrls = true; });
builder.Services.Configure<JsonOptions>(options => { options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; });
builder.Services.AddRazorPages(options => { options.Conventions.AllowAnonymousToFolder("/auth"); });

School.Instance = builder.Configuration.GetSection(nameof(School)).Get<School>();

var minify = !builder.Environment.IsDevelopment();
builder.Services.AddWebOptimizer(pipeline =>
{
  if (minify)
  {
    pipeline.MinifyCssFiles("css/*.css");
    pipeline.MinifyJsFiles("js/*.js");
  }
});

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