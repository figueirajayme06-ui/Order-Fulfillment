#nullable enable

using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;

namespace OF.Tests
{
    public record class LogEntry(
        string? Category,
        LogLevel Level,
        string Message,
        Exception? Error,
        DateTime Timestamp,
        EventId EventId,
        string LogLine)
    {
        public override string ToString()
            => LogLine;
    }

    public static class LoggingExtensions
    {
        /// <summary>
        /// Routes logs from <paramref name="logger"/> into <paramref name="sink"/>.
        /// If <paramref name="category"/> is null, the category will be the type name of <see cref="ILogger{TCategoryName}"/> inner type if implemented.
        /// Typical usage of <paramref name="category"/> is when using <see cref="ILogger"/> directly.
        /// </summary>
        public static Mock<T> SinkIn<T>(
            this Mock<T> logger,
            Action<string?, LogLevel, EventId, string, Exception?, DateTime> sink,
            string? category = null)
            where T : class, ILogger
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                category = typeof(T)
                .GetInterfaces()
                .Prepend(typeof(T))
                .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ILogger<>))?
                .GetGenericArguments()[0]
                .FriendlyName(omitNamespace: true);
            }

            logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>()))
                .Returns(true);
            logger.Setup(x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()))
                .Callback(new InvocationAction(invocation =>
                {
                    var now = DateTime.Now;
                    var (level, id, message, error) = invocation.Splat();
                    sink(category, level, id, message, error, now);
                }));
            return logger;
        }

        /// <summary>
        /// Routes logs from <paramref name="logger"/> into <paramref name="output"/> and allow log entries to be recorded via <paramref name="record"/>
        /// If <paramref name="category"/> is null, the category will be the type name of <see cref="ILogger{TCategoryName}"/> inner type if implemented.
        /// Typical usage of <paramref name="category"/> is when using <see cref="ILogger"/> directly.
        /// </summary>
        public static Mock<T> SinkIn<T>(
            this Mock<T> logger,
            ITestOutputHelper output,
            Action<LogEntry>? record = null,
            string? category = null)
            where T : class, ILogger
            => logger.SinkIn((category, level, id, message, e, timestamp) =>
            {
                var logLine = FormatLog(category, level, id, message, e, timestamp);
                record?.Invoke(new(category, level, message, e, timestamp, id, logLine));
                output.WriteLine(logLine);
            }, category);

        /// <summary>
        /// Routes logs from <paramref name="logger"/> into <paramref name="output"/> and record log entries in <paramref name="logs"/>.
        /// If <paramref name="category"/> is null, the category will be the type name of <see cref="ILogger{TCategoryName}"/> inner type if implemented.
        /// Typical usage of <paramref name="category"/> is when using <see cref="ILogger"/> directly.
        /// </summary>
        public static Mock<T> SinkIn<T>(
            this Mock<T> logger,
            ITestOutputHelper output,
            out IReadOnlyList<LogEntry> logs,
            string? category = null)
            where T : class, ILogger
        {
            var _logs = new List<LogEntry>();
            logs = _logs;
            logger.SinkIn((category, level, id, message, e, timestamp) =>
            {
                var logLine = FormatLog(category, level, id, message, e, timestamp);
                lock (_logs)
                {
                    _logs.Add(new(category, level, message, e, timestamp, id, logLine));
                }
                output.WriteLine(logLine);
            }, category);
            return logger;
        }

        /// <summary>
        /// Creates a logger routing logs into <paramref name="output"/> and records log entries into <paramref name="logs"/>.
        /// </summary>
        public static Mock<ILogger<T>> ToLogger<T>(
            this ITestOutputHelper output,
            out IReadOnlyList<LogEntry> logs)
            where T : class
        {
            var _logs = new List<LogEntry>();
            logs = _logs;
            return new Mock<ILogger<T>>().SinkIn((category, level, id, message, e, timestamp) =>
            {
                var logLine = FormatLog(category, level, id, message, e, timestamp);
                lock (_logs)
                {
                    _logs.Add(new(category, level, message, e, timestamp, id, logLine));
                }
                output.WriteLine(logLine);
            });
        }

        /// <summary>
        /// Creates a logger routing logs into <paramref name="output"/>
        /// </summary>
        public static Mock<ILogger<T>> ToLogger<T>(
            this ITestOutputHelper output)
            where T : class
            => new Mock<ILogger<T>>().SinkIn((category, level, id, message, e, timestamp) => output.WriteLine(FormatLog(category, level, id, message, e, timestamp)));

        /// <summary>
        /// Format the data into a string
        /// </summary>
        public static string FormatLog(
            string? category,
            LogLevel logLevel,
            EventId eventId,
            string message,
            Exception? e,
            DateTime timestamp)
        {
            const string errorCategoryTemplate =        "[{0:yyyy-MM-ddTHH:mm:ss.fff}] {1,-11} [{2}] | Message: {3} Exception:\r\n{4}";
            const string messageCategoryTemplate =      "[{0:yyyy-MM-ddTHH:mm:ss.fff}] {1,-11} [{2}] | Message: {3}";
            const string errorCategoryEventTemplate =   "[{0:yyyy-MM-ddTHH:mm:ss.fff}] {1,-11} [{2}] | EventId: [{3}] Message: {4} Exception:\r\n{5}";
            const string eventCategoryTemplate =        "[{0:yyyy-MM-ddTHH:mm:ss.fff}] {1,-11} [{2}] | EventId: [{3}] Message: {4}";

            const string errorTemplate =        "[{0:yyyy-MM-ddTHH:mm:ss.fff}] {1,-11} | Message: {2} Exception:\r\n{3}";
            const string messageTemplate =      "[{0:yyyy-MM-ddTHH:mm:ss.fff}] {1,-11} | Message: {2}";
            const string errorEventTemplate =   "[{0:yyyy-MM-ddTHH:mm:ss.fff}] {1,-11} | EventId: [{2}] Message: {3} Exception:\r\n{4}";
            const string eventTemplate =        "[{0:yyyy-MM-ddTHH:mm:ss.fff}] {1,-11} | EventId: [{2}] Message: {3}";

            var level = logLevel.ToString().ToUpperInvariant();

            return category switch
            {
                not null when eventId == default && e is not null => string.Format(errorCategoryTemplate, timestamp, level, category, message, e),
                not null when eventId == default && e is null => string.Format(messageCategoryTemplate, timestamp, level, category, message),
                not null when eventId != default && e is not null => string.Format(errorCategoryEventTemplate, timestamp, level, category, eventId, message, e),
                not null when eventId != default && e is null => string.Format(eventCategoryTemplate, timestamp, level, category, eventId, message),
                null when eventId == default && e is not null => string.Format(errorTemplate, timestamp, level, message, e),
                null when eventId != default && e is not null => string.Format(errorEventTemplate, timestamp, level, eventId, message, e),
                null when eventId != default && e is null => string.Format(eventTemplate, timestamp, level, eventId, message),
                _ => string.Format(messageTemplate, timestamp, level, message),
            };
        }

        private static (LogLevel level, EventId id, string message, Exception? error) Splat(
            this IInvocation invocation)
        {
            var level = (LogLevel)invocation.Arguments[0];
            var eventId = (EventId)invocation.Arguments[1];
            var state = invocation.Arguments[2];
            var e = invocation.Arguments[3] as Exception;
            var formatter = invocation.Arguments[4] as Delegate;
            var message = (string)formatter!.DynamicInvoke(state, e)!;
            return (level, eventId, message, e);
        }
    }
}
