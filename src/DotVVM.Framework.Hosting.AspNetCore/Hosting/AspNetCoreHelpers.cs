﻿using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Authentication;

namespace DotVVM.Framework.Hosting
{
    public static class AspNetCoreHelpers
    {
        public static HttpContext GetAspNetCoreContext(this IDotvvmRequestContext context)
        {
            var concreteContext = context.HttpContext as DotvvmHttpContext;

            if (concreteContext == null)
            {
                throw new NotSupportedException("This app must run on AspNetCore hosting.");
            }

            return concreteContext.OriginalContext;
        }

        public static AuthenticationManager GetAuthentication(this IDotvvmRequestContext context)
            => context.GetAspNetCoreContext().Authentication;
    }
}