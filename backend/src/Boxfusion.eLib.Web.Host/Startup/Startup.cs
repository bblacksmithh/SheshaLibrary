using Abp.AspNetCore;
using Abp.AspNetCore.SignalR.Hubs;
using Abp.Castle.Logging.Log4Net;
using Abp.Extensions;
using Abp.PlugIns;
using Boxfusion.eLib.Web.Host.Startup;
using Castle.Facilities.Logging;
using ElmahCore;
using ElmahCore.Mvc;
using GraphQL;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Boxfusion.eLib.Hangfire;
using Boxfusion.eLib.Configuration;
using Shesha.Authorization;
using Shesha.Configuration;
using Shesha.DynamicEntities;
using Shesha.DynamicEntities.Swagger;
using Shesha.Extensions;
using Shesha.GraphQL;
using Shesha.GraphQL.Middleware;
using Shesha.Exceptions;
using System;
using System.IO;
using Shesha.Identity;
using Shesha.Web;
using Shesha.Scheduler.Extensions;
using Shesha.Swagger;
using System.Reflection;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.Swagger;

namespace Boxfusion.eLib.Web.Host.Startup
{
	public class Startup
	{
		private readonly IConfigurationRoot _appConfiguration;
		private readonly IWebHostEnvironment _hostEnvironment;

		public Startup(IWebHostEnvironment hostEnvironment, IHostingEnvironment env)
		{
			_appConfiguration = env.GetAppConfiguration();
			_hostEnvironment = hostEnvironment;
		}

		public IServiceProvider ConfigureServices(IServiceCollection services)
		{
			services.Configure<IISServerOptions>(options =>
			{
				options.AllowSynchronousIO = true;
			});

			services.AddElmah<XmlFileErrorLog>(options =>
			{
				options.Path = @"elmah";
				options.LogPath = Path.Combine(_hostEnvironment.ContentRootPath, "App_Data", "ElmahLogs");
				//options.CheckPermissionAction = context => context.User.Identity.IsAuthenticated; //note: looks like we have to use cookies for it
				options.Filters.Add(new ElmahFilter());
			});

			services.AddMvcCore(options =>
			{
				options.EnableEndpointRouting = false;
				options.Conventions.Add(new Shesha.Swagger.ApiExplorerGroupPerControllerConvention());

				options.EnableDynamicDtoBinding();
				options.AddDynamicAppServices(services);

                    options.Filters.AddService(typeof(SheshaAuthorizationFilter));
                    options.Filters.AddService(typeof(SheshaExceptionFilter), order: 1);
                })
                .AddApiExplorer()
                .AddNewtonsoftJson(options =>
                {
                    options.UseCamelCasing(true);
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_3_0);

			IdentityRegistrar.Register(services);
			AuthConfigurer.Configure(services, _appConfiguration);

			services.AddSignalR();

			services.AddCors();
			/*
            // Configure CORS for angular2 UI
            services.AddCors(
                options => options.AddPolicy(
                    _defaultCorsPolicyName,
                    builder => builder
                        .WithOrigins(
                            // App:CorsOrigins in appsettings.json can contain more than one address separated by comma.
                            _appConfiguration["App:CorsOrigins"]
                                .Split(",", StringSplitOptions.RemoveEmptyEntries)
                                .Select(o => o.RemovePostFix("/"))
                                .ToArray()
                        )
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials()
                )
            );
            */
			AddApiVersioning(services);

			services.AddHttpContextAccessor();

			services.AddHangfire(config =>
			{
				config.UseSqlServerStorage(_appConfiguration.GetConnectionString("Default"));
			});

			// add Shesha GraphQL
			services.AddSheshaGraphQL();

			// Add ABP and initialize 
			// Configure Abp and Dependency Injection
			return services.AddAbp<SheshaWebHostModule>(
				options =>
				{
					// Configure Log4Net logging
					options.IocManager.IocContainer.AddFacility<LoggingFacility>(f => f.UseAbpLog4Net().WithConfig("log4net.config"));

					// configure plugins
					var pluginsFolder = Path.Combine(_hostEnvironment.ContentRootPath, "Plugins");
					if (!Directory.Exists(pluginsFolder))
						Directory.CreateDirectory(pluginsFolder);
					options.PlugInSources.AddFolder(Path.Combine(_hostEnvironment.ContentRootPath, "Plugins"), SearchOption.AllDirectories);
				}
			);
		}

		public void Configure(IApplicationBuilder app, IBackgroundJobClient backgroundJobs)
		{
			app.UseElmah();

			// note: already registered in the ABP
			AppContextHelper.Configure(app.ApplicationServices.GetRequiredService<IHttpContextAccessor>());


			app.UseConfigurationFramework();

			app.UseAbp(options =>
			{
				options.UseAbpRequestLocalization = false;
			}); // Initializes ABP framework.​
				//app.UseCors(_defaultCorsPolicyName); // Enable CORS!
				// global cors policy
			app.UseCors(x => x
				.AllowAnyMethod()
				.AllowAnyHeader()
				.SetIsOriginAllowed(origin => true) // allow any origin
				.AllowCredentials()); // allow credentials​
			app.UseStaticFiles();

			app.UseAuthentication();

			app.UseAbpRequestLocalization();

			app.UseRouting();

			app.UseAuthorization();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllerRoute(
					name: "defaultWithArea",
					pattern: "{area}/{controller=Home}/{action=Index}/{id?}");
				endpoints.MapControllerRoute(
					name: "default",
					pattern: "{controller=Home}/{action=Index}/{id?}");
				endpoints.MapHub<AbpCommonHub>("/signalr");
				endpoints.MapControllers();
				endpoints.MapSignalRHubs();
			});

