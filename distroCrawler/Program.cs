using HtmlAgilityPack;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace distroCrawler
{
    class Program
    {
        static Logger logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            string version = string.Empty;
            string message = string.Empty;

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            version = fvi.FileVersion;
            Program ob = new Program();
            //Console.WriteLine("Program to restart the application is started.");
            Console.WriteLine("*******************************************");
            Console.WriteLine("Distro Crawler Program. (v" + version + ")");
            Console.WriteLine("*******************************************");
            Console.WriteLine();
            Console.WriteLine("1 - Crawl Data.");
            Console.WriteLine("2 - Update Database");
            Console.WriteLine();
            Console.Write("Please enter your choice - ");

            if (args.Length == 0)
            {
                message = "Console Argument is not provided";
                logger.Info(message);
                ob.CheckUserResponse();
            }
            else
            {
                Tuple<string, string, string, string> argTuple = GetCommandArg(args);
                ob.MainWrapper(argTuple);
            }

            Console.WriteLine("Press any key to exit.");
            Console.Read();
        }

        private void CheckUserResponse()
        {
            var userInput = Console.ReadLine();
            Tuple<string, string, string, string> argTuple = new Tuple<string, string, string, string>(userInput, null, null, null);
            MainWrapper(argTuple);
        }

        private static Tuple<string, string, string, string> GetCommandArg(string[] args)
        {
            Tuple<string, string, string, string> argTuple = null;
            string mode = null, dbName = null, dbScriptFolderPath = null;
            logger.Info(string.Format("parameter count = {0}", args.Length));

            for (int i = 0; i < args.Length; i++)
            {
                logger.Trace(string.Format("Arg[{0}] = [{1}]", i, args[i]));
            }
            foreach (string arg in args)
            {
                if (arg.Length >= 3)
                {
                    switch (arg.Substring(0, 2).ToUpper())
                    {
                        case "/M"://MODE
                            mode = arg.Substring(3);
                            break;
                        case "/D"://DB Name
                            dbName = arg.Substring(3);
                            break;
                        case "/F"://DB Name
                            dbScriptFolderPath = arg.Substring(3);
                            break;
                        default:
                            break;
                    }
                }
            }

            logger.Debug("DbName = " + dbName);
            logger.Debug("FolderPath = " + dbScriptFolderPath);
            argTuple = new Tuple<string, string, string, string>(mode, dbName, dbScriptFolderPath, string.Empty);
            return argTuple;
        }

        private int MainWrapper(Tuple<string, string, string, string> argTuple)
        {
            int exitCode = 0;

            if (argTuple.Item1 == "1")
            {
                CrawlData();
            }
            else if (argTuple.Item1 == "2")
            {
                DateTime dt = DateTime.Now;
                string message;
                List<distroTrend.Model.Distro> listDistro = CrawlData();

                DateTime dtUpdate = DateTime.Now;
                UpdateDB(listDistro);
                message = "Data was updated in " + (DateTime.Now - dtUpdate).Seconds + " secs.";
                Console.WriteLine(message);

                message = "Total Time taken is " + (DateTime.Now - dt).Seconds + " secs.";
                Console.WriteLine(message);
            }
            else
            {
                Console.WriteLine("No supported option.");
            }

            return exitCode;
        }

        static List<distroTrend.Model.Distro> CrawlData()
        {
            string url = "https://distrowatch.com/dwres.php?resource=popularity";
            DateTime dt = DateTime.Now;
            string data = GetWebSiteData(url);

            Console.WriteLine("Parsing data started.");
            //logger.Debug(data);
            List<distroTrend.Model.Distro> listDistro = ParseData(data);

            Console.WriteLine("Parsing data completed.");

            string fileName = @"C:\temp\distro.csv";

            List<distroTrend.Model.Distro> listDistroCSV = new List<distroTrend.Model.Distro>();
            //Transform list for CSV.
            foreach (distroTrend.Model.Distro distro in listDistro)
            {
                distroTrend.Model.Distro distroCSV = new distroTrend.Model.Distro();
                distroCSV.Id = distro.Id;
                distroCSV.Code = distro.Code;
                distroCSV.Name = distro.Name;
                distroCSV.Description = "\"" + distro.Description.Replace("\"", "\"\"") + "\"";
                distroCSV.HomePage = distro.HomePage;
                distroCSV.ImageURL = distro.ImageURL;

                //Download Image.
                if (!string.IsNullOrEmpty(distro.ImageURL))
                {
                    string urlImage = "https://distrowatch.com/" + distro.ImageURL;

                    string directoryName = Path.GetDirectoryName(distro.ImageURL);

                    if (!System.IO.Directory.Exists(directoryName))
                        System.IO.Directory.CreateDirectory(directoryName);
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(new Uri(urlImage), distro.ImageURL);
                    }
                }

                listDistroCSV.Add(distroCSV);
            }

            Helper.Utility.WriteCSV(listDistroCSV, fileName);

            Console.WriteLine("Date Exported to " + fileName);
            string message = "Data was extracted in " + (DateTime.Now - dt).Seconds + " secs.";
            logger.Info(message);
            Console.WriteLine(message);

            return listDistro;
        }

        static void UpdateDB(List<distroTrend.Model.Distro> listDistro)
        {
            BLL.Distro distroBL = new BLL.Distro();

            string sqlConn = System.Configuration.ConfigurationManager.AppSettings["dbConnection"];

            string message = string.Empty;

            foreach (distroTrend.Model.Distro distro in listDistro)
            {
                message = string.Empty;

                distroTrend.Model.Distro distroDb = distroBL.GetDistro(distro.Code, sqlConn);

                if(distroDb == null)
                {
                    message = distro.Name + " is not found in DB. Inserting...";

                }
                else
                {
                    if (distroDb.Description != distro.Description || distroDb.ImageURL != distro.ImageURL)
                    {
                        message = distro.Name + " found in DB but details are outdated. Updating...";
                        distroBL.Update(sqlConn, distroDb.Id, distro);
                    }
                }

                if (!string.IsNullOrEmpty(message))
                {
                    logger.Info(message);
                    Console.WriteLine(message);
                }
            }
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

            int counter = 0;

            foreach (HtmlNode node in nodes)
            {
                counter++;
                string title = node.InnerText;
                distroTrend.Model.Distro distro = new distroTrend.Model.Distro();

                string[] token = node.OuterHtml.Split(new char[] { '\"' }, StringSplitOptions.RemoveEmptyEntries);
                string dwCode = string.Empty;
                if (token.Length > 1)
                    dwCode = token[1];

                distro.Code = GetCode(title);
                distro.Name = title;
                SetSubData(dwCode, name: title, distro);

                logger.Debug("Code=" + distro.Code + ", Name=" + distro.Name + ", Desc=" + distro.Description);

                if (!listDistro.Exists(x => x.Name == title))
                    listDistro.Add(distro);

                string message = counter + ". data Parsed for " + distro.Name;
                Console.WriteLine(message);

                //TODO: Temporary Break.
                if (listDistro.Count > 300)
                    break;
            }

            return listDistro;
        }
        static void ParseDataSub(string data, string name, distroTrend.Model.Distro distro)
        {
            string description = string.Empty;
            string imageSrc = string.Empty;

            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(data);

            var nodesMain = doc.DocumentNode.SelectNodes(@"//td[@class='TablesTitle']/a");

            foreach (HtmlNode node in nodesMain)
            {
                if (node.ParentNode.ChildNodes.Count > 10)
                    description = node.ParentNode.ChildNodes[10].InnerText;

                var nodes = doc.DocumentNode.SelectNodes(@"//img");

                foreach (HtmlNode nodeImg in nodes)
                {
                    string search = "title=" + "\"" + name + "\"";
                    if (nodeImg.OuterHtml.Contains(search))
                    {
                        HtmlAttribute img = nodeImg.Attributes.Where(i => i.Name == "src").FirstOrDefault();
                        distro.ImageURL = img.Value;
                    }
                }
            }

            distro.Description = description;
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

                        if (maxSubLength > truncateLength / 2)
                            maxSubLength = truncateLength / 2;

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

        static void SetSubData(string dwCode, string name, distroTrend.Model.Distro distro)
        {
            string url = "https://distrowatch.com/table.php?distribution=" + dwCode;
            DateTime dt = DateTime.Now;
            string data = GetWebSiteData(url);

            ParseDataSub(data, name, distro);
        }
    }
}
