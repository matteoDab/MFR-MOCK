using GALEDI.config;
using GALEDI.servs;
using GALEDI.SQL;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GALEDI
{
    class Program : IDisposable
    {
        // Configuration flags for enabling/disabling timers
        private static bool useTimerDownload = true;
        private static bool useTimerCheckRequest = true;

        // Variables to skip the first execution of each timer
        private static bool timerDownloadINTFirstExecutionSkipped = false;
        private static bool timerCheckRequestFirstExecutionSkipped = false;

        // Timer objects for periodic tasks
        private static Timer timerDownloadINT = null;
        private static Timer timerCheckRequest = null;

        // Cancellation token source to signal shutdown and control task cancellation
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private static CancellationToken cancellationToken;

        static async Task Main(string[] args)
        {
            // Initialize global exception handling
            AppDomain.CurrentDomain.UnhandledException += GlobalExceptionHandler;

            try
            {
                // Load configuration settings
                Config.LoadConfig();

                // Initialize the cancellation token
                cancellationToken = cancellationTokenSource.Token;

                // Setup and start timers based on configuration
                if (useTimerDownload)
                {
                    timerDownloadINT = new Timer(TimerCallDownloadINT, null, 0, 20000);
                }
                if (useTimerCheckRequest)
                {
                    timerCheckRequest = new Timer(async state => await TimerCallCheckRequest(state), null, 0, 30000); // ???
                }

                Console.WriteLine("\nPress [Enter] to exit the program.");
                Console.ReadLine();

                // Signal cancellation and clean up resources on program exit
                cancellationTokenSource.Cancel();
                DisposeTimers();
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"Unhandled exception occurred in Main: {ex.Message}");
            }
            finally
            {
                DisposeTimers();
            }
        }

        /// <summary>
        /// Global exception handler for unhandled exceptions.
        /// Logs the exception details and performs necessary cleanup.
        /// </summary>
        private static void GlobalExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            Help.PrintRedLine($"Global Exception Handler caught an unhandled exception: {ex.Message}");
            // Additional logging or cleanup can be added here

            // Ensure timers are disposed and cancellation is signaled
            DisposeTimers();
            cancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Determines whether the first execution of a timer should be skipped.
        /// </summary>
        private static bool ShouldSkipFirstExecution(ref bool firstExecutionSkipped)
        {
            if (!firstExecutionSkipped)
            {
                firstExecutionSkipped = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Timer callback method for downloading data via FTP.
        /// Includes logic to handle first execution skipping and exception handling.
        /// </summary>
        private static void TimerCallDownloadINT(object state)
        {
            if (ShouldSkipFirstExecution(ref timerDownloadINTFirstExecutionSkipped))
                return;

            try
            {
                Console.WriteLine("\n\n--- TimerCallDownloadINT Start ---\n");
                Console.WriteLine($"Execution Time: {DateTime.Now:HH:mm:ss}\n");

                var mySQL = new MySQL(Config.LocalSQL_ConnectionString);
                FTP ftp = new FTP(mySQL);
                List<string> mfrs = new List<string> { Mfrs.H, Mfrs.E };
                foreach (var mfr in mfrs)
                {
                    ftp.DownloadINT(mfr);
                }

                Console.WriteLine("\n--- TimerCallDownloadINT End ---\n");
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"An error occurred in {nameof(TimerCallDownloadINT)}: {ex.Message}");
            }
        }

        /// <summary>
        /// Asynchronous timer callback method for checking and uploading data via FTP.
        /// Includes logic to handle first execution skipping, exception handling, and cancellation token support.
        /// </summary>
        private static async Task TimerCallCheckRequest(object state)
        {
            if (ShouldSkipFirstExecution(ref timerCheckRequestFirstExecutionSkipped))
                return;

            try
            {
                // Support for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    Help.PrintRedLine("Cancellation requested. Exiting TimerCallCheckRequest.");
                    return;
                }

                Console.WriteLine("\n\n--- TimerCallCheckRequest Start ---\n");
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss "));

                var mySQL = new MySQL(Config.LocalSQL_ConnectionString);
                FTP ftp = new FTP(mySQL);
                await ftp.UploadLVSAsync(cancellationToken);

                Console.WriteLine("\n--- TimerCallCheckRequest End ---\n");
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"An error occurred in {nameof(TimerCallCheckRequest)}: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes the timers and performs any necessary cleanup.
        /// </summary>
        private static void DisposeTimers()
        {
            timerDownloadINT?.Dispose();
            timerCheckRequest?.Dispose();
        }

        /// <summary>
        /// Implementation of IDisposable to ensure resources are cleaned up properly.
        /// This includes disposing of timers and cancellation token source.
        /// </summary>
        public void Dispose()
        {
            DisposeTimers();
            cancellationTokenSource.Dispose();
        }
    }
}
