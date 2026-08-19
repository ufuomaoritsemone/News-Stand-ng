using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

class Program
{
    static async Task Main()
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,application/rss+xml,*/*;q=0.8");

        var feeds = new Dictionary<string, string>
        {
            { "Premium Times", "https://www.premiumtimesng.com/feed/" },
            { "Punch", "https://punchng.com/feed/" },
            { "Vanguard", "https://www.vanguardngr.com/feed/" },
            { "Daily Trust", "https://dailytrust.com/feed/" },
            { "Channels TV", "https://www.channelstv.com/feed/" },
            { "Guardian", "https://guardian.ng/category/news/feed/" }
        };

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            CheckCharacters = false,
            IgnoreComments = true,
            IgnoreWhitespace = true
        };

        foreach (var (name, url) in feeds)
        {
            Console.WriteLine($"Testing {name}: {url}");
            try
            {
                var resp = await client.GetAsync(url);
                Console.WriteLine($"  Status: {resp.StatusCode}");
                if (!resp.IsSuccessStatusCode) continue;

                var stream = await resp.Content.ReadAsStreamAsync();
                using var reader = XmlReader.Create(stream, settings);
                var doc = XDocument.Load(reader);
                var items = doc.Descendants().Where(x => x.Name.LocalName.Equals("item", StringComparison.OrdinalIgnoreCase) || x.Name.LocalName.Equals("entry", StringComparison.OrdinalIgnoreCase)).ToList();
                Console.WriteLine($"  Found {items.Count} items.");
                if (items.Count > 0)
                {
                    var first = items.First();
                    var title = first.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("title", StringComparison.OrdinalIgnoreCase))?.Value;
                    Console.WriteLine($"  Sample Title: {title}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Exception: {ex.Message}");
            }
            Console.WriteLine();
        }
    }
}
