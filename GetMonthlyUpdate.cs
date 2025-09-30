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
        _logger.LogInformation("GetMonthlyUpdate �Լ� ���� ����");
        string body = string.Empty;

        // ���� �Ķ���� ����
        string startDate = req.Query["StartDate"];
        string endDate = req.Query["EndDate"];

        // �⺻�� ����
        if (string.IsNullOrEmpty(startDate))
            startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd");
        
        if (string.IsNullOrEmpty(endDate))
            endDate = DateTime.Now.AddDays(2).ToString("yyyy-MM-dd");

        _logger.LogInformation($"��ȸ �Ⱓ: {startDate} ~ {endDate}");

        // �����ͺ��̽� ���� ���ڿ� ��������
        string connectionString = _configuration.GetConnectionString("SqlConnectionString");
        var updateNewsItems = new List<AzUpdateNews>();

        try
        {
            // �����ͺ��̽� ���� �� ���� ���� - �񵿱� ���� ����
            await using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                
                string queryText = "SELECT * FROM dbo.AzUpdateNews_EN WHERE DT BETWEEN @StartDate AND @EndDate";
                
                await using (SqlCommand command = new SqlCommand(queryText, connection))
                {
                    // �Ķ���� �߰�
                    command.Parameters.Add("@StartDate", SqlDbType.Date).Value = startDate;
                    command.Parameters.Add("@EndDate", SqlDbType.Date).Value = endDate;
                    
                    // ���� ���� �� ��� �б�
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

            // ����� ���� ��� ó��
            if (updateNewsItems.Count == 0)
            {
                _logger.LogInformation("��ȸ ����� �����ϴ�.");
                return new OkObjectResult("�ش� �Ⱓ�� ������Ʈ ������ �����ϴ�.");
            }

            string itemTemplate = Utils.GetItemTemplate();

            // ��ȸ�� ����� HTML ����
            foreach (AzUpdateNews updateItem in updateNewsItems)
            {
                // HTML ���� ����
                body += string.Format(itemTemplate,
                    updateItem.Link,
                    updateItem.Title,
                    updateItem.Description,
                    updateItem.Category,
                    updateItem.PubDate);
            }

            _logger.LogInformation($"�� {updateNewsItems.Count}���� ������Ʈ �׸��� ��ȸ�߽��ϴ�.");

            // html ���ڿ� �ϼ��ϱ�
            string htmlTemp = $"<html>{{0}}<body>{{1}}</body></html>";
            string head = Utils.GetHeadAndStyle();
            string html = string.Format(htmlTemp, head, body);

            return new ContentResult { Content = html, ContentType = "text/html" };
            //return new OkObjectResult(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "�����ͺ��̽� ��ȸ �� ������ �߻��߽��ϴ�.");
            return new ObjectResult($"������ �߻��߽��ϴ�: {ex.Message}")
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}