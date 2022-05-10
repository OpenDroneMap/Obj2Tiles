#region License
/*
MIT License

Copyright(c) 2017-2018 Mattias Edlund

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
#endregion

namespace MeshDecimatorCore
{
    #region ILogger
    /// <summary>
    /// A logger.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs a line of verbose text.
        /// </summary>
        /// <param name="text">The text.</param>
        void LogVerbose(string text);

        /// <summary>
        /// Logs a line of warning text.
        /// </summary>
        /// <param name="text">The text.</param>
        void LogWarning(string text);

        /// <summary>
        /// Logs a line of error text.
        /// </summary>
        /// <param name="text">The text.</param>
        void LogError(string text);
    }
    #endregion

    /// <summary>
    /// The logging API.
    /// </summary>
    public static class Logging
    {
        #region Fields
        private static ILogger logger = null;
        private static object syncObj = new object();
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the active logger.
        /// </summary>
        public static ILogger Logger
        {
            get { return logger; }
            set {
                lock (syncObj)
                {
                    logger = value;
                }
            }
        }
        #endregion

        #region Static Initializer
        /// <summary>
        /// The static initializer.
        /// </summary>
        static Logging()
        {
            logger = new Loggers.ConsoleLogger();
        }
        #endregion

        #region Public Methods
        #region Verbose
        /// <summary>
        /// Logs a line of verbose text.
        /// </summary>
        /// <param name="text">The text.</param>
        public static void LogVerbose(string text)
        {
            lock (syncObj)
            {
                if (logger != null)
                {
                    logger.LogVerbose(text);
                }
            }
        }

        /// <summary>
        /// Logs a line of formatted verbose text.
        /// </summary>
        /// <param name="format">The string format.</param>
        /// <param name="args">The string arguments.</param>
        public static void LogVerbose(string format, params object[] args)
        {
            LogVerbose(string.Format(format, args));
        }
        #endregion

        #region Warnings
        /// <summary>
        /// Logs a line of warning text.
        /// </summary>
        /// <param name="text">The text.</param>
        public static void LogWarning(string text)
        {
            lock (syncObj)
            {
                if (logger != null)
                {
                    logger.LogWarning(text);
                }
            }
        }

        /// <summary>
        /// Logs a line of formatted warning text.
        /// </summary>
        /// <param name="format">The string format.</param>
        /// <param name="args">The string arguments.</param>
        public static void LogWarning(string format, params object[] args)
        {
            LogWarning(string.Format(format, args));
        }
        #endregion

        #region Errors
        /// <summary>
        /// Logs a line of error text.
        /// </summary>
        /// <param name="text">The text.</param>
        public static void LogError(string text)
        {
            lock (syncObj)
            {
                if (logger != null)
                {
                    logger.LogError(text);
                }
            }
        }

        /// <summary>
        /// Logs a line of formatted error text.
        /// </summary>
        /// <param name="format">The string format.</param>
        /// <param name="args">The string arguments.</param>
        public static void LogError(string format, params object[] args)
        {
            LogError(string.Format(format, args));
        }
        #endregion
        #endregion
    }
}