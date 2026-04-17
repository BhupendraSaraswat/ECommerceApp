using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ECommerce.Shared.Infrastructure.Data
{
    // ✅ Base Repository — sab repositories isko inherit karengi
    // Common ADO.NET helper methods yahan hain
    public abstract class BaseRepository
    {
        protected readonly IDbConnectionFactory _connectionFactory;
        protected readonly ILogger _logger;

        protected BaseRepository(
            IDbConnectionFactory connectionFactory,
            ILogger logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        // ✅ SELECT query — ek ya zyada rows return karta hai
        protected async Task<List<T>> QueryAsync<T>(
            string sql,
            Func<SqlDataReader, T> mapper,
            SqlParameter[]? parameters = null)
        {
            var results = new List<T>();

            using var connection = await _connectionFactory.CreateOpenConnectionAsync();
            using var command = new SqlCommand(sql, connection);

            if (parameters != null)
                command.Parameters.AddRange(parameters);

            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                results.Add(mapper(reader));
            }

            return results;
        }

        // ✅ SELECT query — sirf ek row return karta hai
        protected async Task<T?> QuerySingleAsync<T>(
            string sql,
            Func<SqlDataReader, T> mapper,
            SqlParameter[]? parameters = null)
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync();
            using var command = new SqlCommand(sql, connection);

            if (parameters != null)
                command.Parameters.AddRange(parameters);

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
                return mapper(reader);

            return default;
        }

        // ✅ INSERT / UPDATE / DELETE — kitni rows affect huin return karta hai
        protected async Task<int> ExecuteAsync(
            string sql,
            SqlParameter[]? parameters = null)
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync();
            using var command = new SqlCommand(sql, connection);

            if (parameters != null)
                command.Parameters.AddRange(parameters);

            return await command.ExecuteNonQueryAsync();
        }

        // ✅ INSERT — naya ID return karta hai (SCOPE_IDENTITY)
        protected async Task<Guid> ExecuteInsertAsync(
            string sql,
            SqlParameter[]? parameters = null)
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync();
            using var command = new SqlCommand(sql + "; SELECT CAST(SCOPE_IDENTITY() AS UNIQUEIDENTIFIER);", connection);

            if (parameters != null)
                command.Parameters.AddRange(parameters);

            var result = await command.ExecuteScalarAsync();
            return result != null ? (Guid)result : Guid.Empty;
        }

        // ✅ Ek single value return karta hai — COUNT, SUM, etc.
        protected async Task<T?> ExecuteScalarAsync<T>(
            string sql,
            SqlParameter[]? parameters = null)
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync();
            using var command = new SqlCommand(sql, connection);

            if (parameters != null)
                command.Parameters.AddRange(parameters);

            var result = await command.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return default;

            return (T)Convert.ChangeType(result, typeof(T));
        }

        // ✅ Transaction ke saath multiple queries — sab ek saath commit ya rollback
        protected async Task ExecuteInTransactionAsync(
            Func<SqlConnection, SqlTransaction, Task> operations)
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                await operations(connection, transaction);
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Transaction rolled back due to error");
                throw;
            }
        }

        // ✅ NULL check helper — ADO.NET mein DBNull.Value aata hai
        protected static T? GetValueOrDefault<T>(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return default;

            return (T)reader.GetValue(ordinal);
        }

        // ✅ Safe string read
        protected static string GetString(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        // ✅ Safe Guid read
        protected static Guid GetGuid(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? Guid.Empty : reader.GetGuid(ordinal);
        }

        // ✅ Safe DateTime read
        protected static DateTime GetDateTime(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? DateTime.MinValue : reader.GetDateTime(ordinal);
        }

        // ✅ Safe decimal read
        protected static decimal GetDecimal(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0m : reader.GetDecimal(ordinal);
        }

        // ✅ Safe int read
        protected static int GetInt(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }

        // ✅ Safe bool read
        protected static bool GetBool(SqlDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
        }
    }
}