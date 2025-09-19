using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using FuncHttp = Microsoft.Azure.Functions.Worker.Http;

namespace AzUpdate
{
    // AzUpdateNews moved to Models/AzUpdateNews.cs

    public class AzUpdateFunction
    {
        private readonly ILogger<AzUpdateFunction> _logger;
        readonly ChromeOptions chromeOptions;

        public AzUpdateFunction(ILogger<AzUpdateFunction> logger)
        {
            _logger = logger;

            chromeOptions = new ChromeOptions();
            chromeOptions.AddArguments(
                "--headless",
                "--no-sandbox",
                "--disable-gpu",
                "--whitelisted-ips"
            );
        }

        // 초기화 함수 (wran-up용)
        [Function("Init")]
        public IActionResult Init([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            return new OkObjectResult("Function Initialized");
        }

        // Azure 업데이트 정보를 RSS Feed에서 읽어와 HTML로 반환
        [Function("GetUpdateHTMLOnly")]
        public async Task<IActionResult> GetUpdateHTMLOnly(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            string url = "https://azure.microsoft.com/ko-kr/updates?id=498166";
            int waitingDuration = 2000;
            int targetHour = 24;

            // 수정된 코드 (CS8600 방지)
            string? u = req.Query.TryGetValue("URL", out var urlValue) ? urlValue.ToString() : null;
            if (!string.IsNullOrEmpty(u))
            {
                url = u;
            }
            string? w = req.Query.TryGetValue("WaitingDuration", out var waitingValue) ? waitingValue.ToString() : null;
            if (!string.IsNullOrEmpty(w))
            {
                waitingDuration = int.Parse(w);
            }
            string? h = req.Query.TryGetValue("Hour", out var hourValue) ? hourValue.ToString() : null;
            if (!string.IsNullOrEmpty(h))
            {
                targetHour = int.Parse(h);
            }

            string htmlTemp = $"<html>{{0}}<body>{{1}}</body></html>";

            // 스타일이 포함된 HTML 템플릿
            string head = GetHeadAndStyle();
            head = head.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("  ", "");

            // RSS에서 피드에서 AzUpdateNews으로 변환하여 필요한 정보만 가져오기
            List<AzUpdateNews> itemList = GetAzUpdateNewsListFromRssFeed(RssFeed.AzureUpdate, targetHour);

            string itemTemplate = $@"<div class='update-item'>
                                        <div class='update-title'><b><a href='{{0}}'>{{1}}</a></b></div>
                                        <ul class='update-details'>
                                            <li style='border-left-color: #ff6b6b'><strong>Description:</strong>{{2}}</li>
                                            <li style='border-left-color: #4ecdc4'><strong>Category:</strong>{{3}}</li>
                                            <li style='border-left-color: #45b7d1'><strong>Publication Date:</strong>{{4}}(UTC)</li>
                                        </ul>
                                    </div>";
            string body = string.Empty;


            List<AzUpdateNews> dbItems = new List<AzUpdateNews>();

            // List<AzUpdateNews>를 루프돌면서 (1)동적 컨텐츠 읽어오고, (2)HTML 스니펫 생성, (3)DB 저장용 리스트에 추가
            foreach (AzUpdateNews updateItem in itemList)
            {
                //임시로 막아두고, V1에서 주석을 풀기만 하면 됨. 밑의 함수에서도 한줄 제거
                //updateItem.Description = this.GetContentsFromWebSite(updateItem.Link, waitingDuration);
                updateItem.Title = ReplaceBadgeText(updateItem.Title);

                body += string.Format(itemTemplate,
                    updateItem.Link,
                    updateItem.Title,
                    updateItem.Description,
                    updateItem.Category,
                    updateItem.PubDate);
            }

            //본문이 없는 경우, 업데이트 없다는 문장으로 대체
            if (body.Trim().Length > 0)
            {
                body = body.Replace("\r", "")
                           .Replace("\n", "")
                           .Replace("\t", "")
                           .Replace("  ", "")
                           .Replace("\"", "'");
            }
            else
            {
                //업데이트가 없으면 그냥 빈 문자열로 보내달라는 요청에 따라 주석처리
                //body = $@"<div class='update-item'><div class='update-title'><b>No Update Today</b></div></div>";
            }

            // html 문자열 완성하기
            string html = string.Format(htmlTemp, head, body);

            // MultiResponse로 HTTP 응답과 SQL Output 반환
            return new OkObjectResult(body);
        }

       [Function("GetUpdate")]
        public async Task<MultiResponse> GetUpdateWithDB(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] FuncHttp.HttpRequestData req)
        {
            int waitingDuration = 2000;
            int targetHour = 24;

            FuncHttp.HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");

            var query = HttpUtility.ParseQueryString(req.Url.Query);

            // Now you can access query parameters by name
            string? w = query["WaitingDuration"];
            string? h = query["Hour"];

            if (!string.IsNullOrEmpty(w)) waitingDuration = int.Parse(w);
            if (!string.IsNullOrEmpty(h)) targetHour = int.Parse(h);

            string htmlTemp = $"<html>{{0}}<body>{{1}}</body></html>";

            // 스타일이 포함된 HTML 템플릿
            string head = GetHeadAndStyle();
            head = head.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("  ", "");

            // RSS에서 피드에서 AzUpdateNews으로 변환하여 필요한 정보만 가져오기
            List<AzUpdateNews> itemList = GetAzUpdateNewsListFromRssFeed(RssFeed.AzureUpdate, targetHour);

            string itemTemplate = $@"<div class='update-item'>
                                        <div class='update-title'><b><a href='{{0}}'>{{1}}</a></b></div>
                                        <ul class='update-details'>
                                            <li style='border-left-color: #ff6b6b'><strong>Description:</strong>{{2}}</li>
                                            <li style='border-left-color: #4ecdc4'><strong>Category:</strong>{{3}}</li>
                                            <li style='border-left-color: #45b7d1'><strong>Publication Date:</strong>{{4}}(UTC)</li>
                                        </ul>
                                    </div>";
            string body = string.Empty;

            List<AzUpdateNews> dbItems = new List<AzUpdateNews>();
            try
            {
                // List<AzUpdateNews>를 루프돌면서 (1)동적 컨텐츠 읽어오고, (2)HTML 스니펫 생성
                foreach (AzUpdateNews updateItem in itemList)
                {
                    string? desc = updateItem.Description;
                    updateItem.Description = this.GetContentsFromWebSite(updateItem.Link, waitingDuration);
                    string? title = updateItem.Title = ReplaceBadgeText(updateItem.Title);

                    //SQL DB에 저장하는 Title은 일부 단어들을 한국어로 변환
                    updateItem.Title = ReplaceBadgeTextToKorean(updateItem.Title);
                    // DB 저장용 리스트에 추가
                    dbItems.Add(updateItem);
                    _logger.LogInformation($"AzUpdateNews prepared for database: {updateItem.Title}");

                    // HTML 본문에는 원래 Description 사용.
                    //TODO : V1에서 아래 한줄제거
                    updateItem.Description = desc;  
                    updateItem.Title = title;

                    // HTML 본문 생성
                    body += string.Format(itemTemplate,
                        updateItem.Link,
                        updateItem.Title,
                        updateItem.Description,
                        updateItem.Category,
                        updateItem.PubDate);
                }

                //본문이 없는 경우, 업데이트 없다는 문장으로 대체
                if (body.Trim().Length > 0)
                {
                    body = body.Replace("\r", "")
                               .Replace("\n", "")
                               .Replace("\t", "")
                               .Replace("  ", "")
                               .Replace("\"", "'");
                }
                else
                {
                    //업데이트가 없으면 그냥 빈 문자열로 보내달라는 요청에 따라 주석처리
                    //body = $@"<div class='update-item'><div class='update-title'><b>No Update Today</b></div></div>";
                }

                // html 문자열 완성하기
                string html = string.Format(htmlTemp, head, body);

                // HTTP 응답에 HTML 본문 쓰기
                await response.WriteStringAsync(body, Encoding.UTF8);

                // MultiResponse로 HTTP 응답과 SQL Output 반환
                return new MultiResponse()
                {
                    News = dbItems.ToArray(),
                    HttpResponseData = response
                };
            }
            catch (Exception ex)
            {
                // 오류 발생 시 로그 기록 및 오류 메시지 반환
                response.StatusCode = HttpStatusCode.InternalServerError;
                await response.WriteStringAsync(ex.Message);

                return new MultiResponse()
                {
                    News = dbItems.ToArray(),
                    HttpResponseData = response
                };
            }

            //TODO: 생각해보니 OkObjectResult(body) 할 때는 한글이 문제없는데, 여기서만 문제인거 같다.

        }

