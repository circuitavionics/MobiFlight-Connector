using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace MobiFlightInstaller
{
    static public class MobiFlightHelperMethods
    {
        public static readonly string ProcessName = "MFConnector";
        public static readonly string OptionBetaEnableSearch = "/configuration/userSettings/MobiFlight.Properties.Settings/setting[@name='BetaUpdates']";
        static Char[] s_Base32Char = {
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h',
            'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p',
            'q', 'r', 's', 't', 'u', 'v', 'w', 'x',
            'y', 'z', '0', '1', '2', '3', '4', '5'};

        static public bool GetMfBetaOptionValue()
        {
            if (!File.Exists(Directory.GetCurrentDirectory() + "\\" + ProcessName + ".exe"))
            {
                Log.Instance.log("MFConnector.exe not found, BETA disable", LogSeverity.Debug);
                return false;
            }

            string PatchConfigFile = MobiFlightHelperMethods.GetExeLocalAppDataUserConfigPath(Directory.GetCurrentDirectory() + "\\" + ProcessName + ".exe");
            Log.Instance.log("Check BETA option in " + PatchConfigFile, LogSeverity.Debug);

            if (!File.Exists(PatchConfigFile))
            {
                Log.Instance.log("Impossible to read the file does not exist -> BETA disable", LogSeverity.Debug);
                return false;
            }

            bool result = ExtractConfigBetaValueFromXML(PatchConfigFile);

            if (result)
                Log.Instance.log("BETA enable", LogSeverity.Debug);
            else
                Log.Instance.log("BETA disable", LogSeverity.Debug);

            return result;
        }

        static public bool ExtractConfigBetaValueFromXML(string PatchConfigFile)
        {
            string xmlFile = File.ReadAllText(@PatchConfigFile);
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.LoadXml(xmlFile);
            XmlNodeList nodeList = xmldoc.SelectNodes(OptionBetaEnableSearch);
            string Result = string.Empty;
            foreach (XmlNode node in nodeList)
            {
                Result = node.InnerText;
            }
            if (Result == "True") return true;
            else return false;
        }

        static public string GetExeLocalAppDataUserConfigPath(string fullExePath)
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var versionInfo = FileVersionInfo.GetVersionInfo(fullExePath);
            var companyName = versionInfo.CompanyName;
            var exeName = versionInfo.OriginalFilename;

            var assemblyName = AssemblyName.GetAssemblyName(fullExePath);
            var version = assemblyName.Version.ToString();

            var uri = "file:///" + fullExePath;
            uri = uri.ToUpperInvariant();

            var ms = new MemoryStream();
            var bSer = new BinaryFormatter();
            bSer.Serialize(ms, uri);
            ms.Position = 0;
            var sha1 = new SHA1CryptoServiceProvider();
            var hash = sha1.ComputeHash(ms);
            var hashstring = ToBase32StringSuitableForDirName(hash);

            var userConfigLocalAppDataPath = Path.Combine(localAppDataPath, companyName, exeName + "_Url_" + hashstring, version, "user.config");

            return userConfigLocalAppDataPath;
        }
// CXL: Search for all existing MobiFlight user.config files.
//
// MobiFlight stores user settings under:
// %LOCALAPPDATA%\MobiFlight\MFConnector.exe_Url_<hash>\<version>\user.config
//
// Different MobiFlight installations or installation paths can produce
// different hash folders, and previous versions can have their own
// version folders. We therefore check every genuine MFConnector config
// folder under the MobiFlight LocalAppData directory.
//
// Each user.config found is passed to CXLApplyRequiredSettingToUserConfig(),
// which changes only the Circuit Avionics required setting.
public static void CXLFindExistingUserConfigs()
{
    try
    {
        string mobiFlightSettingsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MobiFlight"
        );

        Log.Instance.log(
            "CXL settings: searching for existing MobiFlight user.config files in " +
            mobiFlightSettingsRoot,
            LogSeverity.Info
        );

        // A completely fresh installation may not yet have a MobiFlight
        // settings directory. This is normal and is not an error.
        if (!Directory.Exists(mobiFlightSettingsRoot))
        {
            Log.Instance.log(
                "CXL settings: no existing MobiFlight settings directory was found.",
                LogSeverity.Info
            );
            return;
        }

        // Only inspect folders created for MFConnector.exe by the .NET
        // user-settings system. This prevents us from searching or modifying
        // unrelated configuration files elsewhere in LocalAppData.
        string[] connectorConfigFolders = Directory.GetDirectories(
            mobiFlightSettingsRoot,
            "MFConnector.exe_Url_*",
            SearchOption.TopDirectoryOnly
        );

        int configCount = 0;

        foreach (string connectorConfigFolder in connectorConfigFolders)
        {
            // Search below each recognised MFConnector settings folder for
            // user.config files. Normally these are inside version folders
            // such as 10.5.3.0 or 11.2.0.0.
            string[] userConfigFiles = Directory.GetFiles(
                connectorConfigFolder,
                "user.config",
                SearchOption.AllDirectories
            );

            foreach (string userConfigPath in userConfigFiles)
            {
                configCount++;

                Log.Instance.log(
                    "CXL settings: found existing MobiFlight user.config: " +
                    userConfigPath,
                    LogSeverity.Info
                );

                // Apply only the required Circuit Avionics setting to this
                // existing configuration. All other user settings are preserved.
                CXLApplyRequiredSettingToUserConfig(userConfigPath);
            }
        }

        Log.Instance.log(
            "CXL settings: existing configuration search complete. Found " +
            configCount +
            " MobiFlight user.config file(s).",
            LogSeverity.Info
        );
    }
    catch (Exception ex)
    {
        // Failure to search old configuration files must never prevent
        // MobiFlight itself from being installed.
        Log.Instance.log(
            "CXL settings: unable to complete the search for existing MobiFlight configuration files. Error: " +
            ex.Message,
            LogSeverity.Error
        );
    }
}


