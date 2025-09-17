using Microsoft.Extensions.Configuration;

namespace AzUpdate.Configuration
{
    /// <summary>
    /// SQL 연결 설정을 위한 옵션 클래스
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
    /// 연결 문자열 빌더 유틸리티
    /// </summary>
    public static class ConnectionStringBuilder
    {
        /// <summary>
        /// Managed Identity를 사용하는 연결 문자열 생성
        /// </summary>
        public static string BuildManagedIdentityConnectionString(string server, string database)
        {
            return $"Server={server};Database={database};Authentication=Active Directory Managed Identity;Encrypt=true;TrustServerCertificate=false;";
        }

        /// <summary>
        /// 사용자/암호 기반 연결 문자열 생성
        /// </summary>
        public static string BuildUserPasswordConnectionString(string server, string database, string userId, string password)
        {
            return $"Server={server};Database={database};User ID={userId};Password={password};Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;";
        }

        /// <summary>
        /// 로컬 개발용 연결 문자열 생성
        /// </summary>
        public static string BuildLocalConnectionString(string server = "localhost", string database = "AzureUpdates")
        {
            return $"Server={server};Database={database};Integrated Security=true;TrustServerCertificate=true;";
        }
    }
}