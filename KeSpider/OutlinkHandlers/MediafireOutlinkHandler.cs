using KeSpider.API;
using System.Net;
using System.Text.RegularExpressions;

namespace KeSpider.OutlinkHandlers;

public partial class MediafireOutlinkHandler : IOutlinkHandler
{

    [GeneratedRegex(@"(?<url>https://www\.mediafire\.com/(?:\?|file/)[a-zA-Z0-9]+)")]
    internal static partial Regex RegMediaFire();

    [GeneratedRegex(@"<a\s(?:[^>]*\s)?(?:href=""(?<url>[^""]+)""\s(?:[^>]*\s)?id=""downloadButton""|href=""(?<url>[^""]+)""\s(?:[^>]*\s)?id=""downloadButton"")")]
    internal static partial Regex RegMediafireFile();

    [GeneratedRegex(@"<div\s(?:[^>]*\s)?class=""filename""\s(?:[^>]*\s)?>(?<name>[^<]+)</div")]
    internal static partial Regex RegMediafireFileName();
    public static MediafireOutlinkHandler Instance { get; } = new();

    public Regex Pattern => RegMediaFire();
    public async ValueTask ProcessMatches(
        HttpClient client,
        Dictionary<Array256bit, string> dlCache,
        PostRoot post,
        DateTime datetime,
        DateTime datetimeEdited,
        string pageFolderPath,
        string content,
        HashSet<string> usedLinks,
        params IEnumerable<Match> matches)
    {
        foreach (Match m in matches)
        {
            if (!m.Success)
                continue;
            string text = m.Groups["url"].Value;
            if (!usedLinks.Add(text))
                continue;
            Console.WriteLine($"    @O - Find Outlink of Mediafire: {text}");
            string fileName = Utils.ReplaceInvalidFileNameChars(text) + ".placeholder.txt";
            string path = Path.Combine(pageFolderPath, fileName);
            if (Program.SavemodeContent == SaveMode.Skip && File.Exists(path))
            {
                Console.WriteLine("    @O - Skipped");
                Utils.SetTime(path, datetime, datetimeEdited);
            }
            else
            {
                Utils.SaveFile(text, fileName, pageFolderPath, datetime, datetimeEdited, Program.SavemodeOutlink);
                string html = await client.GetStringAsync(text);
                Match mm = RegMediafireFile().Match(html);
                if (mm.Success)
                {
                    string urlDirect = WebUtility.UrlDecode(mm.Groups["url"].Value);
                    string name = RegMediafireFileName().Match(html) is { Success: true } match ?
                        match.Groups["name"].Value : Path.GetFileName(urlDirect);
                    name = Program.FixSpecialExt(name);
                    string path2 = Path.Combine(pageFolderPath, name);
                    if (File.Exists(path2))
                    {
                        Console.WriteLine($"    @O - Skipped");
                        goto E;
                    }
                    Console.WriteLine($"    @O - aria2c!");
                    Program.Aria2cDownload(pageFolderPath, name, urlDirect);
                E:
                    Utils.SetTime(path2, datetime, datetimeEdited);
                    FileInfo fi = new(path2);
                    string d = Path.Combine(fi.DirectoryName ?? "", Path.GetFileNameWithoutExtension(fi.Name));
                    if (!Directory.Exists(d) && !File.Exists(d))
                    {
                        if (fi.Extension is ".zip" or ".rar" or ".7z" or ".gz" or ".tar")
                            Program.SevenZipExtract(d, path2);
                    }
                }
            }
        }
    }
}
