using GALEDI.config;
using GALEDI.SQL;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace GALEDI.servs
{
    /// <summary>
    /// The FTP class provides methods to download and upload files to and from an FTP server.
    /// It also handles interaction with the MySQL database for storing and retrieving data.
    /// </summary>
    internal class FTP
    {
        private readonly MySQL _mySQL;

        // Constructor Injection
        public FTP(MySQL mySQL)
        {
            _mySQL = mySQL;
        }

        // FTP configuration details for different manufacturers
        private static readonly string ftpServerMFRH = Config.MFRH_FTP_Server;
        private static readonly string usernameMFRH = Config.MFRH_FTP_User;
        private static readonly string passwordMFRH = Config.MFRH_FTP_Password;

        private static readonly string ftpServerMFRE = Config.MFRE_FTP_Server;
        private static readonly string usernameMFRE = Config.MFRE_FTP_User;
        private static readonly string passwordMFRE = Config.MFRE_FTP_Password;

        // Real FTP server details for direct access
        private static readonly string MFRH_FTP_ServerREAL = "ftp://10.1.133.69";
        private static readonly string MFRE_FTP_ServerREAL = "ftp://10.1.133.67";
        private static readonly string FTP_REAL = "ftp_user";

        private static readonly string requestFile = "LVS_REQ.txt";
        private static readonly string uploadFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "GALEDI", "Uploads");

        /// <summary>
        /// Asynchronous method to upload LVS files to the FTP server.
        /// </summary>
        public async Task UploadLVSAsync(CancellationToken cancellationToken)
        {
            try
            {
                List<string> mfrs = new List<string> { Mfrs.H, Mfrs.E };
                foreach (var mfr in mfrs)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Help.PrintRedLine("Cancellation requested. Exiting UploadLVSAsync.");
                        return;
                    }

                    string fileName = mfr == Mfrs.H ? "mfrh_lvs.txt" : "mfre_lvs.txt";

                    bool fileExistsRequest = await CheckFileExistsAsync(requestFile, GetFtpDetails(mfr));
                    bool fileExistsFeedback = await CheckFileExistsAsync(fileName, GetFtpDetails(mfr));

                    if (fileExistsRequest && !fileExistsFeedback)
                    {
                        var data = await _mySQL.ReadFromMFRXINTAsync(mfr);

                        await WriteDataToTxtAndUploadAsync(mfr, data, cancellationToken);
                    }
                    else if (!fileExistsRequest && fileExistsFeedback)
                    {
                        // Delete the feedback file if it exists and no request file is present
                        bool deleteSuccess = await DeleteFileFromFtpServerAsync(mfr, fileName);
                        if (deleteSuccess)
                        {
                            Console.WriteLine($"Feedback file {fileName} for {mfr} successfully deleted.");
                        }
                        else
                        {
                            Help.PrintRedLine($"Failed to delete feedback file {fileName} for {mfr}.");
                        }

                    }
                    else if (fileExistsRequest && fileExistsFeedback)
                    {
                        Help.PrintRedLine($"A Feedback file for {mfr} is already there.");
                    }
                }
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"Error in {nameof(UploadLVSAsync)}: {ex.Message}");
            }
        }

        /// <summary>
        /// Uploads a file to the FTP server asynchronously and updates the database upon success.
        /// </summary>
        /// <param name="ftpDetails">The FTP server details (server, username, password).</param>
        /// <param name="fileName">The name of the file to be uploaded.</param>
        /// <param name="sourceFilePath">The local file path of the file to be uploaded.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <param name="mfr">The manufacturer identifier for database update after upload.</param>
        private async Task UploadFileToFtpServerAsync((string server, string username, string password) ftpDetails, string fileName, string sourceFilePath, CancellationToken cancellationToken, string mfr)
        {
            FtpWebRequest request = null;
            try
            {
                // Create the FTP request
                request = (FtpWebRequest)WebRequest.Create($"{ftpDetails.server}/{fileName}");
                request.Method = WebRequestMethods.Ftp.UploadFile;
                request.Credentials = new NetworkCredential(ftpDetails.username, ftpDetails.password);
                request.UseBinary = true;

                // Upload the file
                using (FileStream fileStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read))
                using (Stream ftpStream = request.GetRequestStream())
                {
                    await fileStream.CopyToAsync(ftpStream, cancellationToken);
                }

                // Get the response from the FTP server
                using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    Console.WriteLine($"Successfully uploaded {fileName} to {ftpDetails.server}. Status: {response.StatusDescription}");

                    // Update the database after successful upload
                    await UpdateDatabaseAfterUploadAsync(mfr);
                }
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"Error uploading file {fileName} to {ftpDetails.server}: {ex.Message}");
            }
            finally
            {
                // Abort the request if something goes wrong
                request?.Abort();
            }
        }


        /// <summary>
        /// Updates the SQL database to set the 'deleted' column to 1 for the specified manufacturer.
        /// </summary>
        /// <param name="mfr">The manufacturer identifier.</param>
        private async Task UpdateDatabaseAfterUploadAsync(string mfr)
        {
            try
            {
                string query = "UPDATE tbl_mfrx_int SET deleted = 1 WHERE mfr = @mfr AND deleted != 1";

                using (var connection = new MySqlConnection(Config.LocalSQL_ConnectionString))
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@mfr", mfr);
                    await connection.OpenAsync();
                    int rowsAffected = await command.ExecuteNonQueryAsync();
                    Console.WriteLine($"Successfully updated {rowsAffected} rows in the database for {mfr}.");
                }
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"Error updating database after file upload for {mfr}: {ex.Message}");
            }
        }

        public async Task<List<(int id, string[])>> ReadFromMFRXINTAsync(string mfr)
        {
            var result = new List<(int id, string[])>();
            try
            {
                string query = "SELECT id, LE, plannedDestination, actualDestination, status, date, time FROM tbl_mfrx_int WHERE mfr = @mfr AND deleted != 1";

                using (var connection = new MySqlConnection(Config.LocalSQL_ConnectionString))
                using (var command = new MySqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@mfr", mfr);
                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var id = reader.GetInt32(0); // Get the ID from the first column
                            var row = new string[reader.FieldCount - 1];
                            for (int i = 1; i < reader.FieldCount; i++)
                            {
                                row[i - 1] = reader.GetString(i);
                            }
                            result.Add((id, row));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"Error reading from database for {mfr}: {ex.Message}");
            }

            return result;
        }



        /// <summary>
        /// Writes data to a temporary text file and uploads it to the FTP server. Updates the database if the upload is successful.
        /// </summary>
        /// <param name="mfr">The manufacturer identifier.</param>
        /// <param name="data">The data to be written to the text file and uploaded.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        public async Task WriteDataToTxtAndUploadAsync(string mfr, List<(int id, string[] data)> dataWithIds, CancellationToken cancellationToken, FTP ftpClient, MySQL mySQL)
        {
            string fileName = mfr == Mfrs.H ? "mfrh_lvs.txt" : "mfre_lvs.txt";
            string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);
            var idsToMarkAsDeleted = new List<int>();

            try
            {
                // Step 1: Write data to a temporary file
                using (var writer = new StreamWriter(tempFilePath, false))
                {
                    foreach (var (id, row) in dataWithIds)
                    {
                        string line = string.Join(",", row);
                        await writer.WriteLineAsync(line);

                        // Collect the IDs of the rows that were successfully written to the file
                        idsToMarkAsDeleted.Add(id);
                    }
                }

                Console.WriteLine($"File {fileName} successfully created at {tempFilePath}.");

                // Step 2: Upload the file to the FTP server
                await UploadFileToFtpServerAsync(GetFtpDetails(mfr), fileName, tempFilePath, cancellationToken, mfr);

                // Step 3: Update the database to mark the specific rows as deleted
                await mySQL.UpdateDatabaseAfterUploadAsync(mfr, idsToMarkAsDeleted);
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"Error creating or uploading file {fileName}: {ex.Message}");
            }
            finally
            {
                // Delete the temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }



        /// <summary>
        /// Writes data to a temporary text file and uploads it to the FTP server.
        /// Supports cancellation.
        /// </summary>
        public async Task WriteDataToTxtAndUploadAsync(string mfr, List<string[]> data, CancellationToken cancellationToken)
        {
            string fileName = mfr == Mfrs.H ? "mfrh_lvs.txt" : "mfre_lvs.txt";
            string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);

            try
            {
                // Step 1: Write data to a temporary file
                using (var writer = new StreamWriter(tempFilePath, false))
                {
                    if (data.Count > 0)
                    {
                        foreach (var row in data)
                        {
                            string line = string.Join(",", row);
                            await writer.WriteLineAsync(line);
                        }
                    }
                    // If data is empty, the file will be empty as well
                }

                Console.WriteLine($"File {fileName} successfully created at {tempFilePath}.");

                // Step 2: Upload the file to the FTP server
                await UploadFileToFtpServerAsync(GetFtpDetails(mfr), fileName, tempFilePath, cancellationToken, mfr);
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"Error creating or uploading file {fileName}: {ex.Message}");
            }
            finally
            {
                // Delete the temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        /// <summary>
        /// Downloads a file from the FTP server, processes it, and writes the data to a SQL table.
        /// </summary>
        public void DownloadINT(string mfr)
        {
            try
            {
                string sourceServer = mfr == Mfrs.H ? MFRH_FTP_ServerREAL : MFRE_FTP_ServerREAL;
                string destinationServer = mfr == Mfrs.H ? ftpServerMFRH : ftpServerMFRE;
                string username = FTP_REAL;
                string password = FTP_REAL;
                string fileName = mfr == Mfrs.H ? "mfrh_int.txt" : "mfre_int.txt";
                string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);

                // Download the file from the "real" FTP server
                DownloadFileFromFtpServer(sourceServer, fileName, username, password, tempFilePath);

                // Write file data to SQL table
                _mySQL.WriteToMFRXINT(mfr, tempFilePath);
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"Error in {nameof(DownloadINT)}: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads a file from the specified FTP server.
        /// </summary>
        private void DownloadFileFromFtpServer(string server, string fileName, string username, string password, string destinationPath)
        {
            FtpWebRequest request = null;
            try
            {
                request = (FtpWebRequest)WebRequest.Create($"{server}/{fileName}");
                request.Method = WebRequestMethods.Ftp.DownloadFile;
                request.Credentials = new NetworkCredential(username, password);

                using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
                using (Stream responseStream = response.GetResponseStream())
                using (FileStream fileStream = new FileStream(destinationPath, FileMode.Create))
                {
                    responseStream.CopyTo(fileStream);
                }

                Console.WriteLine($"\nSuccessfully downloaded file {fileName} from {server}.");
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"Error downloading file {fileName} from {server}: {ex.Message}");
                throw;
            }
            finally
            {
                request?.Abort();
            }
        }

        /// <summary>
        /// Checks if a specific file exists on the FTP server asynchronously.
        /// </summary>
        private static async Task<bool> CheckFileExistsAsync(string fileName, (string server, string username, string password) ftpDetails)
        {
            FtpWebRequest request = null;

            try
            {
                request = (FtpWebRequest)WebRequest.Create(ftpDetails.server);
                request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
                request.Credentials = new NetworkCredential(ftpDetails.username, ftpDetails.password);

                using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
                using (Stream responseStream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (IsFileMatch(line, fileName))
                        {
                            Console.WriteLine($"File {fileName} found on FTP server.");
                            return true;
                        }
                    }
                }
                Help.PrintRedLine($"File {fileName} not found on FTP server.");
                return false;
            }
            catch (WebException ex)
            {
                FtpWebResponse response = (FtpWebResponse)ex.Response;
                if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    Help.PrintRedLine($"File {fileName} not found on FTP server.");
                    return false;
                }
                else
                {
                    Help.PrintRedLine($"Error checking file {fileName} on FTP server: {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"Unexpected error checking file {fileName} on FTP server: {ex.Message}");
                throw;
            }
            finally
            {
                request?.Abort();
            }
        }

        /// <summary>
        /// Checks if a line from the FTP server directory listing matches the specified file name.
        /// </summary>
        private static bool IsFileMatch(string line, string fileName)
        {
            bool isDirectory = line.StartsWith("d");
            string[] tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string name = tokens[tokens.Length - 1];

            return !isDirectory && StringComparer.OrdinalIgnoreCase.Equals(name, fileName);
        }

        /// <summary>
        /// Gets the FTP details (server, username, password) based on the manufacturer.
        /// </summary>
        private static (string server, string username, string password) GetFtpDetails(string mfr)
        {
            return mfr == Mfrs.H ? (ftpServerMFRH, usernameMFRH, passwordMFRH) : (ftpServerMFRE, usernameMFRE, passwordMFRE);
        }

        /// <summary>
        /// Löscht eine Datei auf dem FTP-Server.
        /// </summary>
        /// <returns>True, wenn die Datei erfolgreich gelöscht wurde, andernfalls False.</returns>
        private async Task<bool> DeleteFileFromFtpServerAsync(string mfr, string fileName)
        {
            FtpWebRequest request = null;
            try
            {
                // Abrufen der FTP-Details basierend auf dem Hersteller
                var ftpDetails = GetFtpDetails(mfr);

                // Erstellen des FTP-WebRequests zum Löschen der Datei
                request = (FtpWebRequest)WebRequest.Create($"{ftpDetails.server}/{fileName}");
                request.Method = WebRequestMethods.Ftp.DeleteFile;
                request.Credentials = new NetworkCredential(ftpDetails.username, ftpDetails.password);

                // Ausführen des Requests und Abrufen der Antwort
                using (FtpWebResponse response = (FtpWebResponse)await request.GetResponseAsync())
                {
                    Console.WriteLine($"Successfully deleted {fileName} from {ftpDetails.server}. Status: {response.StatusDescription}");
                    return true;
                }
            }
            catch (WebException ex)
            {
                FtpWebResponse response = (FtpWebResponse)ex.Response;
                if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    Help.PrintRedLine($"File {fileName} not found on FTP server. Cannot delete.");
                }
                else
                {
                    Help.PrintRedLine($"Error deleting file {fileName} from FTP server: {ex.Message}");
                }
                return false;
            }
            catch (Exception ex)
            {
                Help.PrintRedLine($"Unexpected error deleting file {fileName} from FTP server: {ex.Message}");
                return false;
            }
            finally
            {
                request?.Abort();
            }
        }
    }
}
