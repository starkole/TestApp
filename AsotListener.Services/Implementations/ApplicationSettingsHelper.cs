﻿namespace AsotListener.Services.Implementations
{
    using System;
    using System.Threading;
    using Contracts;
    using Windows.Foundation.Diagnostics;
    using Windows.Storage;

    public sealed class ApplicationSettingsHelper : IApplicationSettingsHelper, IDisposable
    {
        private ILogger logger;
        private Mutex mutex;

        public ApplicationSettingsHelper(ILogger logger)
        {
            mutex = new Mutex(false, "AsotApplicationSettingsHelperMutex");
            this.logger = logger;
            logger.LogMessage("ApplicationSettingsHelper initialized.");
        }
        
        /// <summary>
        /// Function to read a setting value and clear it after reading it
        /// </summary>
        public T ReadSettingsValue<T>(string key) where T : class
        {
            mutex.WaitOne();
            try
            {

                logger.LogMessage($"Reading {key} parameter from LoaclSettings.");
                if (!ApplicationData.Current.LocalSettings.Values.ContainsKey(key))
                {
                    logger.LogMessage($"No {key} parameter found in LoaclSettings.", LoggingLevel.Warning);
                    return null;
                }
                else
                {
                    var value = ApplicationData.Current.LocalSettings.Values[key] as T;
                    if (value == null)
                    {
                        logger.LogMessage($"Cannot cast {key} parameter to type {typeof(T)}.", LoggingLevel.Warning);
                    }

                    return value;
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"Exception on reading from LoaclSettings. {ex.Message}", LoggingLevel.Error);
                return null;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        /// <summary>
        /// Save a key value pair in settings. Create if it doesn't exist
        /// </summary>
        public void SaveSettingsValue(string key, object value)
        {
            mutex.WaitOne();
            try
            {
                logger.LogMessage($"Saving {key} parameter to LoaclSettings.");
                if (!ApplicationData.Current.LocalSettings.Values.ContainsKey(key))
                {
                    ApplicationData.Current.LocalSettings.Values.Add(key, value);
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values[key] = value;
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"Exception on saving to LoaclSettings. {ex.Message}", LoggingLevel.Error);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public void Dispose()
        {
            mutex.Dispose();
        }
    }
}
