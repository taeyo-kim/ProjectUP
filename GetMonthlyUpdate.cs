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

    // 메뉴얼로 이 함수 호출하면 월간 파일이 생성됨 인자는 Monthly
    // Period 인자에 Weekly 또는 Monthly 입력 가능 (대소문자 무시)
    [Function("GetUpdateByPeriod")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        _logger.LogInformation("GetMonthlyUpdate 함수 실행 시작");
        string body = string.Empty;
        string startDate = string.Empty;
        string endDate = string.Empty;
        string title = string.Empty; //월간, 주간 
        bool createFile = true;

        // 쿼리 파라미터 추출 (대소문자 무시)
        string? period = req.Query["Period"].FirstOrDefault() ?? req.Query["period"].FirstOrDefault();
        if (string.IsNullOrEmpty(period)) period = "Weekly";

        //파일 생성 인자 (기본은 True)
        string? f = req.Query.TryGetValue("CreateFile", out var createFileValue) ? createFileValue.ToString() : null;
        if (!string.IsNullOrEmpty(f) && bool.TryParse(f, out bool fileResult))
        {
            createFile = fileResult;
        }

        _logger.LogInformation($"Period 파라미터: {period}, CreateFile: {createFile}");

        // 기본값 설정 (대소문자 무시 비교)
        if (string.Equals(period, "Weekly", StringComparison.OrdinalIgnoreCase))
        {
            title = "주간";
            startDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
            endDate = DateTime.Now.ToString("yyyy-MM-dd");
        }
        else if (string.Equals(period, "Monthly", StringComparison.OrdinalIgnoreCase))
        {
            title = "월간";
            startDate = new DateTime(DateTime.Now.Year, DateTime.Now.AddMonths(-1).Month, 1).ToString("yyyy-MM-dd");
            endDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");
        }
        //https://projectupfunc.azurewebsites.net/api/GetUpdateByPeriod?period=Monthly

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

                string queryText = "SELECT DISTINCT DT, Title, Link, Description, Category, PubDate  FROM dbo.AzUpdateNews_KO WHERE DT BETWEEN @StartDate AND @EndDate";

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

            string infoHeader = $"<div class='update-item' "
                + "style=\"text-align: center; height: 400; padding-top: 50;\">"
                + "<h1><img src=\"https://mivoes.blob.core.windows.net/images/azure.jpg\" width=\"50\" style=\"vertical-align: middle;\" /> Azure "
                + title + " 업데이트 <h1>"
                + "<h2><img src=\"https://mivoes.blob.core.windows.net/images/bell.png\" width=\"30\" style=\"vertical-align: middle;\" />"
                + startDate.Replace("-",".") + " ~ " + endDate.Replace("-",".") + "</h2>"
                + "<span class=\"main-title\" title=\"SE 김태영, 성지용\" style=\"padding-top: 100px;\">Project <span class=\"orange-text\" title=\"SE 김태영, 성지용\">UP</span><span class=\"silver-text\">(date) by </span><span class=\"se-text\" title=\"SE 김태영, 성지용\">SE태지</span>"
                + "</div>";

            string footer = "";

            // html 문자열 완성하기
            string htmlTemp = $"<html>{{0}}<body>{{1}}{{2}}</body></html>";
            string head = Utils.GetHeadAndStyle();
            string html = string.Format(htmlTemp, head, infoHeader, body);

            if (createFile)
            {
                string fileName = $"AzureUpdates_{period}_{startDate}.pdf";
                await CreateHtml2PDF(html, fileName);

                return new ContentResult { Content = fileName, ContentType = "text/html" };
            }
            else
                return new OkObjectResult(html);
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
        _logger.LogInformation("PDF 생성 + Blob 업로드(SAS) 시작");
        
        // Blob 설정
        string accountUrl = Environment.GetEnvironmentVariable("BlobAccountUrl") ?? throw new InvalidOperationException("BlobAccountUrl 설정 누락");
        //string sasUrl = Environment.GetEnvironmentVariable("BlobSasUrl") ?? throw new InvalidOperationException("BlobSasUrl 설정 누락");
        string container = Environment.GetEnvironmentVariable("BlobContainerName") ?? "pdf-files";

        // SAS URL을 사용하여 BlobServiceClient 생성
        // SAS URL 형식: https://<account>.blob.core.windows.net/?<sas-token>

        // Managed Identity로 인증
        // - 로컬 디버깅: Azure CLI 또는 Visual Studio 자격 증명 사용
        // - Azure 배포: 함수앱의 시스템 할당 또는 사용자 할당 Managed Identity 사용
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = false,
            ExcludeSharedTokenCacheCredential = false,
            ExcludeVisualStudioCredential = false,
            ExcludeAzureCliCredential = false,
        });

        // MI로 BlobServiceClient 생성
        var blobServiceClient = new BlobServiceClient(new Uri(accountUrl), credential);

        // SAS URL로 BlobServiceClient 생성
        //var blobServiceClient = new BlobServiceClient(new Uri(sasUrl));
        var containerClient = blobServiceClient.GetBlobContainerClient(container);
        //await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
        var blobClient = containerClient.GetBlobClient(fileName);

        await new BrowserFetcher().DownloadAsync();
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] 
            { 
                "--no-sandbox",
                "--disable-setuid-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--lang=ko-KR",
                "--font-render-hinting=none",  // 폰트 렌더링 개선
                "--disable-web-security"  // CORS 이슈 방지 (폰트 로딩)
            },
            ExecutablePath = "/usr/bin/google-chrome-stable"  // Dockerfile에 설치된 Chrome 사용
        });
        var page = await browser.NewPageAsync();

        // 페이지 언어 설정
        await page.SetExtraHttpHeadersAsync(new Dictionary<string, string>
        {
            { "Accept-Language", "ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7" }
        });

        var pdfOptions = new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions { Top = "20mm", Bottom = "20mm", Left = "15mm", Right = "15mm" },
            DisplayHeaderFooter = false,
            PreferCSSPageSize = false,
            OmitBackground = false  // 배경색 유지
        };

        // HTML 직접 로드(또는 page.GoToAsync(url))
        await page.SetContentAsync(html, new NavigationOptions 
        { 
            WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
            Timeout = 30000  // 30초 타임아웃
        });

        // 폰트 로딩 대기 (중요!)
        await Task.Delay(2000);  // Google Fonts 로딩 대기
        await page.EvaluateExpressionAsync("document.fonts.ready");
        
        _logger.LogInformation("폰트 로딩 완료");

        ////로컬에 테스트로 파일 생성하는 코드
        //await page.PdfAsync("output.pdf", pdfOptions);
        //await browser.CloseAsync();

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