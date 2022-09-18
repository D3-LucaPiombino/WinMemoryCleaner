using Microsoft.Extensions.Logging;

namespace WinMemoryCleaner.Core
{
    /// <summary>
    /// Log Helper
    /// </summary>
    public class LogHelper<T>
    {
        #region Fields

        private readonly ILogger<T> _logger;

        #endregion Fields

        public LogHelper(
            ILogger<T> logger
        )
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        

        #region Methods

        /// <summary>
        /// Debug
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="method">Method</param>
        public void Debug(string? message)
        {
            _logger.LogDebug(message);
        }

        /// <summary>
        /// Error
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="method">Method</param>
        public void Error(string message)
        {
            _logger.LogError(message);
        }

        /// <summary>
        /// Error
        /// </summary>
        /// <param name="exception">Exception</param>
        /// <param name="message">Custom message about the Exception</param>
        /// <param name="method">Method</param>
        public void Error(Exception exception, string? message = null)
        {
            _logger.LogError(exception, message ?? "Exception");
        }

        /// <summary>
        /// Info
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="method">Method</param>
        public void Info(string? message)
        {
            _logger.LogInformation(message);
        }



        /// <summary>
        /// Warning
        /// </summary>
        /// <param name="message">Message</param>
        /// <param name="method">Method</param>
        public void Warning(string? message)
        {
            _logger.LogWarning(message);
        }
    }

    #endregion Methods
}