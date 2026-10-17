using System.Reflection;
using SmallBazar.Utils;

namespace SmallBazar.Services
{
    public class VersionService : IVersionService
    {

        readonly Assembly assembly = Assembly.GetExecutingAssembly();

        private readonly AssemblyTitleAttribute titleAttribute;
        private readonly AssemblyDescriptionAttribute descriptionAttribute;
        private readonly AssemblyCompanyAttribute companyAttribute;
        private readonly AssemblyProductAttribute productAttribute;
        private readonly AssemblyCopyrightAttribute copyrightAttribute;


        public VersionService()
        {
            var log = ServiceHelper.GetService<IAppLogger>();
            try
            {
                titleAttribute = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
                descriptionAttribute = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
                companyAttribute = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
                productAttribute = assembly.GetCustomAttribute<AssemblyProductAttribute>();
                copyrightAttribute = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>();
            } catch (System.Exception ex)
            {
                log?.LogErrorAsync(AppLogger.LogLevel.Error, "Error while getting assembly attributes\n" + ex.Message);
            }
        }

        public string Title => titleAttribute?.Title ?? string.Empty;
        public string Description => descriptionAttribute?.Description ?? string.Empty;
        public string Company => companyAttribute?.Company ?? string.Empty;
        public string Product => productAttribute?.Product ?? string.Empty;
        public string Copyright => copyrightAttribute?.Copyright ?? string.Empty;
        /// <summary>Gets the version reported by the running application package.</summary>
        public string Version => AppInfo.Current.VersionString;

    }
}
