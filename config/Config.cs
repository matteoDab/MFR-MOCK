using GALEDI.servs;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Xml.Linq;

namespace GALEDI.config
{
    public static class Config
    {
        public static string LocalSQL_ConnectionString
        {
            get
            {
                return $"Server={LocalSQL_IP};Database={LocalSQL_DBName};Uid={LocalSQL_User};Pwd={LocalSQL_Password};";
            }
        }

        public static string LocalSQL_IP { get; private set; }
        public static string LocalSQL_DBName { get; private set; }
        public static string LocalSQL_User { get; private set; }
        public static string LocalSQL_Password { get; private set; }

        internal static void SetLocalSQL_IP(string value) => LocalSQL_IP = value;
        internal static void SetLocalSQL_DBName(string value) => LocalSQL_DBName = value;
        internal static void SetLocalSQL_User(string value) => LocalSQL_User = value;
        internal static void SetLocalSQL_Password(string value) => LocalSQL_Password = value;

        public static IPAddress MFRH_TCP_IP { get; private set; }
        public static int MFRH_TCP_Port { get; private set; }
        public static IPAddress MFRH_HTTP_IP { get; private set; }
        public static int MFRH_HTTP_Port { get; private set; }
        public static string MFRH_HTTP_Rueckmeldung { get; private set; }
        public static string MFRH_FTP_Server { get; private set; }
        public static string MFRH_FTP_User { get; private set; }
        public static string MFRH_FTP_Password { get; private set; }

        internal static void SetMFRH_TCP_IP(IPAddress value) => MFRH_TCP_IP = value;
        internal static void SetMFRH_TCP_Port(int value) => MFRH_TCP_Port = value;
        internal static void SetMFRH_HTTP_IP(IPAddress value) => MFRH_HTTP_IP = value;
        internal static void SetMFRH_HTTP_Port(int value) => MFRH_HTTP_Port = value;
        internal static void SetMFRH_HTTP_Rueckmeldung(string value) => MFRH_HTTP_Rueckmeldung = value;
        internal static void SetMFRH_FTP_Server(string value) => MFRH_FTP_Server = value;
        internal static void SetMFRH_FTP_User(string value) => MFRH_FTP_User = value;
        internal static void SetMFRH_FTP_Password(string value) => MFRH_FTP_Password = value;

        public static IPAddress MFRE_TCP_IP { get; private set; }
        public static int MFRE_TCP_Port { get; private set; }
        public static IPAddress MFRE_HTTP_IP { get; private set; }
        public static int MFRE_HTTP_Port { get; private set; }
        public static string MFRE_HTTP_Rueckmeldung { get; private set; }
        public static string MFRE_FTP_Server { get; private set; }
        public static string MFRE_FTP_User { get; private set; }
        public static string MFRE_FTP_Password { get; private set; }

        internal static void SetMFRE_TCP_IP(IPAddress value) => MFRE_TCP_IP = value;
        internal static void SetMFRE_TCP_Port(int value) => MFRE_TCP_Port = value;
        internal static void SetMFRE_HTTP_IP(IPAddress value) => MFRE_HTTP_IP = value;
        internal static void SetMFRE_HTTP_Port(int value) => MFRE_HTTP_Port = value;
        internal static void SetMFRE_HTTP_Rueckmeldung(string value) => MFRE_HTTP_Rueckmeldung = value;
        internal static void SetMFRE_FTP_Server(string value) => MFRE_FTP_Server = value;
        internal static void SetMFRE_FTP_User(string value) => MFRE_FTP_User = value;
        internal static void SetMFRE_FTP_Password(string value) => MFRE_FTP_Password = value;

        public static IPAddress MFRA_TCP_IP { get; private set; }
        public static int MFRA_TCP_Port { get; private set; }
        public static IPAddress MFRA_HTTP_IP { get; private set; }
        public static int MFRA_HTTP_Port { get; private set; }

        internal static void SetMFRA_TCP_IP(IPAddress value) => MFRA_TCP_IP = value;
        internal static void SetMFRA_TCP_Port(int value) => MFRA_TCP_Port = value;
        internal static void SetMFRA_HTTP_IP(IPAddress value) => MFRA_HTTP_IP = value;
        internal static void SetMFRA_HTTP_Port(int value) => MFRA_HTTP_Port = value;

        public static string FTP_Server { get; private set; }
        public static string FTP_User { get; private set; }
        public static string FTP_Password { get; private set; }

        internal static void SetFTP_Server(string value) => FTP_Server = value;
        internal static void SetFTP_User(string value) => FTP_User = value;
        internal static void SetFTP_Password(string value) => FTP_Password = value;

        public static int timerTCPInterval { get; private set; }
        public static int timerFTPInterval { get; private set; }
        public static int timerSQLInterval { get; private set; }
        public static int timerExcelInterval { get; private set; }

        internal static void SetTimerTCPInterval(int value) => timerTCPInterval = value;
        internal static void SetTimerFTPInterval(int value) => timerFTPInterval = value;
        internal static void SetTimerSQLInterval(int value) => timerSQLInterval = value;
        internal static void SetTimerExcelInterval(int value) => timerExcelInterval = value;

        public static void LoadConfig()
        {
            try
            {
                RegistryHelper.ReadConfiguration();
                Console.WriteLine("Configuration loaded successfully.");
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"An error occurred while loading the configuration: {ex.Message}");
                Help.PrintRedLine("Please check the configuration in the registry.");
                Environment.Exit(0);
            }
        }
    }
}