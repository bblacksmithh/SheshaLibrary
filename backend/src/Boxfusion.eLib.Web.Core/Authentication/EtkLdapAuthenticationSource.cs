using Shesha.Authorization.Users;
using Shesha.Ldap.Authentication;
using Shesha.Ldap.Configuration;
using Shesha.MultiTenancy;

namespace Boxfusion.eLib.Authentication
{
    /// <summary>
    /// 
    /// </summary>
    public class EtkLdapAuthenticationSource : LdapAuthenticationSource<Tenant, User>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="ldapModuleConfig"></param>
        public EtkLdapAuthenticationSource(ILdapSettings settings, ISheshaLdapModuleConfig ldapModuleConfig)
            : base(settings, ldapModuleConfig)
        {
        }
    }
}
