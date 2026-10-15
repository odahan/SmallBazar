#nullable disable


namespace SmallBazar.Services
{
    public interface IAppLogger
    {
        Task LogErrorAsync(AppLogger.LogLevel level, string message,
            [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
            [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0);
        Task<string> GetCurrentLogContentAsync();
        Task<bool> DeleteAllLogs();
        string LogDirectoryPath { get; }    

        AppLogger.LogLevel MinLogLevel { get; set; }
    }
}
