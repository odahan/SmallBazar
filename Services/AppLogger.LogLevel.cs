#nullable disable
namespace SmallBazar.Services;

public partial class AppLogger
{
    public enum LogLevel
    {
        Debug = 0,
        Information = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }
}