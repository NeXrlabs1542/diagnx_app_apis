using Npgsql;

namespace DiagnX.Api.Data;

public static class ConnectionStrings
{
    /// <summary>
    /// Uses ConnectionStrings:Default, or DATABASE_URL in the postgres://user:pass@host:port/db form
    /// that Render (and Heroku-style hosts) provide.
    /// </summary>
    public static string Resolve(IConfiguration config)
    {
        var url = config["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(url) && (url.StartsWith("postgres://") || url.StartsWith("postgresql://")))
            return FromUrl(url);

        return config.GetConnectionString("Default")
               ?? throw new InvalidOperationException("Set ConnectionStrings__Default or DATABASE_URL.");
    }

    public static string FromUrl(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            Database = uri.AbsolutePath.TrimStart('/'),
            // Render's internal URLs (host without a dot) don't use TLS; external ones require it.
            SslMode = uri.Host.Contains('.') ? SslMode.Require : SslMode.Prefer,
            MaxPoolSize = 20,
        };
        return builder.ConnectionString;
    }
}
