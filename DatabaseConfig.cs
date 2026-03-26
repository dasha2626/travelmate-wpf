using System;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Text;

namespace TravelMate
{
    public static class DatabaseConfig
    {
        public static readonly string AppDirectory;
        public static readonly string DatabasePath;
        public static readonly string ConnectionString;

        static DatabaseConfig()
        {
            AppDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TravelMate"
            );

            DatabasePath = Path.Combine(AppDirectory, "TravelMate.db");
            ConnectionString = $"Data Source={DatabasePath};Version=3;";
        }

        public static void EnsureDatabase()
        {
            if (!Directory.Exists(AppDirectory))
                Directory.CreateDirectory(AppDirectory);

            if (!File.Exists(DatabasePath))
            {
                SQLiteConnection.CreateFile(DatabasePath);

                using (var conn = new SQLiteConnection(ConnectionString))
                {
                    conn.Open();

               
                    string resourceName = "TravelMate.TravelMate.sqldasha.sql";

                    using (Stream stream =
                        Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream(resourceName))
                    {
                        if (stream == null)
                            throw new Exception("Nie znaleziono embedded SQL!");

                        ExecuteSqlFile(stream, conn);
                    }
                }
            }
        }

        public static void ExecuteSqlFile(Stream inputStream, SQLiteConnection dbConn)
        {
            bool begin = false;
            StringBuilder content = new StringBuilder();

            using (var reader = new StreamReader(inputStream, Encoding.UTF8))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.StartsWith("--") || line == "") continue;

                    content.AppendLine(line);

                    if (line.ToUpper().StartsWith("BEGIN") &&
                        !line.ToUpper().StartsWith("BEGIN TRANSACTION"))
                        begin = true;

                    bool end =
                        (!begin && line.EndsWith(";")) ||
                        (begin && line.ToUpper().EndsWith("END;"));

                    if (end)
                    {
                        using (var cmd = new SQLiteCommand(content.ToString(), dbConn))
                        {
                            cmd.ExecuteNonQuery();
                        }

                        content.Clear();
                        begin = false;
                    }
                }
            }
        }
    }
}
