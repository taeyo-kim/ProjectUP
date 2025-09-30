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

        // �ʱ�ȭ �Լ� (wran-up��)
        [Function("Init")]
        public IActionResult Init([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            return new OkObjectResult("Function Initialized");
        }

        // Azure ������Ʈ ������ RSS Feed���� �о�� HTML�� ��ȯ
        [Function("GetNews")]
        public async Task<IActionResult> GetNews(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            string url = "https://azure.microsoft.com/ko-kr/updates?id=498166";
            int waitingDuration = 2000;
            int targetHour = 24;

            string body = string.Empty;
            List<AzUpdateNews> dbItems = new List<AzUpdateNews>();

            // ������ �ڵ� (CS8600 ����)
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

            // ��Ÿ���� ���Ե� HTML ���ø�
            string head = Utils.GetHeadAndStyle();
            head = head.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("  ", "");

            // RSS���� �ǵ忡�� AzUpdateNews���� ��ȯ�Ͽ� �ʿ��� ������ ��������
            List<AzUpdateNews> itemList = GetAzUpdateNewsListFromRssFeed(RssFeed.AzureUpdate, targetHour);

            string itemTemplate = Utils.GetItemTemplate();          

            // List<AzUpdateNews>�� �������鼭 (1)���� ������ �о����, (2)HTML ������ ����, (3)DB ����� ����Ʈ�� �߰�
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

            //������ ���� ���, ������Ʈ ���ٴ� �������� ��ü
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
                //������Ʈ�� ������ �׳� �� ���ڿ��� �����޶�� ��û�� ���� �ּ�ó��
                //body = $@"<div class='update-item'><div class='update-title'><b>No Update Today</b></div></div>";
            }

            // html ���ڿ� �ϼ��ϱ�
            string html = string.Format(htmlTemp, head, body);

            // MultiResponse�� HTTP ����� SQL Output ��ȯ
            return new OkObjectResult(body);
        }

        [Function("GetUpdate")]
        public async Task<MultiResponse> GetUpdate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] FuncHttp.HttpRequestData req)
        {
            /// �⺻�� ����
            int waitingDuration = 3000;
            int targetHour = 24;

            /// ���� �ʱ�ȭ
            string body = string.Empty;
            List<AzUpdateNews> dbItems = new List<AzUpdateNews>();

            /// HTTP ���� �ʱ�ȭ
            FuncHttp.HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            /// ���� ���ڿ����� �Ű����� �б�
            var query = HttpUtility.ParseQueryString(req.Url.Query);

            /// ���� ���ڿ����� �Ű����� �б�
            string? w = query["WaitingDuration"];
            string? h = query["Hour"];

            if (!string.IsNullOrEmpty(w)) waitingDuration = int.Parse(w);
            if (!string.IsNullOrEmpty(h)) targetHour = int.Parse(h);

            string htmlTemp = $"<html>{{0}}<body>{{1}}</body></html>";

            /// ��Ÿ���� ���Ե� HTML ���ø�
            string head = Utils.GetHeadAndStyle();
            head = head.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("  ", "");

            // RSS���� �ǵ忡�� AzUpdateNews���� ��ȯ�Ͽ� �ʿ��� ������ ��������
            List<AzUpdateNews> itemList = GetAzUpdateNewsListFromRssFeed(RssFeed.AzureUpdate, targetHour);

            string itemTemplate = Utils.GetItemTemplate();
            try
            {
                // List<AzUpdateNews>�� �������鼭 (1)���� ������ �о����, (2)HTML ������ ����
                foreach (AzUpdateNews updateItem in itemList)
                {
                    string? desc = updateItem.Description;
                    updateItem.Description = this.GetContentsFromWebSite(updateItem.Link, waitingDuration);
                    updateItem.Title = Utils.ReplaceBadgeText(updateItem.Title);

                    //SQL DB�� �����ϴ� Title�� �Ϻ� �ܾ���� �ѱ���� ��ȯ
                    //�׷���, �ѱ��� ������ ������ �ذ��ϱ� ���� �ּ�ó���ϰ� �׳� �������� ����.
                    //updateItem.Title = ReplaceBadgeTextToKorean(updateItem.Title);
                    // DB ����� ����Ʈ�� �߰�
                    dbItems.Add(updateItem);
                    _logger.LogInformation($"AzUpdateNews prepared for database: {updateItem.Title}");

                    // HTML ���� ����
                    body += string.Format(itemTemplate,
                        updateItem.Link,
                        updateItem.Title,
                        desc, // HTML �������� ���� Description ���. TODO : V1���� �Ʒ� ��������
                        // updateItem.Description,
                        updateItem.Category,
                        updateItem.PubDate);
                }

                //������ ���� ���, ������Ʈ ���ٴ� �������� ��ü
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
                    //������Ʈ�� ������ �׳� �� ���ڿ��� �����޶�� ��û�� ���� �ּ�ó��
                    //body = $@"<div class='update-item'><div class='update-title'><b>No Update Today</b></div></div>";
                }

                // html ���ڿ� �ϼ��ϱ�
                string html = string.Format(htmlTemp, head, body);

                // HTTP ���信 HTML ���� ����
                await response.WriteStringAsync(body, Encoding.UTF8);

                // MultiResponse�� HTTP ����� SQL Output ��ȯ
                return new MultiResponse()
                {
                    News = dbItems.ToArray(),
                    HttpResponseData = response
                };
            }
            catch (Exception ex)
            {
                // ���� �߻� �� �α� ��� �� ���� �޽��� ��ȯ
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

        // Selenium ����Ͽ� ���� ������ �о����
        private string GetContentsFromWebSite(string link, int waitingDuration)
        {
            string description = string.Empty;
            try
            {
                _logger.LogInformation($"������ ȣ�����:{link}");
                var chromeOptions = new ChromeOptions();
                chromeOptions.BrowserVersion = "138.0.7204.183"; // Chrome ������ �°� ����
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
                _logger.LogInformation($"������ ȣ��Ϸ�:{link}");
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error: {ex.Message}");
            }

            return description;
        }

        enum RssFeed { AzureUpdate, Blog };
        // RSS Feed���� AzUpdateNews ����Ʈ�� ��ȯ
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

                    //RSS Feed���� �ʿ��� �����鸸 �����Ͽ� List<AzUpdateNews>�� ����
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
