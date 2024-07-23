using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Hangfire;
using Hangfire.SqlServer;
using System.Threading.Tasks;

namespace JobQueueApp
{
    class Program
    {
        // Use the service name "db" as the server name in the connection string
        private static readonly string connectionString = "Server=host.docker.internal,1433;Database=TestDb;User Id=myuser;Password=MyStrongPassword;";

        static void Main(string[] args)
        {
            GlobalConfiguration.Configuration
                .UseSqlServerStorage(connectionString);

            RecurringJob.AddOrUpdate(() => EnqueueJobs(), Cron.Minutely);

            using (var server = new BackgroundJobServer())
            {
                Console.WriteLine("Hangfire Server started. Press any key to exit...");
                Console.ReadKey();
            }
        }

        public static void EnqueueJobs()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand("SELECT JobId FROM Jobs WHERE Status = @Status", connection))
                {
                    command.Parameters.Add(new SqlParameter("@Status", "Pending"));
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int jobId = reader.GetInt32(0);
                            BackgroundJob.Enqueue(() => ProcessJob(jobId, Environment.MachineName));
                        }
                    }
                }
            }
        }

        [AutomaticRetry(Attempts = 3)]
        public static void ProcessJob(int jobId, string workerName)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = new SqlCommand("UPDATE Jobs SET Status = @Status, ProcessedBy = @ProcessedBy WHERE JobId = @JobId", connection))
                {
                    command.Parameters.Add(new SqlParameter("@Status", "Processing"));
                    command.Parameters.Add(new SqlParameter("@ProcessedBy", workerName));
                    command.Parameters.Add(new SqlParameter("@JobId", jobId));
                    command.ExecuteNonQuery();
                }

                Console.WriteLine($"Processing job {jobId} by worker {workerName}");

                // Simulate job processing
                Task.Delay(2000).Wait();

                using (SqlCommand command = new SqlCommand("UPDATE Jobs SET Status = @Status WHERE JobId = @JobId", connection))
                {
                    command.Parameters.Add(new SqlParameter("@Status", "Completed"));
                    command.Parameters.Add(new SqlParameter("@JobId", jobId));
                    command.ExecuteNonQuery();
                }

                Console.WriteLine($"Completed job {jobId} by worker {workerName}");
            }
        }
    }
}
