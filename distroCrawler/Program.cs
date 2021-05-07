using System;
using System.Collections.Generic;
using NLog;
using System.Linq;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace distroCrawler
{
    class Program
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            Console.WriteLine("Distro Crawler Program.");

            string url = "https://distrowatch.com/dwres.php?resource=popularity";
            DateTime dt = DateTime.Now;
            string data = GetWebSiteData(url);

            Console.WriteLine("Parsing Data.");
            //logger.Debug(data);
            List<distroTrend.Model.Distro> listDistro = ParseData(data);

            string fileName = @"C:\temp\distro.csv";

            Helper.Utility.WriteCSV(listDistro, fileName);

            Console.WriteLine("Date Exported to " + fileName);
            string message = "Data was extracted in " + (DateTime.Now - dt).Seconds + " secs.";
            logger.Info(message);
            Console.WriteLine(message);
            Console.WriteLine("Press any key to exit.");
            Console.Read();
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

                distro.Code = GetCode(title);
                distro.Name = title;

                if (!listDistro.Exists(x => x.Name == title))
                    listDistro.Add(distro);
            }

            return listDistro;
        }
        static string GetCode(string name)
        {
            string code;
            int truncateLength = 8;

            string[] nameArray = name.Split(" ");

            //Default
            code = name;

            Regex rgx = new Regex("[^a-zA-Z0-9 -]");
            code = rgx.Replace(code, "");

            if (name.Length > truncateLength)
            {
                if (nameArray.Length > 1)
                {
                    code = string.Empty;

                    foreach (string word in nameArray)
                    {
                        int maxSubLength = word.Length;
                        
                        if (maxSubLength > truncateLength/2)
                            maxSubLength = truncateLength/2;

                        code += word.Substring(0, maxSubLength);
                    }
                }
            }   

            code = code.Replace(" ", string.Empty);

            int maxLength = code.Length;

            if (maxLength > truncateLength)
                maxLength = truncateLength;

            code = code.Substring(0, maxLength);

            logger.Debug("Name=" + name + ", Code=" + code);

            return code;
        }
    }
}
