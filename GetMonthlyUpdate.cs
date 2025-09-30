using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProjectUP.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace ProjectUP;

public class GetMonthlyUpdate
{
    private readonly ILogger<GetMonthlyUpdate> _logger;
    private readonly IConfiguration _configuration;

    public GetMonthlyUpdate(ILogger<GetMonthlyUpdate> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("GetMonthlyUpdate")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        _logger.LogInformation("GetMonthlyUpdate 함수 실행 시작");
        string body = string.Empty;

        // 쿼리 파라미터 추출
        string startDate = req.Query["StartDate"];
        string endDate = req.Query["EndDate"];

        // 기본값 설정
        if (string.IsNullOrEmpty(startDate))
            startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd");
        
        if (string.IsNullOrEmpty(endDate))
            endDate = DateTime.Now.AddDays(2).ToString("yyyy-MM-dd");

        _logger.LogInformation($"조회 기간: {startDate} ~ {endDate}");

        // 데이터베이스 연결 문자열 가져오기
        string connectionString = _configuration.GetConnectionString("SqlConnectionString");
        var updateNewsItems = new List<AzUpdateNews>();

        try
        {
            // 데이터베이스 접속 및 쿼리 실행 - 비동기 패턴 적용
            await using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                
                string queryText = "SELECT * FROM dbo.AzUpdateNews_EN WHERE DT BETWEEN @StartDate AND @EndDate";
                
                await using (SqlCommand command = new SqlCommand(queryText, connection))
                {
                    // 파라미터 추가
                    command.Parameters.Add("@StartDate", SqlDbType.Date).Value = startDate;
                    command.Parameters.Add("@EndDate", SqlDbType.Date).Value = endDate;
                    
                    // 쿼리 실행 및 결과 읽기
                    await using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var item = new AzUpdateNews
                            {
                                Title = reader["Title"] != DBNull.Value ? reader.GetString(reader.GetOrdinal("Title")) : string.Empty,
                                Description = reader["Description"] != DBNull.Value ? reader.GetString(reader.GetOrdinal("Description")) : string.Empty,
                                Link = reader["Link"] != DBNull.Value ? reader.GetString(reader.GetOrdinal("Link")) : string.Empty,
                                Category = reader["Category"] != DBNull.Value ? reader.GetString(reader.GetOrdinal("Category")) : string.Empty,
                                PubDate = reader["PubDate"] != DBNull.Value ? reader.GetString(reader.GetOrdinal("PubDate")) : string.Empty
                            };
                            
                            updateNewsItems.Add(item);
                        }
                    }
                }
            }

            // 결과가 없는 경우 처리
            if (updateNewsItems.Count == 0)
            {
                _logger.LogInformation("조회 결과가 없습니다.");
                return new OkObjectResult("해당 기간에 업데이트 정보가 없습니다.");
            }

            string itemTemplate = Utils.GetItemTemplate();

            // 조회된 결과로 HTML 생성
            foreach (AzUpdateNews updateItem in updateNewsItems)
            {
                // HTML 본문 생성
                body += string.Format(itemTemplate,
                    updateItem.Link,
                    updateItem.Title,
                    updateItem.Description,
                    updateItem.Category,
                    updateItem.PubDate);
            }

            _logger.LogInformation($"총 {updateNewsItems.Count}개의 업데이트 항목을 조회했습니다.");

            // html 문자열 완성하기
            string htmlTemp = $"<html>{{0}}<body>{{1}}</body></html>";
            string head = Utils.GetHeadAndStyle();
            string html = string.Format(htmlTemp, head, body);

            return new ContentResult { Content = html, ContentType = "text/html" };
            //return new OkObjectResult(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "데이터베이스 조회 중 오류가 발생했습니다.");
            return new ObjectResult($"오류가 발생했습니다: {ex.Message}")
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}