// CXL: Apply the required Circuit Avionics setting to one existing
// MobiFlight user.config file.
//
// This function deliberately changes only FwAutoUpdateCheck.
// All other MobiFlight settings and user preferences are left untouched.
//
// If the setting is missing from an otherwise valid MobiFlight config,
// it is added. If the XML is invalid or is not a MobiFlight settings
// file, the file is left unchanged and the installer continues.
public static void CXLApplyRequiredSettingToUserConfig(string userConfigPath)
{
    try
    {
        XmlDocument config = new XmlDocument();
        config.PreserveWhitespace = true;
        config.Load(userConfigPath);

        // Verify that this really contains the expected MobiFlight
        // user-settings section before changing anything.
        XmlNode settingsNode =
            config.SelectSingleNode(
                "/configuration/userSettings/MobiFlight.Properties.Settings"
            );

        if (settingsNode == null)
        {
            Log.Instance.log(
                "CXL settings: user.config does not contain the expected MobiFlight settings section. File was not changed: " +
                userConfigPath,
                LogSeverity.Warn
            );
            return;
        }

        XmlNode settingNode =
            settingsNode.SelectSingleNode(
                "setting[@name='FwAutoUpdateCheck']"
            );

        if (settingNode == null)
        {
            // The config is valid but does not contain FwAutoUpdateCheck.
            // Add the required setting without changing anything else.
            Log.Instance.log(
                "CXL settings: FwAutoUpdateCheck was not found. Adding it with value False.",
                LogSeverity.Info
            );

            XmlElement settingElement = config.CreateElement("setting");
            settingElement.SetAttribute("name", "FwAutoUpdateCheck");
            settingElement.SetAttribute("serializeAs", "String");

            XmlElement valueElement = config.CreateElement("value");
            valueElement.InnerText = "False";

            settingElement.AppendChild(valueElement);
            settingsNode.AppendChild(settingElement);

            config.Save(userConfigPath);

            Log.Instance.log(
                "CXL settings: FwAutoUpdateCheck added successfully to " +
                userConfigPath,
                LogSeverity.Info
            );

            return;
        }

        XmlNode valueNode = settingNode.SelectSingleNode("value");

        if (valueNode == null)
        {
            // The setting exists but its value element is missing.
            // Repair it by adding the required False value.
            Log.Instance.log(
                "CXL settings: FwAutoUpdateCheck was found without a value. Adding value False.",
                LogSeverity.Info
            );

            XmlElement valueElement = config.CreateElement("value");
            valueElement.InnerText = "False";
            settingNode.AppendChild(valueElement);

            config.Save(userConfigPath);

            Log.Instance.log(
                "CXL settings: FwAutoUpdateCheck repaired successfully in " +
                userConfigPath,
                LogSeverity.Info
            );

            return;
        }

        // If the setting is already correct there is no need to rewrite
        // the file. Log this clearly and leave it untouched.
        if (String.Equals(valueNode.InnerText, "False", StringComparison.OrdinalIgnoreCase))
        {
            Log.Instance.log(
                "CXL settings: FwAutoUpdateCheck is already set to False. No change required.",
                LogSeverity.Info
            );

            return;
        }

        // The setting exists but currently has another value.
        // Record the old value in the installation log and force it to False.
        Log.Instance.log(
            "CXL settings: FwAutoUpdateCheck found with value " +
            valueNode.InnerText +
            ". Setting it to False.",
            LogSeverity.Info
        );

        valueNode.InnerText = "False";
        config.Save(userConfigPath);

        Log.Instance.log(
            "CXL settings: existing user.config updated successfully: " +
            userConfigPath,
            LogSeverity.Info
        );
    }
    catch (Exception ex)
    {
        // A malformed, locked or otherwise unreadable config must not
        // cause the Circuit Avionics installer itself to fail.
        Log.Instance.log(
            "CXL settings: unable to safely update user.config. File was left unchanged: " +
            userConfigPath +
            ". Error: " +
            ex.Message,
            LogSeverity.Error
        );
    }
}


