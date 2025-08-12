using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Xml;

namespace AzUpdate
{
    //public class UpdateInfo
    //{
    //    public string? URL { get; set; }
    //    public int? WatingDuration { get; set; }
    //}

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

        [Function("GetUpdate")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
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

            //string test = $"<html><head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"><title>Azure Service Updates</title><style>* {{margin: 0;padding: 0;box-sizing: border-box;}}body {{font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;background: #f5f7fa;min-height: 100vh;padding: 0.5rem;line-height: 1.6;color: #333;}}h1 {{text-align: center;color: #2c3e50;font-size: 2.5rem;font-weight: 700;margin-bottom: 3rem;}}.update-item {{background: white;border: 1px solid #e1e8ed;border-radius: 12px;margin-bottom: 1rem;padding: 0;overflow: hidden;box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);}}\r\n.update-title {{background: #f8f9fa;padding: 0.5rem 1rem;margin: 0;border-bottom: 1px solid #e1e8ed;}}.update-title b {{font-weight: 600;}}.update-details {{padding: 0.5rem;margin: 0;list-style: none;background: white;}}\r\n.update-details > li {{font-size: 0.85rem;background: #f8f9fa;margin-bottom: 0rem;padding: 0.5rem 1.5rem;border-radius: 8px;border-left: 4px solid transparent;}} li li {{margin-left: 1.5rem;}} .status-badge-GA {{background: #28a745;color: white;padding: 0.4rem 1rem;border-radius: 20px;font-size: 1rem;font-weight: 600;text-transform: uppercase;letter-spacing: 0.5px;border: none;display: inline-block;margin-right: 0.8rem;}}.status-badge-dev {{background: #FFFF00;color: black;padding: 0.4rem 1rem;border-radius: 20px;font-size: 1rem;font-weight: 600;text-transform: uppercase;letter-spacing: 0.5px;border: none;display: inline-block;margin-right: 0.8rem;}}.status-badge-preview {{background: #00c3ff;color: white;padding: 0.4rem 1rem;border-radius: 20px;font-size: 1rem;font-weight: 600;text-transform: uppercase;letter-spacing: 0.5px;border: none;display: inline-block;margin-right: 0.8rem;}}.status-badge-retirement {{background: #ff6b6b;color: white;padding: 0.4rem 1rem;border-radius: 20px;font-size: 1rem;font-weight: 600;text-transform: uppercase;letter-spacing: 0.5px;border: none;display: inline-block;margin-right: 0.8rem;}}strong {{color: #2c3e50;font-weight: 600;margin-right: 0.5rem;}}a {{text-decoration: none;color: #2c3e50;}}/* ������ ������ */@media (max-width: 768px) {{body {{padding: 1rem;}}h1 {{font-size: 2rem;margin-bottom: 2rem;}}.update-title, .update-details {{padding: 1rem;}}.status-badge {{display: block;margin-bottom: 0.5rem;text-align: center;}}/* ��ũ�ѹ� ��Ÿ�ϸ� */::-webkit-scrollbar {{width: 8px;}}::-webkit-scrollbar-track {{background: #f1f1f1;}}::-webkit-scrollbar-thumb {{background: #c1c1c1;border-radius: 4px;}}::-webkit-scrollbar-thumb:hover {{background: #a8a8a8;}}</style></head><body><div class='update-item'>                                     <div class='update-title'><b><a href='https://azure.microsoft.com/updates?id=499945'>" +
            //    $"<span class='status-badge-GA'>���� ���(G.A)</span>  Azure Data Box Next Gen is now generally available in additional regions</a></b></div>\r\n                                        <ul class='update-details'>\r\n                                            <li style='border-left-color: #ff6b6b'><strong>Description:</strong>Azure Data Box Next Gen is now Generally Available in NEW regions including<i> Australia, Japan, Singapore, Brazil, Hong Kong, UAE, Switzerland and Norway</i>.<br>With these new additions, we now have&nbsp;<ul data-editing-info=\"{{&quot;orderedStyleType&quot;:1,&quot;unorderedStyleType&quot;:2}}\"><li>Both - Azure Data box 120TB and 525 TB generally available (GA) in the US, UK, Canada, EU, US Gov, Australia, Japan and Singapore.</li><li>Azure Data Box 120TB is GA in Brazil, UAE, Hong Kong, Switzerland and Norway.</li></ul><blockquote><br></blockquote>Earlier this year we <a href=\"https://nam06.safelinks.protection.outlook.com/?url=https%3A%2F%2Ftechcommunity.microsoft.com%2Fblog%2FAzureStorageBlog%2Fannouncing-general-availability-of-next-generation-azure-data-box-devices%2F4404549&amp;data=05%7C02%7CBapi.Chakraborty%40microsoft.com%7Ce4c7c0536e154c6db6cf08ddd3e0c0eb%7C72f988bf86f141af91ab2d7cd011db47%7C1%7C0%7C638899684528485428%7CUnknown%7CTWFpbGZsb3d8eyJFbXB0eU1hcGkiOnRydWUsIlYiOiIwLjAuMDAwMCIsIlAiOiJXaW4zMiIsIkFOIjoiTWFpbCIsIldUIjoyfQ%3D%3D%7C0%7C%7C%7C&amp;sdata=ogF%2BCllhD98ddUCIE7akKh5NW9IUE8VX0FDFUM0qD3Q%3D&amp;reserved=0\">announced general availability</a> of Azure Data Box 120 TB and Azure Data Box 525 TB, our next-generation, compact, NVMe-based Data Box devices.&nbsp;We have ingested several petabytes of data from diverse industries, with customers noting up to 10x faster data transfer. The new devices are valued for their reliability and efficiency in large-scale migration projects.<br>Order your device today! You can use the Azure portal to select the requisite SKU suitable for your migration needs and place the order. For feedback and questions, write to us at DataBoxPM@microsoft.com.</li>\r\n                                            <li style='border-left-color: #4ecdc4'><strong>Category:</strong>Launched,Migration,Storage,Azure Data Box,Features,</li>\r\n                                            <li style='border-left-color: #45b7d1'><strong>Publication Date:</strong>Wed, 06 Aug 2025 16:00:45 Z(UTC)</li>\r\n                                        </ul>\r\n                                    </div></div></body></html>";


            //return new ContentResult
            //{
            //    Content = test,
            //    ContentType = "text/plain; charset=utf-8",
            //    StatusCode = 200
            //};

            // ��Ÿ���� ���Ե� HTML ���ø�
            string head = GetHeadAndStyle();
            head = head.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("  ", "");

            // RSS���� �ǵ忡�� UpdateItem���� ��ȯ�Ͽ� �ʿ��� ������ ��������
            List<UpdateItem> itemList = GetUpdateItemListFromRssFeed(RssFeed.AzureUpdate, targetHour);

            string itemTemplate = $@"<div class='update-item'>
                                        <div class='update-title'><b><a href='{{0}}'>{{1}}</a></b></div>
                                        <ul class='update-details'>
                                            <li style='border-left-color: #ff6b6b'><strong>Description:</strong>{{2}}</li>
                                            <li style='border-left-color: #4ecdc4'><strong>Category:</strong>{{3}}</li>
                                            <li style='border-left-color: #45b7d1'><strong>Publication Date:</strong>{{4}}(UTC)</li>
                                        </ul>
                                    </div>";
            string body = string.Empty;

            // List<UpdateItem>�� �������鼭 (1)���� ������ �о����, (2)HTML ������ ����
            foreach (UpdateItem updateItem in itemList)
            {
                updateItem.Description = this.GetContentsFromWebSite(updateItem.Link, waitingDuration);
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

            //������ ��ü HTML�� ��ȯ�Ϸ� ������, �����͸� ���̱� ���� body�� ��ȯ
            //string sign = @"<div style=""text-align: right;"">
            //                    <span class=""main-title"">Project <span class=""orange-text"">UP</span><span class=""silver-text"">(date)</span></span>
            //                    <span class=""silver-text"">by <span class=""se-text"">SE����</span></span>
            //                </div>";

            return new OkObjectResult(body);


            //_logger.LogInformation("C# HTTP trigger function processed a request.");
            //return new OkObjectResult("Welcome to Azure Functions!");
        }

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

        private string GetContentsFromWebSite(string link, int waitingDuration)
        {
            string description = string.Empty;
            try
            {
                _logger.LogInformation($"������ ȣ�����:{link}");
                var chromeOptions = new ChromeOptions();
                chromeOptions.AddArguments(
                    "--headless",
                    "--no-sandbox",
                    "--disable-gpu",
                    "--whitelisted-ips"
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
        private class UpdateItem
        {
            public string? Title { get; set; }
            public string? Link { get; set; }
            public string? Description { get; set; }
            public string? Category { get; set; }
            public string? PubDate { get; set; }
        }

        List<UpdateItem> GetUpdateItemListFromRssFeed(RssFeed feedType, int last)
        {
            List<UpdateItem> itemList = [];
            UpdateItem item;
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

                    //RSS Feed���� �ʿ��� �����鸸 �����Ͽ� List<UpdateItem>�� ����
                    foreach (XmlNode rssNode in rssNodes)
                    {
                        item = new UpdateItem();
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
