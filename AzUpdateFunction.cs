using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using ProjectUP.Models;
using System.Net;
using System.Text;
using System.Web;
using System.Xml;
using FuncHttp = Microsoft.Azure.Functions.Worker.Http;

namespace ProjectUP
{
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
        [Function("GetNews")]
        public async Task<IActionResult> GetNews(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            string url = "https://azure.microsoft.com/ko-kr/updates?id=498166";
            int waitingDuration = 2000;
            int targetHour = 24;

            string body = string.Empty;
            List<AzUpdateNews> dbItems = new List<AzUpdateNews>();

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
            string head = Utils.GetHeadAndStyle();
            head = head.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("  ", "");

            // RSS에서 피드에서 AzUpdateNews으로 변환하여 필요한 정보만 가져오기
            List<AzUpdateNews> itemList = GetAzUpdateNewsListFromRssFeed(RssFeed.AzureUpdate, targetHour);

            string itemTemplate = Utils.GetItemTemplate();          

            // List<AzUpdateNews>를 루프돌면서 (1)동적 컨텐츠 읽어오고, (2)HTML 스니펫 생성, (3)DB 저장용 리스트에 추가
            foreach (AzUpdateNews updateItem in itemList)
            {

                updateItem.Description = this.GetContentsFromWebSite(updateItem.Link, waitingDuration);
                updateItem.Title = Utils.ReplaceBadgeText(updateItem.Title);

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
        public async Task<MultiResponse> GetUpdate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] FuncHttp.HttpRequestData req)
        {
            /// 기본값 설정
            int waitingDuration = 3000;
            int targetHour = 24;
            string readingType = "FULL";  // or RSS

            /// 변수 초기화
            string body = string.Empty;
            List<AzUpdateNews> dbItems = new List<AzUpdateNews>();

            /// HTTP 응답 초기화
            FuncHttp.HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            /// 쿼리 문자열에서 매개변수 읽기
            var query = HttpUtility.ParseQueryString(req.Url.Query);

            /// 쿼리 문자열에서 매개변수 읽기
            string? w = query["WaitingDuration"];
            string? h = query["Hour"];
            string? t = query["ReadingType"];

            if (!string.IsNullOrEmpty(w)) waitingDuration = int.Parse(w);
            if (!string.IsNullOrEmpty(h)) targetHour = int.Parse(h);
            if (!string.IsNullOrEmpty(t)) readingType = t;

            string htmlTemp = $"<html>{{0}}<body>{{1}}</body></html>";

            /// 스타일이 포함된 HTML 템플릿
            string head = Utils.GetHeadAndStyle();
            head = head.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("  ", "");

            // RSS에서 피드에서 AzUpdateNews으로 변환하여 필요한 정보만 가져오기
            List<AzUpdateNews> itemList = GetAzUpdateNewsListFromRssFeed(RssFeed.AzureUpdate, targetHour);

            string itemTemplate = Utils.GetItemTemplate();
            try
            {
                // List<AzUpdateNews>를 루프돌면서 (1)동적 컨텐츠 읽어오고, (2)HTML 스니펫 생성
                foreach (AzUpdateNews updateItem in itemList)
                {
                    string? desc = updateItem.Description;
                    if (readingType == "FULL")
                    {
                        updateItem.Description = this.GetContentsFromWebSite(updateItem.Link, waitingDuration);
                    }
                    else  // RSS
                    {
                        updateItem.Description = desc;
                    }

                    updateItem.Title = Utils.ReplaceBadgeText(updateItem.Title);

                    //SQL DB에 저장하는 Title은 일부 단어들을 한국어로 변환
                    //그런데, 한글이 깨지는 현상을 해결하기 힘들어서 주석처리하고 그냥 영문으로 보냄.
                    //updateItem.Title = ReplaceBadgeTextToKorean(updateItem.Title);
                    // DB 저장용 리스트에 추가
                    dbItems.Add(updateItem);
                    _logger.LogInformation($"AzUpdateNews prepared for database: {updateItem.Title}");

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
        }

        public class MultiResponse
        {
            [SqlOutput("dbo.AzUpdateNews_En", connectionStringSetting: "SqlConnectionString")]
            public AzUpdateNews[]? News { get; set; }

            public FuncHttp.HttpResponseData? HttpResponseData { get; set; }
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
