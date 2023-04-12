using Serilog.Events;

namespace Stryker.Core.Primitives.Options
{
    public class LogOptions
    {
        public bool LogToFile { get; init; }
        public LogEventLevel LogLevel { get; init; }
    }
}