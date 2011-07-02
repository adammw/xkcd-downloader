using System;
using System.Reflection;
using System.Net;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace xkcddownloaderv2
{
    class XKCDDownloader
    {
        const string API_BASE = "http://xkcd.com/";
        const string API_NAME = "info.0.json";
        const int downloadThreads = 4;

        private class Comic
        {
            public string title = "";
            public int num = 0;
            public string img = "";
            public string alt ="";
        }

        public static void Main(string[] args)
        {
            Console.WriteLine("xkcd downloader");
            Console.WriteLine("Version {0}", Assembly.GetEntryAssembly().GetName().Version.ToString());
            Console.WriteLine();
            if (args.Length != 1)
            {
                Console.WriteLine("USAGE: {0} [downloaddir]", getBaseName2(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName));
            }
            else
            {
                string downloadDirectory = args[0];
                Console.Write("Fetching latest comic... ");

                Comic comic = getBaseComic();
                Console.Write("Latest comic is #{0}\n", comic.num);
                Console.WriteLine("Downloading {0} comics...", comic.num);
                downloadComic(comic, downloadDirectory);

                Task[] tasks = new Task[comic.num - 1];
                for (int i = 0; i < comic.num - 1; i++)
                {
                    int id = (comic.num - i - 1);
                    tasks[i] = Task.Factory.StartNew(() => fetchComic(id, downloadDirectory), TaskCreationOptions.LongRunning);
                }
                Task.WaitAll(tasks);
            }
            Console.WriteLine("Download Complete");
        }

        static void fetchComic(int num, string downloadDirectory)
        {
            if (num == 0)
                return;
            Comic comic = getComic(num);
            if (comic != null)
            {
                downloadComic(comic, downloadDirectory);
            }
        }

        static void downloadComic(Comic comic, string downloadDirectory)
        {
            string filename = getBaseName(comic.img);
            string consoleMessage = String.Format("[#{0}] {1} - Downloading as {2}...", comic.num, comic.title, filename);
            if (File.Exists(downloadDirectory + "\\" + filename) && (new FileInfo(downloadDirectory + "\\" + filename)).Length > 0)
            {
                consoleMessage += " already exists";
            }
            else
            {
                WebClient webclient = new WebClient();
                int tries = 0;
                TryAgain:
                try
                {
                    webclient.DownloadFile(comic.img, downloadDirectory + "\\" + filename);
                }
                catch (WebException e)
                {
                    if (e.Status == WebExceptionStatus.Timeout)
                    {
                        if (tries < 3)
                        {
                            tries++;
                            goto TryAgain;
                        }
                        else
                        {
                            consoleMessage += " timed out";
                        }
                    }
                }
                consoleMessage += " done";
            }
            Console.WriteLine(consoleMessage);
        }

        static string getBaseName(string url)
        {
            string[] parts = url.Split('/');
            return parts[parts.Length - 1];
        }

        static string getBaseName2(string url)
        {
            string[] parts = url.Split('\\');
            return parts[parts.Length - 1];
        }

        static string getJson(string url)
        {
            int tries = 0;
            TryAgain:
            try
            {
                WebRequest req = WebRequest.Create(url);
                WebResponse res = req.GetResponse();
                StreamReader reader = new StreamReader(res.GetResponseStream());
                string json = reader.ReadToEnd();
                reader.Close();
                res.Close();
                return json;
            }
            catch (WebException e)
            {
                if (e.Status == WebExceptionStatus.Timeout)
                {
                    if (tries < 3)
                    {
                        tries++;
                        goto TryAgain;
                    }
                }
                Console.WriteLine("WebException: {0} @ {1}", e.Message, url);
                return "";
            }
        }

        static Comic getBaseComic()
        {
            string json = getJson(API_BASE + API_NAME);
            if (String.IsNullOrEmpty(json))
                return null;
            return parseComic(json);
        }

        static Comic getComic(int id)
        {
            string json = getJson(String.Format("{0}{1}/{2}", API_BASE, id, API_NAME));
            if (String.IsNullOrEmpty(json))
                return null;
            return parseComic(json);
        }

        static Comic parseComic(string json)
        {
            return JsonConvert.DeserializeObject<Comic>(json);
        }
    }
}
