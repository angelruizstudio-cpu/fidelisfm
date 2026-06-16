using Microsoft.Data.SqlClient;

namespace CFS.Data;

public sealed class SqlConnectionFactory(string connectionString)
{
    private const string ConnectionName = "CfsDatabase";

    public SqlConnection Create()
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing connection string '{ConnectionName}'. Configure it in appsettings.Development.json or user secrets.");
        }

        return new SqlConnection(connectionString);
    }
}
