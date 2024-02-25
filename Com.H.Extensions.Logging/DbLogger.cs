using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Com.H.Data.Common;
using Microsoft.Extensions.Logging;

namespace Com.H.Extensions.Logging
{
    public class DbLogger(
        string categoryName, 
        Func<string, LogLevel, bool> filter,
        DbConnection? dbc = null, 
        string? loggingQuery = null,
        bool logToConsole = false) : ILogger
    {
        private readonly string _categoryName = categoryName;
        private readonly Func<string, LogLevel, bool> _filter = filter;
        private readonly DbConnection? _dbc = dbc;
        private readonly string? _loggingQuery = loggingQuery;
        private static readonly object _lock = new();
        private readonly bool _logToConsole = logToConsole;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return new DbLoggerDisposable();
        }

        public bool IsEnabled(LogLevel logLevel) 
            => (_filter == null || _filter(_categoryName, logLevel));

        public void Log<TState>(
            LogLevel logLevel, 
            EventId eventId, 
            TState state, 
            Exception? exception, 
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            ArgumentNullException.ThrowIfNull(formatter);

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (exception != null)
            {
                message += Environment.NewLine + exception.ToString();
            }

            lock(_lock)
            {
                if (_logToConsole)
                {
                    // print information logLevel in blue color, warning in yellow, error in red
                    switch (logLevel)
                    {
                        case LogLevel.Information:
                            Console.ForegroundColor = ConsoleColor.Blue;
                            break;
                        case LogLevel.Warning:
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            break;
                        case LogLevel.Error:
                            Console.ForegroundColor = ConsoleColor.Red;
                            break;
                    }
                    Console.Write($"{logLevel}: ");
                    // write the eventId in green color
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write($"{eventId.Id}");
                    // reset the color
                    Console.ResetColor();
                    // print the message
                    Console.WriteLine($" ({_categoryName}) - {message}");
                    // if there is an exception, print it
                    if (exception != null)
                    {
                        Console.WriteLine(exception);
                    }
                }


                if (_dbc is not null
                    && !string.IsNullOrEmpty(_loggingQuery)
                    )
                {
                    if (state is IEnumerable<KeyValuePair<string, object>> stateArgs 
                        && stateArgs?.Count() > 0)
                    {
                        _ = this._dbc.ExecuteQuery(_loggingQuery,
                        new List<Com.H.Data.Common.QueryParams>()
                        {
                            new()
                            {
                                DataModel = new
                                {
                                    log_level = logLevel.ToString(),
                                    log_event_id = eventId.Id,
                                    log_message = message,
                                    log_date = DateTime.Now,
                                    log_category_name = _categoryName,
                                    log_exception = exception?.ToString()
                                }
                            },
                            new()
                            {
                                DataModel = stateArgs
                            }
                        }).ToList();
                    }
                    else
                    {
                        _ = this._dbc.ExecuteQuery(_loggingQuery,
                                                   new List<Com.H.Data.Common.QueryParams>()
                                                   {
                            new()
                            {
                                DataModel = new
                                {
                                    log_level = logLevel.ToString(),
                                    log_event_id = eventId.Id,
                                    log_message = message,
                                    log_date = DateTime.Now,
                                    log_category_name = _categoryName,
                                    log_exception = exception?.ToString()
                                }
                            }
                        }).ToList();
                    }
                }
            }
        }
        private class DbLoggerDisposable(
            ) : IDisposable
        {
            public void Dispose()
            {
            }
        }


    }


}