        public class MultiResponse
        {
            [SqlOutput("dbo.AzUpdateNews", connectionStringSetting: "SqlConnectionString")]
            public AzUpdateNews[]? News { get; set; }

            public FuncHttp.HttpResponseData? HttpResponseData { get; set; }
        }

        // CSS 스타일 포함된 head 태그 반환
        private string GetHeadAndStyle()
        {
            return @"<head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Azure Service Updates</title>
                <style>
                    * {
                        margin: 0;
                        padding: 0;
                        box-sizing: border-box;
                    }

                    body {
                        font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                        background: #f5f7fa;
                        min-height: 100vh;
                        padding: 0.5rem;
                        line-height: 1.6;
                        color: #333;
                    }

                    h1 {
                        text-align: center;
                        color: #2c3e50;
                        font-size: 2.5rem;
                        font-weight: 700;
                        margin-bottom: 3rem;
                    }

                    .update-item {
                        background: white;
                        border: 1px solid #e1e8ed;
                        border-radius: 12px;
                        margin-bottom: 1rem;
                        padding: 0;
                        overflow: hidden;
                        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
                    }

                    .update-title {
                        background: #f8f9fa;
                        padding: 0.5rem 1rem;
                        margin: 0;
                        border-bottom: 1px solid #e1e8ed;
                    }

                    .update-title b {
                        font-weight: 400;
                    }

                    .update-details {
                        padding: 0.5rem;
                        margin: 0;
                        list-style: none;
                        background: white;
                    }

                    .update-details > li {
                        font-size: 0.85rem;
                        background: #f8f9fa;
                        margin-bottom: 0rem;
                        padding: 0.5rem 1.5rem;
                        border-radius: 8px;
                        border-left: 4px solid transparent;
                    }
                    
                    li li {margin-left: 1.5rem;}

                    .status-badge {
                        color: white;
                        padding: 0.4rem 1rem;
                        border-radius: 20px;
                        font-size: 0.8rem;
                        font-weight: 500;
                        text-transform: uppercase;
                        letter-spacing: 0.5px;
                        border: none;
                        display: inline-block;
                        margin-right: 0.8rem;
                    }

                    .GA{
                        background: #28a745;
                    }

                    .dev {
                        background: #FFFF00;
                        color: black;
                    }

                    .preview {
                        background: #00c3ff;
                    }

                    .retirement {
                        background: #ff6b6b;
                    }

                    strong {
                        color: #2c3e50;
                        font-weight: 600;
                        margin-right: 0.5rem;
                    }

                    b a {
                        text-decoration: none ;
                        font-weight: 600;
                        color: #2c3e50;
                    }

                    a {
                        text-decoration: underline ;
                        color: #007acc;
                    }

                    span.main-title {
                        display: inline-block;
                        padding: 0.2rem 0.5rem;
                        font-weight: 500;
                        font-size: 1rem;
                        text-align: center;
                        position: relative;

                    }

                    .orange-text {
                        color: #ff8c00;
                    }

                    .silver-text {
                        color: #878686ad;
                        font-size: 0.9rem;
                    }

                    .se-text {
                        color: #04c977ad;
                        font-size: 0.9rem;
                        font-weight: 600;
                    }
                    /* adaptive design */
                    @media (max-width: 768px) {
                        body {
                            padding: 1rem;
                        }
            
                        h1 {
                            font-size: 2rem;
                            margin-bottom: 2rem;
                        }
            
                        .update-title, .update-details {
                            padding: 1rem;
                        }
            
                        .status-badge {
                            display: block;
                            margin-bottom: 0.5rem;
                            text-align: center;
                        }
                    }

                    /* scrollbar */
                    ::-webkit-scrollbar {
                        width: 8px;
                    }

                    ::-webkit-scrollbar-track {
                        background: #f1f1f1;
                    }

                    ::-webkit-scrollbar-thumb {
                        background: #c1c1c1;
                        border-radius: 4px;
                    }

                    ::-webkit-scrollbar-thumb:hover {
                        background: #a8a8a8;
                    }
                </style>
            </head>";
        }

