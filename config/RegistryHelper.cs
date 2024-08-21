using GALEDI.servs;
using Microsoft.Win32;
using System;
using System.Drawing.Printing;
using System.Net;

namespace GALEDI.config
{
    public static class RegistryHelper
    {
        private const string BaseRegistryPath = @"SOFTWARE\GALEDI";
        private static bool keyCreated = false;
        private static List<string> newKeys = new List<string>();


        private static string GetSubKeyPath(string subKey)
        {
            return $@"{BaseRegistryPath}\{subKey}";
        }

        public static void ReadConfiguration()
        {
            // Ensure all keys are created or verified before reading
            CreateDefaultKeys();

            ReadLocalSQLConfiguration();
            ReadMFRHConfiguration();
            ReadMFREConfiguration();
            ReadMFRAConfiguration();

            ReadTimerConfiguration();
        }

        public static void CreateDefaultKeys()
        {
            CreateDefaultSQLKeys();
            CreateDefaultMFRHKeys();
            CreateDefaultMFREKeys();
            CreateDefaultMFRAKeys();

            CreateDefaulTimerKeys();

            if (keyCreated)
            {
                Help.PrintRedLine($"At least one missing registry entry had to be created.\nPlease configurate the following keys in the Registry Editor and restart the program.");
                
                foreach (string key in newKeys)
                {
                    Help.PrintRedLine(key);
                }

                Environment.Exit(0);
            }
        }

        private static void CreateDefaultSQLKeys()
        {
            CreateKeyIfNull("SQL", "LocalSQL_IP", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull("SQL", "LocalSQL_DBName", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull("SQL", "LocalSQL_User", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull("SQL", "LocalSQL_Password", string.Empty, RegistryValueKind.String);
        }

        private static void CreateDefaultMFRHKeys()
        {
            CreateKeyIfNull(Mfrs.H, "MFRH_TCP_IP", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.H, "MFRH_TCP_Port", 0, RegistryValueKind.DWord);
            CreateKeyIfNull(Mfrs.H, "MFRH_HTTP_IP", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.H, "MFRH_HTTP_Port", 0, RegistryValueKind.DWord);
            CreateKeyIfNull(Mfrs.H, "MFRH_HTTP_Rueckmeldung", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.H, "MFRH_FTP_Server", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.H, "MFRH_FTP_User", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.H, "MFRH_FTP_Password", string.Empty, RegistryValueKind.String);
        }

        private static void CreateDefaultMFREKeys()
        {
            CreateKeyIfNull(Mfrs.E, "MFRE_TCP_IP", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.E, "MFRE_TCP_Port", 0, RegistryValueKind.DWord);
            CreateKeyIfNull(Mfrs.E, "MFRE_HTTP_IP", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.E, "MFRE_HTTP_Port", 0, RegistryValueKind.DWord);
            CreateKeyIfNull(Mfrs.E, "MFRE_HTTP_Rueckmeldung", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.E, "MFRE_FTP_Server", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.E, "MFRE_FTP_User", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.E, "MFRE_FTP_Password", string.Empty, RegistryValueKind.String);
        }

        private static void CreateDefaultMFRAKeys()
        {
            CreateKeyIfNull(Mfrs.A, "MFRA_TCP_IP", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.A, "MFRA_TCP_Port", 0, RegistryValueKind.DWord);
            CreateKeyIfNull(Mfrs.A, "MFRA_HTTP_IP", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.A, "MFRA_HTTP_Port", 0, RegistryValueKind.DWord);
            CreateKeyIfNull(Mfrs.A, "MFRA_FTP_Server", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.A, "MFRA_FTP_User", string.Empty, RegistryValueKind.String);
            CreateKeyIfNull(Mfrs.A, "MFRA_FTP_Password", string.Empty, RegistryValueKind.String);
        }

        private static void CreateDefaulTimerKeys()
        {
            CreateKeyIfNull("Timer", "TCP", 0, RegistryValueKind.DWord);
            CreateKeyIfNull("Timer", "FTP", 0, RegistryValueKind.DWord);
            CreateKeyIfNull("Timer", "SQL", 0, RegistryValueKind.DWord);
            CreateKeyIfNull("Timer", "Excel", 0, RegistryValueKind.DWord);
        }

        private static void CreateKeyIfNull(string subKey, string valueName, object defaultValue, RegistryValueKind valueKind)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(GetSubKeyPath(subKey), writable: true))
            {
                if (key == null)
                {
                    using (RegistryKey newKey = Registry.LocalMachine.CreateSubKey(GetSubKeyPath(subKey)))
                    {
                        newKey.SetValue(valueName, defaultValue, valueKind);
                        keyCreated = true;
                        newKeys.Add(valueName);
                    }
                }
                else if (key.GetValue(valueName) == null)
                {
                    key.SetValue(valueName, defaultValue, valueKind);
                    keyCreated = true;
                    newKeys.Add(valueName);
                }
            }
        }

        private static void ReadLocalSQLConfiguration()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(GetSubKeyPath("SQL")))
            {
                if (key != null)
                {
                    Config.SetLocalSQL_IP((string)key.GetValue("LocalSQL_IP"));
                    Config.SetLocalSQL_DBName((string)key.GetValue("LocalSQL_DBName"));
                    Config.SetLocalSQL_User((string)key.GetValue("LocalSQL_User"));
                    Config.SetLocalSQL_Password((string)key.GetValue("LocalSQL_Password"));
                }
            }
        }