// CXL: Create the complete initial user.config for the MobiFlight
// installation that has just been installed by this setup.
//
// This is used only when the current MobiFlight version does not already
// have a user.config. Unlike the existing-config updater above, this file
// contains the complete set of preferred Circuit Avionics starting settings.
//
// If a current user.config already exists, it is NOT overwritten.
// CXLFindExistingUserConfigs() is responsible for applying the one
// required setting to existing configurations.
public static void CXLCreateInitialUserConfig()
{
    try
    {
        string connectorPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "MFConnector.exe"
        );

        string userConfigPath =
            GetExeLocalAppDataUserConfigPath(connectorPath);

        string userConfigDirectory =
            Path.GetDirectoryName(userConfigPath);

        Log.Instance.log(
            "CXL settings: checking whether a new initial user.config is required at " +
            userConfigPath,
            LogSeverity.Info
        );

        Directory.CreateDirectory(userConfigDirectory);

        // Never overwrite an existing current configuration. Existing
        // configurations have already been handled by
        // CXLFindExistingUserConfigs().
        if (File.Exists(userConfigPath))
        {
            Log.Instance.log(
                "CXL settings: current user.config already exists. No new initial configuration file is required.",
                LogSeverity.Info
            );
            return;
        }

        Log.Instance.log(
            "CXL settings: current user.config not found. Creating the Circuit Avionics initial configuration.",
            LogSeverity.Info
        );

        string userConfigContent =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <userSettings>
    <MobiFlight.Properties.Settings>
      <setting name=""FwAutoUpdateCheck"" serializeAs=""String"">
        <value>False</value>
      </setting>
      <setting name=""LogEnabled"" serializeAs=""String"">
        <value>True</value>
      </setting>
      <setting name=""LogLevel"" serializeAs=""String"">
        <value>Info</value>
      </setting>
      <setting name=""MinimizeOnAutoRun"" serializeAs=""String"">
        <value>False</value>
      </setting>
      <setting name=""HubHopAutoCheck"" serializeAs=""String"">
        <value>False</value>
      </setting>
      <setting name=""EnableMidiSupport"" serializeAs=""String"">
        <value>False</value>
      </setting>
      <setting name=""ProSimAutoConnectEnabled"" serializeAs=""String"">
        <value>False</value>
      </setting>
      <setting name=""AutoRetrigger"" serializeAs=""String"">
        <value>False</value>
      </setting>
    </MobiFlight.Properties.Settings>
  </userSettings>
</configuration>";

        File.WriteAllText(userConfigPath, userConfigContent);

        Log.Instance.log(
            "CXL settings: Circuit Avionics initial user.config created successfully.",
            LogSeverity.Info
        );
    }
    catch (Exception ex)
    {
        // Failure to create the initial settings file should be recorded,
        // but it must not crash the main MobiFlight installation.
        Log.Instance.log(
            "CXL settings: unable to create the Circuit Avionics initial user.config. Error: " +
            ex.Message,
            LogSeverity.Error
        );
    }
}

        private static string ToBase32StringSuitableForDirName(byte[] buff)
        {
            StringBuilder sb = new StringBuilder();
            byte b0, b1, b2, b3, b4;
            int l, i;

            l = buff.Length;
            i = 0;

            // Create l chars using the last 5 bits of each byte.  
            // Consume 3 MSB bits 5 bytes at a time.

            do
            {
                b0 = (i < l) ? buff[i++] : (byte)0;
                b1 = (i < l) ? buff[i++] : (byte)0;
                b2 = (i < l) ? buff[i++] : (byte)0;
                b3 = (i < l) ? buff[i++] : (byte)0;
                b4 = (i < l) ? buff[i++] : (byte)0;

                // Consume the 5 Least significant bits of each byte
                sb.Append(s_Base32Char[b0 & 0x1F]);
                sb.Append(s_Base32Char[b1 & 0x1F]);
                sb.Append(s_Base32Char[b2 & 0x1F]);
                sb.Append(s_Base32Char[b3 & 0x1F]);
                sb.Append(s_Base32Char[b4 & 0x1F]);

                // Consume 3 MSB of b0, b1, MSB bits 6, 7 of b3, b4
                sb.Append(s_Base32Char[(
                    ((b0 & 0xE0) >> 5) |
                    ((b3 & 0x60) >> 2))]);

                sb.Append(s_Base32Char[(
                    ((b1 & 0xE0) >> 5) |
                    ((b4 & 0x60) >> 2))]);

                // Consume 3 MSB bits of b2, 1 MSB bit of b3, b4

                b2 >>= 5;

                if ((b3 & 0x80) != 0)
                    b2 |= 0x08;
                if ((b4 & 0x80) != 0)
                    b2 |= 0x10;

                sb.Append(s_Base32Char[b2]);

            } while (i < l);

            return sb.ToString();
        }
    }
}
