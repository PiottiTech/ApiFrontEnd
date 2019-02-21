using System.Configuration;

namespace PiottiTech.ApiFrontEnd
{
    public class Config
    {
        public static string AppSetting(string appSettingName)
        {
            return ConfigurationManager.AppSettings[appSettingName] ?? "";
        }
    }
}