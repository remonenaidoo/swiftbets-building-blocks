using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using SwiftBets.BuildingBlocks.Core;

namespace SwiftBets.BuildingBlocks.Web;

public static class WebRegistration
{
    public static IServiceCollection AddSwiftBetsWeb(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        return services;
    }

    /// <summary>RS256 bearer validation against the identity issuer's JWKS; issuer, audience and lifetime are all enforced.</summary>
    public static IServiceCollection AddSwiftBetsJwtBearer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<JwtOptions>(configuration, JwtOptions.SectionName);
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Authority = jwt.Authority;
            options.Audience = jwt.Audience;
            options.RequireHttpsMetadata = jwt.RequireHttpsMetadata;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ClockSkew = TimeSpan.FromSeconds(30),
                RoleClaimType = "role",
                NameClaimType = "sub",
            };
            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    await ErrorEnvelopes.WriteAsync(context.HttpContext, ErrorEnvelopes.Create(context.HttpContext, StatusCodes.Status401Unauthorized, "unauthenticated")).ConfigureAwait(false);
                },
                OnForbidden = context => ErrorEnvelopes.WriteAsync(context.HttpContext, ErrorEnvelopes.Create(context.HttpContext, StatusCodes.Status403Forbidden, "forbidden")),
            };
        });
        services.AddAuthorizationBuilder()
            .AddPolicy(Roles.Punter, p => p.RequireRole(Roles.Punter))
            .AddPolicy(Roles.Operator, p => p.RequireRole(Roles.Operator, Roles.Admin))
            .AddPolicy(Roles.Admin, p => p.RequireRole(Roles.Admin))
            .AddPolicy(Roles.Service, p => p.RequireRole(Roles.Service))
            .AddPolicy(Roles.OperatorOrService, p => p.RequireRole(Roles.Operator, Roles.Admin, Roles.Service));
        return services;
    }

    public static IServiceCollection AddClientCredentials(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<ClientCredentialsOptions>(configuration, ClientCredentialsOptions.SectionName);
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient(nameof(ClientCredentialsTokenProvider));
        services.AddSingleton(sp => new ClientCredentialsTokenProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ClientCredentialsTokenProvider)),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClientCredentialsOptions>>(),
            sp.GetRequiredService<TimeProvider>()));
        return services;
    }

    public static WebApplication UseSwiftBetsWeb(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseStatusCodePages(async context =>
        {
            var http = context.HttpContext;
            if (http.Response.ContentLength is null or 0 && string.IsNullOrEmpty(http.Response.ContentType))
            {
                var status = http.Response.StatusCode;
                await ErrorEnvelopes.WriteAsync(http, ErrorEnvelopes.Create(http, status, status == 404 ? "not_found" : "http_" + status)).ConfigureAwait(false);
            }
        });
        return app;
    }
}
