using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ScrapingSandbox
{
    class Program
    {
        private static string imageDirectoryPath = @"C:\Users\SnowCait\Documents\GitHub\dragalialost-library\src\assets\img";
        private static Uri gamepediaUrl = new Uri("https://dragalialost.gamepedia.com/");

        private static readonly HttpClient httpClient = new HttpClient();

        static async Task Main(string[] args)
        {
            var dic = new Dictionary<string, string>()
            {
                { "adventurer", "/Category:Character_Icon_Images" },
                { "dragon", "/Category:Dragon_Icons" },
                { "weapon", "/Category:Weapon_Icons" },
                { "wyrmprint", "/Category:Wyrmprint_Icons" },
            };
            foreach (var (type, firstPageUrl) in dic)
            {
                await DownloadAsync(firstPageUrl, type);

                // 負荷を与えないように少し待つ
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
        }

        private static async Task DownloadAsync(string firstPageUrl, string type)
        {
            var uri = new Uri(gamepediaUrl, firstPageUrl);
            do
            {
                Console.WriteLine($"{type}: {uri}");
                using var response = await httpClient.GetAsync(uri).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                var parser = new HtmlParser();
                using var htmlDocument = await parser.ParseDocumentAsync(html).ConfigureAwait(false);
                await DownloadImagesAsync(htmlDocument, type).ConfigureAwait(false);

                var content = htmlDocument.GetElementById("mw-category-media");
                var nextPageUrl = content.GetElementsByTagName("a").Where(x => x.TextContent == "next page").FirstOrDefault()?.GetAttribute("href");
                if (nextPageUrl == null)
                {
                    break;
                }
                uri = new Uri(gamepediaUrl, nextPageUrl);

                // 負荷を与えないように少し待つ
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            } while (true);
        }

        private static async Task DownloadImagesAsync(IHtmlDocument htmlDocument, string type)
        {
            var content = htmlDocument.GetElementById("mw-category-media");
            var files = content.GetElementsByClassName("galleryfilename");
            foreach (var file in files)
            {
                var fileName = file.TextContent.Replace(" ", "_");
                if (fileName.Contains("(Small)"))
                {
                    Console.WriteLine($"{fileName} skipped.");
                    continue;
                }
                if (fileName == "100018_01_r05.png")
                {
                    Console.WriteLine($"{fileName} skipped.");
                    continue;
                }
                var filePath = Path.Combine(imageDirectoryPath, type, fileName);
                if (File.Exists(filePath))
                {
                    Console.WriteLine($"{fileName} exists.");
                    continue;
                }

                var filePageUri = new Uri(gamepediaUrl, file.GetAttribute("href"));
                var imageUri = await GetImagePathAsync(filePageUri);
                await DownloadImageAsync(imageUri, filePath).ConfigureAwait(false);
                Console.WriteLine($"{fileName} downloaded.");

                // 負荷を与えないように少し待つ
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
            }
        }

        private static async Task<Uri> GetImagePathAsync(Uri filePageUri)
        {
            using var response = await httpClient.GetAsync(filePageUri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            var parser = new HtmlParser();
            using var htmlDocument = await parser.ParseDocumentAsync(html).ConfigureAwait(false);
            var fileDocument = htmlDocument.GetElementById("file");
            var imgTag = fileDocument.GetElementsByTagName("img").First();
            return new Uri(imgTag.GetAttribute("src"));
        }

        private static async Task DownloadImageAsync(Uri imageUri, string filePath)
        {
            using var imageResponse = await httpClient.GetAsync(imageUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            imageResponse.EnsureSuccessStatusCode();
            using var fileStream = File.Create(filePath);
            using var httpStream = await imageResponse.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await httpStream.CopyToAsync(fileStream).ConfigureAwait(false);
            await fileStream.FlushAsync().ConfigureAwait(false);
        }
    }
}
