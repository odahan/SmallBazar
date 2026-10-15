using System.Reflection;
using SmallBazar.Utils;

namespace SmallBazar.Services
{
    public class VersionService : IVersionService
    {

        private bool isVersionBugPresent = true;
        private string versionWhenVersionIsBugged = "1.6.4"; // 28.11.24 version is still buggy and must be set to 1.0.0 to compile...
        
        readonly Assembly assembly = Assembly.GetExecutingAssembly();

        private readonly AssemblyTitleAttribute titleAttribute;
        private readonly AssemblyDescriptionAttribute descriptionAttribute;
        private readonly AssemblyCompanyAttribute companyAttribute;
        private readonly AssemblyProductAttribute productAttribute;
        private readonly AssemblyCopyrightAttribute copyrightAttribute;
        private readonly Version assemblyVersion;


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
                assemblyVersion = assembly.GetName().Version;
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
        public string Version => isVersionBugPresent 
                                   ? versionWhenVersionIsBugged 
                                   : assemblyVersion?.ToString() ?? string.Empty;

    }
}
