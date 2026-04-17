using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Shared.Infrastructure.Data
{
    // ✅ Ye class SQL Server connection banati hai
    // Har service apna DbConnectionFactory register karegi
    public interface IDbConnectionFactory
    {
        SqlConnection CreateConnection();
        Task<SqlConnection> CreateOpenConnectionAsync();
    }

    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public DbConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection string is not configured in appsettings.json");
        }

        // ✅ Naya connection banao (closed state mein)
        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

        // ✅ Naya connection banao aur seedha open karo
        public async Task<SqlConnection> CreateOpenConnectionAsync()
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}