        // GA, Preview, Dev, Retirement 뱃지 텍스트를 HTML로 변환
        private string ReplaceBadgeText(string text)
        {
            if (text.Contains("[In preview]"))
            {
                text = text.Replace("Public Preview:", "");
                //text = text.Replace("[In preview]", "<span class='status-badge preview'>미리 보기(Public Preview) </span>");
                text = text.Replace("[In preview]", "<span class='status-badge preview'>[In preview]</span>");

            }
            if (text.Contains("[In development]"))
            {
                text = text.Replace("Private Preview:", "");
                //text = text.Replace("[In development]", "<span class='status-badge dev'>개발 중(Private Preview)</span>");
                text = text.Replace("[In development]", "<span class='status-badge dev'>[In development]</span>");
            }

            if (text.Contains("[Launched]"))
            {
                text = text.Replace("Generally Available:", "");
                //text = text.Replace("[Launched]", "<span class='status-badge GA'>정식 출시(G.A)</span>");
                text = text.Replace("[Launched]", "<span class='status-badge GA'>[Launched]</span>");
            }
            if (text.Contains("Retirement:"))
            {
                //text = text.Replace("Retirement:", "<span class='status-badge retirement'>서비스 종료(Deprecated)</span>");
                text = text.Replace("Retirement:", "<span class='status-badge retirement'>Retirement:</span>");
            }
            return text;
        }

