using Npgsql;

namespace Hrms.Infrastructure;

public static class PostgresConnectionString
{
    public static string Normalize(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;

        var uri = new Uri(value);
        var credentials = uri.UserInfo.Split(':', 2);
        if (credentials.Length != 2 || string.IsNullOrWhiteSpace(uri.Host) || string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')))
            throw new InvalidOperationException("The PostgreSQL URL is missing a username, password, host, or database name.");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1]),
            Database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/')),
            ApplicationName = "PeopleFlow HRMS"
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]).Replace('_', ' ');
            var queryValue = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase)) key = "SSL Mode";
            if (key.Equals("channel binding", StringComparison.OrdinalIgnoreCase)) key = "Channel Binding";
            if (key.Equals("application name", StringComparison.OrdinalIgnoreCase)) key = "Application Name";
            if (key is "SSL Mode" or "Channel Binding" or "Application Name") builder[key] = queryValue;
        }

        return builder.ConnectionString;
    }
}
