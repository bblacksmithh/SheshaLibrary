using Abp.Modules;
using Abp.Reflection.Extensions;
using Castle.MicroKernel.Registration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Shesha;
using Shesha.Authentication.JwtBearer;
using Shesha.Authorization;
using Shesha.AzureAD;
using Shesha.Configuration;
using Shesha.Import;
using Shesha.Ldap;
using Shesha.Sms.BulkSms;
using Shesha.Sms.Clickatell;
using Shesha.Sms.SmsPortal;
using Shesha.Sms.Xml2Sms;
using Shesha.Web.FormsDesigner;
using System;
using System.Text;
using Boxfusion.eLib.Common.Authorization;
using Boxfusion.eLib.Domain;
using Boxfusion.eLib.Application;

namespace Boxfusion.eLib
{
    /// <summary>
    /// ReSharper disable once InconsistentNaming
    /// </summary>
    [DependsOn(
        // Adding all the eLib Modules
        typeof(SheshaFrameworkModule),
        typeof(SheshaApplicationModule),
        typeof(SheshaFormsDesignerModule),
        typeof(SheshaImportModule),
        typeof(SheshaLdapModule),
        typeof(SheshaAzureADModule),
        typeof(SheshaFirebaseModule),
        typeof(SheshaClickatellModule),
        typeof(SheshaBulkSmsModule),
        typeof(SheshaXml2SmsModule),
        typeof(SheshaSmsPortalModule),
        typeof(eLibModule),
        typeof(eLibApplicationModule)
	 )]
    public class eLibWebCoreModule : AbpModule
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfigurationRoot _appConfiguration;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="env"></param>
        public eLibWebCoreModule(IWebHostEnvironment env)
        {
            _env = env;
            _appConfiguration = env.GetAppConfiguration();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void PreInitialize()
        {
            Configuration.DefaultNameOrConnectionString = _appConfiguration.GetConnectionString(
                SheshaConsts.ConnectionStringName
            );

            ConfigureTokenAuth();
        }

        private void ConfigureTokenAuth()
        {
            IocManager.Register<TokenAuthConfiguration>();
            var tokenAuthConfig = IocManager.Resolve<TokenAuthConfiguration>();

            tokenAuthConfig.SecurityKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_appConfiguration["Authentication:JwtBearer:SecurityKey"]));
            tokenAuthConfig.Issuer = _appConfiguration["Authentication:JwtBearer:Issuer"];
            tokenAuthConfig.Audience = _appConfiguration["Authentication:JwtBearer:Audience"];
            tokenAuthConfig.SigningCredentials = new SigningCredentials(tokenAuthConfig.SecurityKey, SecurityAlgorithms.HmacSha256);
            tokenAuthConfig.Expiration = TimeSpan.FromDays(5);
        }

        /// <summary>
        /// 
        /// </summary>
        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(typeof(eLibWebCoreModule).GetAssembly());

            IocManager.IocContainer.Register(
            Component.For<ICustomPermissionChecker>().Forward<IeLibPermissionChecker>().Forward<eLibPermissionChecker>().ImplementedBy<eLibPermissionChecker>().LifestyleTransient()                );
        }
    }
}
