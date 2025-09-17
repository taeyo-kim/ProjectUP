using Microsoft.Extensions.Configuration;

namespace AzUpdate.Configuration
{
    /// <summary>
    /// SQL ���� ������ ���� �ɼ� Ŭ����
    /// </summary>
    public class SqlConnectionOptions
    {
        public const string SectionName = "SqlConnection";
        
        public string ConnectionString { get; set; } = string.Empty;
        public int CommandTimeout { get; set; } = 30;
        public int ConnectionTimeout { get; set; } = 30;
        public bool EnableRetryOnFailure { get; set; } = true;
        public int MaxRetryCount { get; set; } = 3;
    }

    /// <summary>
    /// ���� ���ڿ� ���� ��ƿ��Ƽ
    /// </summary>
    public static class ConnectionStringBuilder
    {
        /// <summary>
        /// Managed Identity�� ����ϴ� ���� ���ڿ� ����
        /// </summary>
        public static string BuildManagedIdentityConnectionString(string server, string database)
        {
            return $"Server={server};Database={database};Authentication=Active Directory Managed Identity;Encrypt=true;TrustServerCertificate=false;";
        }

        /// <summary>
        /// �����/��ȣ ��� ���� ���ڿ� ����
        /// </summary>
        public static string BuildUserPasswordConnectionString(string server, string database, string userId, string password)
        {
            return $"Server={server};Database={database};User ID={userId};Password={password};Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;";
        }

        /// <summary>
        /// ���� ���߿� ���� ���ڿ� ����
        /// </summary>
        public static string BuildLocalConnectionString(string server = "localhost", string database = "AzureUpdates")
        {
            return $"Server={server};Database={database};Integrated Security=true;TrustServerCertificate=true;";
        }
    }
}