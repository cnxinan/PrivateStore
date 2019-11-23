using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;
using System.Text;

namespace DotnetCore
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = File.ReadAllText(".\\appsettings.json");
            if (args == null || args.Count() != 2 || !args[0].ToLower().StartsWith("test"))
            {
                throw new ArgumentNullException("You should give the right test server name!");
            }

            var testNo = args[0];

            var configObj = JsonConvert.DeserializeObject<Appsettings>(config);

            DirectoryInfo rootDir1 = new DirectoryInfo(configObj.QinCorePath + "\\Tests");
            ChangeMatchedFiles(rootDir1, $"app.{testNo}.config", args[1]);
            DirectoryInfo rootDir2 = new DirectoryInfo(configObj.QinCorePath + "\\Core\\API");
            ChangeMatchedFiles(rootDir2, $"web.{testNo}.config", args[1]);
        }

        public static void ChangeMatchedFiles(DirectoryInfo rootDir, string searchPattern, string newDataBaseName)
        {
            var subDirs = rootDir.GetDirectories();
            foreach (var dir in subDirs)
            {
                var files = dir.GetFiles(searchPattern);
                foreach (var file in files)
                {
                    Console.WriteLine(file.FullName);

                    XmlDocument doc = new XmlDocument();
                    var readStream = File.OpenRead(file.FullName);
                    doc.Load(readStream);
                    var connNode = doc.SelectSingleNode("configuration/connectionStrings/add[@name=\"DbConnection\"]");

                    if (connNode != null)
                    {
                        var currentStr = connNode.Attributes["connectionString"].Value;
                        connNode.Attributes.GetNamedItem("connectionString").InnerText = Regex.Replace(currentStr, @"Database=(.*?);", $"Database={newDataBaseName};");
                    }
                    readStream.Close();

                    if (connNode != null)
                    {
                        using(StreamWriter streamWrite = new StreamWriter(File.OpenWrite(file.FullName)))
                        {
                            doc.Save(streamWrite);
                        }
                    }
                }
            }
        }
    }

    class Appsettings
    {
        public string QinCorePath { get; set; }
    }
}
