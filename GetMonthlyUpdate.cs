using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProjectUP.Models;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

    [Function("GetUpdateByPeriod")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        _logger.LogInformation("GetMonthlyUpdate 함수 실행 시작");
        string body = string.Empty;
        string startDate = string.Empty;
        string endDate = string.Empty;

        // 쿼리 파라미터 추출
        string? period = req.Query["Period"];
        if (string.IsNullOrEmpty(period)) period = "Weekly";

        // 기본값 설정
        if (period == "Weekly")
            startDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
        if (period == "Monthly")
            startDate = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd");
        endDate = DateTime.Now.ToString("yyyy-MM-dd");

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

            string fileName = $"AzureUpdates_{period}_{startDate}.pdf";
            await CreateHtml2PDF(html, fileName);

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

    async Task CreateHtml2PDF(string html, string fileName)
    {
        _logger.LogInformation("PDF 생성 + Blob 업로드(Managed Identity) 시작");
        // Blob 설정
        string accountUrl = Environment.GetEnvironmentVariable("BlobAccountUrl") ?? throw new InvalidOperationException("AzureStorage 설정 누락");
        string container = Environment.GetEnvironmentVariable("BlobContainerName") ?? "pdf-files";

        // Managed Identity로 인증
        //    로컬 디버깅: 개발자 자격 증명 (VS/CLI) 사용 → 필요 시 Azure 로그인
        //    Azure에 배포: 함수앱의 시스템 할당 MI가 자동 사용됨
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = false,
            // 네트워크 격리된 환경에서 AuthorityHost가 다르면 여기 지정
        });

        var blobServiceClient = new BlobServiceClient(new Uri(accountUrl), credential);
        var containerClient = blobServiceClient.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
        var blobClient = containerClient.GetBlobClient(fileName);

        await new BrowserFetcher().DownloadAsync();
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox" } // 컨테이너이면 권장
        });
        var page = await browser.NewPageAsync();

        var pdfOptions = new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions { Top = "20mm", Bottom = "20mm", Left = "15mm", Right = "15mm" },
            DisplayHeaderFooter = false
        };

        // HTML 직접 로드(또는 page.GoToAsync(url))
        await page.SetContentAsync(html);

        //await page.PdfAsync("output.pdf", pdfOptions);
        ///await browser.CloseAsync();

        // 메모리 스트림으로 PDF 생성
        byte[] pdfBytes = await page.PdfDataAsync(pdfOptions);
        using var ms = new MemoryStream(pdfBytes);

        // Blob에 업로드(메모리에서 바로)
        await blobClient.UploadAsync(ms, overwrite: true);

        // ContentType 메타데이터 설정(선택)
        await blobClient.SetHttpHeadersAsync(new BlobHttpHeaders { ContentType = "application/pdf" });
        await browser.CloseAsync();

        _logger.LogInformation($"업로드 완료: {fileName}");
    }
}