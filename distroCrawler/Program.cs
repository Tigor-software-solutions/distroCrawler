using System;
using System.Collections.Generic;
using NLog;
using System.Linq;
using HtmlAgilityPack;

namespace distroCrawler
{
    class Program
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            logger.Debug("test");
            Console.WriteLine("Hello World!");

            string url = "https://distrowatch.com/dwres.php?resource=popularity";
            string data = GetWebSiteData(url);

            Console.WriteLine("Parsing Data.");
            //logger.Debug(data);
            List<distroTrend.Model.Distro> listDistro = ParseData(data);

            string fileName = @"C:\temp\distro.csv";

            Helper.Utility.WriteCSV(listDistro, fileName);

            Console.WriteLine("Date Exported to " + fileName);
            Console.ReadLine();
        }

        static string GetWebSiteData(string url)
        {
            using var client = new System.Net.WebClient();
            client.Headers.Add("User-Agent", "C# console program");

            string content = client.DownloadString(url);

            return content;
        }

        static List<distroTrend.Model.Distro> ParseData(string data)
        {
            List<distroTrend.Model.Distro> listDistro = new List<distroTrend.Model.Distro>();
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(data);

            var nodes = doc.DocumentNode.SelectNodes(@"//td[@class='phr2']/a");

            foreach (HtmlNode node in nodes)
            {
                string title = node.InnerText;
                distroTrend.Model.Distro distro = new distroTrend.Model.Distro();
                distro.Name = title;

                listDistro.Add(distro);
            }

            return listDistro;
        }
    }
}
