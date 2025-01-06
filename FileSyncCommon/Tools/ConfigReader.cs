using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileSyncCommon.Tools
{
    public static class ConfigReader
    {
        public static int GetInt(string key, int defaultValue)
        {
            var value = ConfigurationManager.AppSettings.Get(key);
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    var result = int.Parse(value);
                }
                catch (Exception ex)
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
        public static DateTime GetDateTime(string key, DateTime defaultValue)
        {
            var value = ConfigurationManager.AppSettings.Get(key);
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    var result = DateTime.Parse(value);
                }
                catch (Exception ex)
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }
        public static string GetString(string key, string defaultValue)
        {
            var value = ConfigurationManager.AppSettings.Get(key);
            if (!string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            return defaultValue;
        }
    }
}