			// Enable middleware to serve generated Swagger as a JSON endpoint
			app.UseSwagger();

			// Enable middleware to serve swagger-ui assets (HTML, JS, CSS etc.)
			app.UseSwaggerUI(options =>
			{
				options.AddEndpointsPerService();
				//options.SwaggerEndpoint("swagger/v1/swagger.json", "Shesha API V1");​
				// todo: add documents per module with summary about `service:xxx` endpoints
				//options.SwaggerEndpoint(baseUrl + "swagger/service:Meter/swagger.json", "Meter API");​
				options.IndexStream = () => Assembly.GetExecutingAssembly()
					.GetManifestResourceStream("Boxfusion.eLib.Web.Host.wwwroot.swagger.ui.index.html");
			}); // URL: /swagger​
			var options = new BackgroundJobServerOptions
			{
				//Queues = new[] { "alpha", "beta", "default" }
			};
			app.UseHangfireServer(options);
			app.UseHangfireDashboard("/hangfire",
				new DashboardOptions
				{
					Authorization = new[] { new HangfireAuthorizationFilter() }
				});
			app.UseMiddleware<GraphQLMiddleware>();
			app.UseGraphQLPlayground(); //to explorer API navigate https://*DOMAIN*/ui/playground
		}

		private void AddApiVersioning(IServiceCollection services)
		{
			services.Replace(ServiceDescriptor.Singleton<IApiControllerSpecification, AbpAppServiceApiVersionSpecification>());
			services.Configure<OpenApiInfo>(_appConfiguration.GetSection(nameof(OpenApiInfo)));

			services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

			//Swagger - Enable this line and the related lines in Configure method to enable swagger UI
			services.AddSwaggerGen(options =>
			{
				options.DescribeAllParametersInCamelCase();
				options.IgnoreObsoleteActions();
				options.AddXmlDocuments();

                options.SchemaFilter<DynamicDtoSchemaFilter>();
                options.OperationFilter<SwaggerOperationFilter>();

				options.CustomSchemaIds(type => SwaggerHelper.GetSchemaId(type));

				options.CustomOperationIds(desc => desc.ActionDescriptor is ControllerActionDescriptor d
					? d.ControllerName.ToCamelCase() + d.ActionName.ToPascalCase()
					: null);

				options.AddDocumentsPerService();

				// Define the BearerAuth scheme that's in use
				options.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme()
				{
					Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
					Name = "Authorization",
					In = ParameterLocation.Header,
					Type = SecuritySchemeType.ApiKey
				});
				//options.SchemaFilter<DynamicDtoSchemaFilter>();
			});
			services.Replace(ServiceDescriptor.Transient<ISwaggerProvider, CachingSwaggerProvider>());

			services.AddApiVersioning(options =>
			{
				options.AssumeDefaultVersionWhenUnspecified = true;
				options.DefaultApiVersion = ApiVersion.Default;
				options.ReportApiVersions = true;
			});

			services.AddVersionedApiExplorer(options =>
			{
				options.GroupNameFormat = "'v'VVV";
				options.SubstituteApiVersionInUrl = true;
			});
		}
	}
}
