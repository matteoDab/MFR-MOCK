using GALEDI.config;
using GALEDI.servs;
using MySql.Data.MySqlClient;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace GALEDI.SQL
{
    /// <summary>
    /// Data access class for interacting with the MySQL database.
    /// Responsible for reading from and writing to the database.
    /// </summary>
    internal class MySQL
    {
        private readonly string _connectionString;

        /// <summary>
        /// Constructor that accepts a connection string, allowing for dependency injection.
        /// </summary>
        public MySQL(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Opens a connection to the MySQL database and returns it.
        /// This method uses a 'using' statement to ensure the connection is properly managed.
        /// </summary>
        private MySqlConnection GetOpenConnection()
        {
            var connection = new MySqlConnection(_connectionString);
            connection.Open();
            return connection;
        }

        public async Task UpdateDatabaseAfterUploadAsync(string mfr, List<int> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                Console.WriteLine("No IDs provided to update.");
                return;
            }

            try
            {
                // SQL query using IN clause with parameters for each ID
                string query = $"UPDATE tbl_mfrx_int SET deleted = 1 WHERE mfr = @mfr AND id IN ({string.Join(",", ids)}) AND deleted != 1";

                using (var connection = new MySqlConnection(_connectionString))
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



        /// <summary>
        /// Asynchronously reads data from the 'tbl_mfrx_int' table based on the specified manufacturer.
        /// Returns a list of string arrays representing each row.
        /// </summary>
        public async Task<List<string[]>> ReadFromMFRXINTAsync(string mfr)
        {
            var result = new List<string[]>();

            try
            {
                using (var connection = GetOpenConnection())
                {
                    string query = "SELECT LE, plannedDestination, actualDestination, status, date, time FROM tbl_mfrx_int WHERE mfr = @mfr AND deleted != 1 ORDER BY timestamp ASC LIMIT 50";
                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@mfr", mfr);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new string[6];
                                row[0] = reader.GetInt64(0).ToString(); // LE
                                row[1] = reader.GetString(1); // plannedDestination
                                row[2] = reader.GetString(2); // actualDestination
                                row[3] = reader.GetInt32(3).ToString("D2"); // status
                                row[4] = reader.GetDateTime(4).ToString("dd.MM.yy"); // date
                                row[5] = ((TimeSpan)reader.GetValue(5)).ToString(@"hh\:mm\:ss"); // time (TimeSpan -> String)

                                result.Add(row);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Detailed error logging with SQL query and parameters for easier debugging
                Help.PrintRedLine($"Error reading from tbl_mfrx_int for mfr '{mfr}': {ex.Message}");
                Help.PrintRedLine($"Stack Trace: {ex.StackTrace}");
            }

            return result;
        }

        /// <summary>
        /// Processes a file and writes its content to the 'tbl_mfrx_int' table in the database.
        /// </summary>
        public void WriteToMFRXINT(string mfr, string filePath)
        {
            try
            {
                // Read all lines from the specified file
                string[] lines = File.ReadAllLines(filePath);

                using (var connection = GetOpenConnection())
                {
                    foreach (string line in lines)
                    {
                        // Skip empty lines
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        string[] parts = line.Split(',');
                        if (parts.Length != 6)
                        {
                            Help.PrintRedLine($"Invalid line format: {line}");
                            continue;
                        }

                        string le = parts[0];
                        string plannedDestination = parts[1];
                        string actualDestination = parts[2];
                        string status = parts[3];
                        string date = parts[4];
                        string time = parts[5];

                        // Generate hash value for data integrity
                        string rawData = string.Join(",", parts);
                        string hashValue = Help.Hash(rawData);

                        // SQL query for inserting data into the table
                        string query = "INSERT INTO tbl_mfrx_int (mfr, LE, plannedDestination, actualDestination, status, date, time, hashValue) " +
                                       "VALUES (@mfr, @LE, @plannedDestination, @actualDestination, @status, @date, @time, @hashValue)";

                        using (var command = new MySqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@mfr", mfr);
                            command.Parameters.AddWithValue("@LE", le);
                            command.Parameters.AddWithValue("@plannedDestination", plannedDestination);
                            command.Parameters.AddWithValue("@actualDestination", actualDestination);
                            command.Parameters.AddWithValue("@status", status);
                            command.Parameters.AddWithValue("@date", DateTime.ParseExact(date, "dd.MM.yy", null).ToString("yyyy-MM-dd"));
                            command.Parameters.AddWithValue("@time", time);
                            command.Parameters.AddWithValue("@hashValue", hashValue);

                            try
                            {
                                command.ExecuteNonQuery();
                                Console.WriteLine($"Record inserted successfully: {rawData}");
                                // Small delay to prevent duplicated Key (timestamp is PK)
                                Task.Delay(1);
                            }
                            catch (MySqlException ex)
                            {
                                if (ex.Number == 1062) // Duplicate entry error code
                                {
                                    Help.PrintRedLine($"Duplicate entry detected. Record not inserted: {rawData}");
                                }
                                else
                                {
                                    Help.PrintRedLine($"Error inserting record: {ex.Message}");
                                    Help.PrintRedLine($"SQL Query: {query}");
                                    Help.PrintRedLine($"Stack Trace: {ex.StackTrace}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during file processing or database insertion
                Help.PrintRedLine($"An error occurred while writing to tbl_mfrx_int: {ex.Message}");
                Help.PrintRedLine($"Stack Trace: {ex.StackTrace}");
            }
            finally
            {
                // Ensure the temporary file is deleted
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
    }
}
