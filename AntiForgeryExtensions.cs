﻿using Microsoft.AspNetCore.Antiforgery;

namespace SchoolHelpdesk;

public static class AntiForgeryExtensions
{
  public static TBuilder ValidateAntiforgery<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
  {
    return builder.AddEndpointFilter(async (context, next) =>
    {
      if (context.HttpContext.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
      {
        return await next(context);
      }

      try
      {
        var antiForgeryService = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        await antiForgeryService.ValidateRequestAsync(context.HttpContext);
      }
      catch (AntiforgeryValidationException)
      {
        return Results.BadRequest("Antiforgery token validation failed.");
      }

      return await next(context);
    });
  }
}
