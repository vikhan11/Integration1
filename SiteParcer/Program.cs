using DTO;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;

namespace SiteParcer
{
    class Program
    {
        [Obsolete]
        public static IEnumerable<NewsDTO> Crawl()
        {
            string homeUrl = "https://www.foxnews.com/";

            //options skip errors
            ChromeOptions chromeOptions = new ChromeOptions();
            chromeOptions.AddArgument("--ignore-certificate-errors");
            chromeOptions.AddArgument("--ignore-certificate-errors-spki-list");
            chromeOptions.AddArgument("--ignore-ssl-errors");
            chromeOptions.AddArgument("test-type");
            chromeOptions.AddArgument("no-sandbox");
            chromeOptions.AddArgument("-incognito");
            chromeOptions.AddArgument("--start-maximized");


            IWebDriver driver = new ChromeDriver(@"C:\Users\Viktor\Desktop\Integration\Integration", chromeOptions);
            driver.Navigate().GoToUrl(homeUrl);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);


            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            wait.Until(d => d.FindElement(By.CssSelector("#wrapper > div.page > div.page-content > div.row.sticky-columns > main > div > div > div.collection.collection-spotlight.has-hero > div > article > div.info > header > h2")));

        

            var elements = driver.FindElements(By.CssSelector(" div.collection.collection-spotlight.has-hero, div.collection.collection-spotlight div.info > header.info-header > h2.title.title-color-default > a"));

            List<NewsDTO> news = elements
            .Select(el => new NewsDTO
            {
                Title = el.Text,
                Url = el.GetAttribute("href")
            }).ToList();
            news.RemoveAt(0);

            for (int i = 0; i < news.Count; i++)
            {
                Thread.Sleep(TimeSpan.FromSeconds(10));

                var n = news[i];
                try
                {
                    driver.Navigate().GoToUrl(n.Url);

                    //wait.Until(ExpectedConditions.ElementExists(By.CssSelector(".pg-headline")));

                    n.ID = (i+1).ToString();
                    n.Author = driver.FindElement(By.CssSelector("div.author-byline a")).Text;
                    //n.Description = driver.FindElement(By.CssSelector("p.speakable")).Text;
                    n.Description = driver.FindElement(By.CssSelector("h2.sub-headline")).Text;
                    n.DateOfPublication = CustomParser(driver.FindElement(By.CssSelector("main div.article-date > time")).Text);
                }
                catch (Exception){}

                yield return n;
            }

            driver.Close();
        }
        public static DateTime CustomParser(string str)
        {
            int value = int.Parse(Regex.Match(str, @"\d+").Value);

            if (str.Contains("minutes")) return DateTime.UtcNow - TimeSpan.FromMinutes(value);
            else if (str.Contains("seconds")) return DateTime.UtcNow - TimeSpan.FromSeconds(value);
            else if (str.Contains("hours")) return DateTime.UtcNow - TimeSpan.FromHours(value);
            else return DateTime.UtcNow;
        }

        [Obsolete]
        static void Main(string[] args)
        {
            ConnectionFactory factory = new ConnectionFactory();

            //creds
            factory.UserName = "guest";
            factory.Password = "guest";
            factory.VirtualHost = "/";
            factory.HostName = "localhost";

            using (IConnection conn = factory.CreateConnection())
            using (var model = conn.CreateModel())
            {
                model.QueueDeclare("news", false, false, false, null);

                foreach (var x in Crawl())
                {
                    Console.WriteLine(x);

                    var properties = model.CreateBasicProperties();
                    properties.Persistent = true;

                    model.BasicPublish(
                        "",
                        "news",
                        basicProperties: properties,
                        body: BinaryConverter.ObjectToByteArray(
                                x
                            )
                        );
                }
            }
        }
    }
}