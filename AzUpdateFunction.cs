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

        // �ʱ�ȭ �Լ� (wran-up��)
        [Function("Init")]
        public IActionResult Init([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            return new OkObjectResult("Function Initialized");
        }

        // Azure ������Ʈ ������ RSS Feed���� �о�� HTML�� ��ȯ
        [Function("GetUpdateHTMLOnly")]
        public async Task<IActionResult> GetUpdateHTMLOnly(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            string url = "https://azure.microsoft.com/ko-kr/updates?id=498166";
            int waitingDuration = 2000;
            int targetHour = 24;

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
            string head = GetHeadAndStyle();
            head = head.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("  ", "");

            // RSS���� �ǵ忡�� AzUpdateNews���� ��ȯ�Ͽ� �ʿ��� ������ ��������
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

            // List<AzUpdateNews>�� �������鼭 (1)���� ������ �о����, (2)HTML ������ ����, (3)DB ����� ����Ʈ�� �߰�
            foreach (AzUpdateNews updateItem in itemList)
            {
                //�ӽ÷� ���Ƶΰ�, V1���� �ּ��� Ǯ�⸸ �ϸ� ��. ���� �Լ������� ���� ����
                //updateItem.Description = this.GetContentsFromWebSite(updateItem.Link, waitingDuration);
                updateItem.Title = ReplaceBadgeText(updateItem.Title);

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

            // ��Ÿ���� ���Ե� HTML ���ø�
            string head = GetHeadAndStyle();
            head = head.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("  ", "");

            // RSS���� �ǵ忡�� AzUpdateNews���� ��ȯ�Ͽ� �ʿ��� ������ ��������
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
                // List<AzUpdateNews>�� �������鼭 (1)���� ������ �о����, (2)HTML ������ ����
                foreach (AzUpdateNews updateItem in itemList)
                {
                    string? desc = updateItem.Description;
                    updateItem.Description = this.GetContentsFromWebSite(updateItem.Link, waitingDuration);
                    string? title = updateItem.Title = ReplaceBadgeText(updateItem.Title);

                    //SQL DB�� �����ϴ� Title�� �Ϻ� �ܾ���� �ѱ���� ��ȯ
                    updateItem.Title = ReplaceBadgeTextToKorean(updateItem.Title);
                    // DB ����� ����Ʈ�� �߰�
                    dbItems.Add(updateItem);
                    _logger.LogInformation($"AzUpdateNews prepared for database: {updateItem.Title}");

                    // HTML �������� ���� Description ���.
                    //TODO : V1���� �Ʒ� ��������
                    updateItem.Description = desc;  
                    updateItem.Title = title;

                    // HTML ���� ����
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

            //TODO: �����غ��� OkObjectResult(body) �� ���� �ѱ��� �������µ�, ���⼭�� �����ΰ� ����.

        }

        public class MultiResponse
        {
            [SqlOutput("dbo.AzUpdateNews", connectionStringSetting: "SqlConnectionString")]
            public AzUpdateNews[]? News { get; set; }

            public FuncHttp.HttpResponseData? HttpResponseData { get; set; }
        }

        // CSS ��Ÿ�� ���Ե� head �±� ��ȯ
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

        // GA, Preview, Dev, Retirement ���� �ؽ�Ʈ�� HTML�� ��ȯ
        private string ReplaceBadgeText(string text)
        {
            if (text.Contains("[In preview]"))
            {
                text = text.Replace("Public Preview:", "");
                //text = text.Replace("[In preview]", "<span class='status-badge preview'>�̸� ����(Public Preview) </span>");
                text = text.Replace("[In preview]", "<span class='status-badge preview'>[In preview]</span>");

            }
            if (text.Contains("[In development]"))
            {
                text = text.Replace("Private Preview:", "");
                //text = text.Replace("[In development]", "<span class='status-badge dev'>���� ��(Private Preview)</span>");
                text = text.Replace("[In development]", "<span class='status-badge dev'>[In development]</span>");
            }

            if (text.Contains("[Launched]"))
            {
                text = text.Replace("Generally Available:", "");
                //text = text.Replace("[Launched]", "<span class='status-badge GA'>���� ���(G.A)</span>");
                text = text.Replace("[Launched]", "<span class='status-badge GA'>[Launched]</span>");
            }
            if (text.Contains("Retirement:"))
            {
                //text = text.Replace("Retirement:", "<span class='status-badge retirement'>���� ����(Deprecated)</span>");
                text = text.Replace("Retirement:", "<span class='status-badge retirement'>Retirement:</span>");
            }
            return text;
        }

        private string ReplaceBadgeTextToKorean(string text)
        {
            return text
                .Replace("Description", "����")
                .Replace("Retirement:", "���� ����:")
                .Replace("[In development]", "�̸�����(�����)")
                .Replace("[In preview]", "�̸�����(����)");
                //.Replace("[Launched]", "���� ����(GA)");
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