        private static void ReadMFRHConfiguration()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(GetSubKeyPath(Mfrs.H)))
            {
                if (key != null)
                {
                    Config.SetMFRH_TCP_IP(IPAddress.Parse((string)key.GetValue("MFRH_TCP_IP")));
                    Config.SetMFRH_TCP_Port((int)key.GetValue("MFRH_TCP_Port"));

                    Config.SetMFRH_HTTP_IP(IPAddress.Parse((string)key.GetValue("MFRH_HTTP_IP")));
                    Config.SetMFRH_HTTP_Port((int)key.GetValue("MFRH_HTTP_Port"));
                    Config.SetMFRH_HTTP_Rueckmeldung((string)key.GetValue("MFRH_HTTP_Rueckmeldung"));

                    Config.SetMFRH_FTP_Server((string)key.GetValue("MFRH_FTP_Server"));
                    Config.SetMFRH_FTP_User((string)key.GetValue("MFRH_FTP_User"));
                    Config.SetMFRH_FTP_Password((string)key.GetValue("MFRH_FTP_Password"));
                }
            }
        }

        private static void ReadMFREConfiguration()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(GetSubKeyPath(Mfrs.E)))
            {
                if (key != null)
                {
                    Config.SetMFRE_TCP_IP(IPAddress.Parse((string)key.GetValue("MFRE_TCP_IP")));
                    Config.SetMFRE_TCP_Port((int)key.GetValue("MFRE_TCP_Port"));

                    Config.SetMFRE_HTTP_IP(IPAddress.Parse((string)key.GetValue("MFRE_HTTP_IP")));
                    Config.SetMFRE_HTTP_Port((int)key.GetValue("MFRE_HTTP_Port"));
                    Config.SetMFRE_HTTP_Rueckmeldung((string)key.GetValue("MFRE_HTTP_Rueckmeldung"));

                    Config.SetMFRE_FTP_Server((string)key.GetValue("MFRE_FTP_Server"));
                    Config.SetMFRE_FTP_User((string)key.GetValue("MFRE_FTP_User"));
                    Config.SetMFRE_FTP_Password((string)key.GetValue("MFRE_FTP_Password"));
                }
            }
        }

        private static void ReadMFRAConfiguration()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(GetSubKeyPath(Mfrs.A)))
            {
                if (key != null)
                {
                    Config.SetMFRA_TCP_IP(IPAddress.Parse((string)key.GetValue("MFRA_TCP_IP")));
                    Config.SetMFRA_TCP_Port((int)key.GetValue("MFRA_TCP_Port"));

                    Config.SetMFRA_HTTP_IP(IPAddress.Parse((string)key.GetValue("MFRA_HTTP_IP")));
                    Config.SetMFRA_HTTP_Port((int)key.GetValue("MFRA_HTTP_Port"));

                    Config.SetFTP_Server((string)key.GetValue("MFRA_FTP_Server"));
                    Config.SetFTP_User((string)key.GetValue("MFRA_FTP_User"));
                    Config.SetFTP_Password((string)key.GetValue("MFRA_FTP_Password"));
                }
            }
        }

        private static void ReadTimerConfiguration()
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(GetSubKeyPath("Timer")))
            {
                if (key != null)
                {
                    Config.SetTimerTCPInterval((int)key.GetValue("TCP"));
                    Config.SetTimerFTPInterval((int)key.GetValue("FTP"));
                    Config.SetTimerSQLInterval((int)key.GetValue("SQL"));
                    Config.SetTimerExcelInterval((int)key.GetValue("Excel"));
                }
            }
        }
    }
}