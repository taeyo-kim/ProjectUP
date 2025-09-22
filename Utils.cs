using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectUP
{
    public static class Utils
    {
        public static string GetItemTemplate()
        {
            string item = $@"<div class='update-item'>
                                <div class='update-title'><b><a href='{{0}}'>{{1}}</a></b></div>
                                <ul class='update-details'>
                                    <li style='border-left-color: #ff6b6b'><strong>Description:</strong>{{2}}</li>
                                    <li style='border-left-color: #4ecdc4'><strong>Category:</strong>{{3}}</li>
                                    <li style='border-left-color: #45b7d1'><strong>Publication Date:</strong>{{4}}(UTC)</li>
                                </ul>
                            </div>";
            return item;
        }

        // CSS 스타일 포함된 head 태그 반환
        public static string GetHeadAndStyle()
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
        public static string ReplaceBadgeText(string text)
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

        public static string ReplaceBadgeTextToKorean(string text)
        {
            return text
                .Replace("Description", "설명")
                .Replace("Retirement:", "지원 종료:")
                .Replace("[In development]", "미리보기(비공개)")
                .Replace("[In preview]", "미리보기(공개)");
            //.Replace("[Launched]", "정식 지원(GA)");
        }
    }
}
