using System.Net.Mime;
using CatchUpPlatform.API.News.Application.Internal.CommandServices;
using CatchUpPlatform.API.News.Application.Internal.QueryServices;
using CatchUpPlatform.API.News.Application.Services;
using CatchUpPlatform.API.News.Domain.Repositories;
using CatchUpPlatform.API.News.Infrastructure.Persistence.EntityFrameworkCore;
using CatchUpPlatform.API.News.Interfaces.Rest.Transform;
using CatchUpPlatform.API.Resources;
using CatchUpPlatform.API.Shared.Domain.Repositories;
using CatchUpPlatform.API.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using CatchUpPlatform.API.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using CatchUpPlatform.API.Shared.Interfaces.ASP.Configuration;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

var builder = WebApplication.CreateBuilder(args);

// Configure Lower Case URLs
builder.Services.AddRouting(options => options.LowercaseUrls = true);

// Localization Configuration
builder.Services.AddLocalization();

// Configure Kebab Case Naming Convention for Controllers
builder.Services.AddControllers(options => options.Conventions.Add(new KebabCaseRouteNamingConvention()))
    .AddDataAnnotationsLocalization(options => 
        options.DataAnnotationLocalizerProvider = (_, factory) => factory.Create(typeof(SharedResource)))
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var localizer = context.HttpContext.RequestServices
                .GetRequiredService<IStringLocalizer<SharedResource>>();
            var problemDetailsFactory = context.HttpContext.RequestServices
                .GetRequiredService<ProblemDetailsFactory>();
            var problemDetails = problemDetailsFactory.CreateValidationProblemDetails(
                context.HttpContext,
                context.ModelState,
                statusCode: StatusCodes.Status400BadRequest,
                title: localizer["InvalidModelStateTitle"].Value);

            var result = new BadRequestObjectResult(problemDetails);
            result.ContentTypes.Add(MediaTypeNames.Application.Json);
            return result;
        };
    });


// Register RFC 7807 ProblemDetails Payloads for centralized exception handling and error reporting
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.ProblemDetails.Status is null or >= 500)
        {
            var localizer = context.HttpContext.RequestServices
                .GetRequiredService<IStringLocalizer<SharedResource>>();
            context.ProblemDetails.Title = localizer["UnexpectedServerError"].Value;
            context.ProblemDetails.Detail = localizer["UnexpectedErrorProcessingRequest"].Value;
        }
    };
});

// Generate the OpenAPI document with ASP.NET Core's built-in support
builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer(LocalizedOpenApiAssembler.TransformOperationAsync);
    options.AddSchemaTransformer(LocalizedOpenApiAssembler.TransformSchemaAsync);
});

// Add Database Connection

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    
    var connectionStringTemplate = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionStringTemplate))
        throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    
    var connectionString = Environment.ExpandEnvironmentVariables(connectionStringTemplate);
    if (string.IsNullOrEmpty(connectionString))
        throw new InvalidOperationException("Connection string 'DefaultConnection' is empty after expanding environment variables.");

    options.UseMySQL(connectionString)
        .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
        .EnableDetailedErrors();

    if (builder.Environment.IsDevelopment())
        options.EnableSensitiveDataLogging();
}); 

// Configure Dependency Injection

// Shared bounded context injection configuration
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// News bounded context injection configuration
builder.Services.AddScoped<IFavoriteSourceRepository, FavoriteSourceRepository>();
builder.Services.AddScoped<IFavoriteSourceCommandService, FavoriteSourceCommandService>();
builder.Services.AddScoped<IFavoriteSourceQueryService, FavoriteSourceQueryService>();


var app = builder.Build();


// Localization Configuration
var supportedCultures = new[] { "en", "en-US", "es", "es-PE" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
localizationOptions.ApplyCurrentCultureToResponseHeaders = true;
app.UseRequestLocalization(localizationOptions);

app.Run();