        private string ReplaceBadgeTextToKorean(string text)
        {
            return text
                .Replace("Description", "설명")
                .Replace("Retirement:", "지원 종료:")
                .Replace("[In development]", "미리보기(비공개)")
                .Replace("[In preview]", "미리보기(공개)");
                //.Replace("[Launched]", "정식 지원(GA)");
        }

        // Selenium 사용하여 동적 컨텐츠 읽어오기
        private string GetContentsFromWebSite(string link, int waitingDuration)
        {
            string description = string.Empty;
            try
            {
                _logger.LogInformation($"페이지 호출시작:{link}");
                var chromeOptions = new ChromeOptions();
                chromeOptions.BrowserVersion = "138.0.7204.183"; // Chrome 버전에 맞게 설정
                chromeOptions.AddArguments(
                    "--headless",
                    "--no-sandbox",
                    "--disable-gpu"                    
                );
                
                using IWebDriver driver = new ChromeDriver(chromeOptions);
                if (!string.IsNullOrEmpty(link))
                {
                    driver.Navigate().GoToUrl(link);
                }
                else
                {
                    description = "Link is null or empty";
                    return description;
                }

                // Wait for dynamic content to load
                System.Threading.Thread.Sleep(waitingDuration);

                string pageSource = driver.PageSource;
                driver.Quit();

                var doc = new HtmlDocument();
                doc.LoadHtml(pageSource);

                var descElement = doc.DocumentNode.SelectSingleNode("//div[@class='accordion-item col-xl-8']");
                description = descElement?.InnerHtml ?? "Element not found";
                _logger.LogInformation($"페이지 호출완료:{link}");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error: {ex.Message}");
            }

            return description;
        }

        enum RssFeed { AzureUpdate, Blog };
        // RSS Feed에서 AzUpdateNews 리스트로 변환
        List<AzUpdateNews> GetAzUpdateNewsListFromRssFeed(RssFeed feedType, int last)
        {
            List<AzUpdateNews> itemList = new List<AzUpdateNews>();
            AzUpdateNews item;
            string feedUri = feedType switch
            {
                RssFeed.AzureUpdate => "https://www.microsoft.com/releasecommunications/api/v2/azure/rss",
                RssFeed.Blog => "https://azure.microsoft.com/en-us/blog/feed/",
                _ => "",
            };
            DateTime now = DateTime.Now;
            now = now.AddMinutes(0 - now.Minute);
            now = now.AddSeconds(0 - now.Second);

            try
            {
                XmlDocument rssXmlDoc = new();
                rssXmlDoc.Load(feedUri);

                XmlNodeList? rssNodes = rssXmlDoc.SelectNodes("rss/channel/item");
                if (rssNodes != null)
                {
                    XmlNode? rssItemNode;
                    XmlNodeList? rssCategoryNodes;

                    //RSS Feed에서 필요한 정보들만 추출하여 List<AzUpdateNews>에 저장
                    foreach (XmlNode rssNode in rssNodes)
                    {
                        item = new AzUpdateNews();
                        rssItemNode = rssNode.SelectSingleNode("pubDate");
                        item.PubDate = rssItemNode != null ? rssItemNode.InnerText : "";
                        rssItemNode = rssNode.SelectSingleNode("title");
                        item.Title = rssItemNode != null ? rssItemNode.InnerText : "";

                        rssItemNode = rssNode.SelectSingleNode("link");
                        item.Link = rssItemNode != null ? rssItemNode.InnerText : "";

                        rssItemNode = rssNode.SelectSingleNode("description");
                        item.Description = rssItemNode != null ? rssItemNode.InnerText : "";

                        rssCategoryNodes = rssNode.SelectNodes("category");
                        if (rssCategoryNodes != null)
                        {
                            foreach (XmlNode itemCategory in rssCategoryNodes)
                            {
                                item.Category += itemCategory.InnerText.Trim() + ",";
                            }
                            item.Category = item.Category?.TrimEnd(',');
                        }

                        DateTime dtPubDate = Convert.ToDateTime(item.PubDate);
                        if ((now - dtPubDate).TotalHours > last)
                        {
                            break;
                        }

                        itemList.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.Message);
            }

            return itemList;
        }
    }
}
