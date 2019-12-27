using CsvHelper;
using HelpjuiceConverter.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReverseMarkdown;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HelpjuiceConverter
{
    class Program
    {
        static readonly string _outputRoot = "Docs";
        static IConfigurationRoot Configuration { get; set; }
        static HttpClient client = new HttpClient();
        static HttpClient downloader = new HttpClient();
        static Converter markdownConverter;
        static Dictionary<string, string> secrets = new Dictionary<string, string>();
        static Dictionary<int, Category> processedCategories = new Dictionary<int, Category>();
        static Dictionary<int, Question> processedQuestions = new Dictionary<int, Question>();
        static HashSet<string> unconvertedImages = new HashSet<string>();
        static HashSet<string> unconvertedLinks = new HashSet<string>();

        static async Task Main(string[] args)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            Startup();

            Console.WriteLine("Converting Docs from HelpJuice to Markdown");

            await RunAsync();

            timer.Stop();
            Console.WriteLine($"Processing Completed in {timer.Elapsed.ToString()}");
        }

        static void Startup()
        {
            // Environment Setup
            var devEnvironmentVariable = Environment.GetEnvironmentVariable("NETCORE_ENVIRONMENT");

            var isDevelopment = string.IsNullOrEmpty(devEnvironmentVariable) ||
                                devEnvironmentVariable.ToLower() == "development";

            // Config Setup
            var builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            if (isDevelopment)
            {
                builder.AddUserSecrets<Program>();
            }

            Configuration = builder.Build();

            IServiceCollection services = new ServiceCollection();
            services
                .Configure<Secrets>(Configuration.GetSection(nameof(Secrets)))
                .AddOptions()
                .AddSingleton<ISecretRevealer, SecretRevealer>()
                .BuildServiceProvider();

            // Load Secrets
            var serviceProvider = services.BuildServiceProvider();
            var revealer = serviceProvider.GetService<ISecretRevealer>();
            secrets = revealer.Reveal();

            // Markdown Converter Setup
            var config = new Config
            {
                GithubFlavored = true, // generate GitHub flavoured markdown, supported for BR, PRE and table tags
                UnknownTags = Config.UnknownTagsOption.Bypass
            };

            markdownConverter = new Converter(config);
        }

        // Process a HelpJuice site/account i.e. site.helpjuice.com
        static async Task ProcessSite(string site)
        {
            Console.WriteLine($"Processing for {site}");

            var rootPath = Path.Combine(Path.GetTempPath(), _outputRoot, site.ToLower());
            DirectoryHandler(rootPath);

            var key = secrets[site];
            await ProcessCategories(site, rootPath, key);
            await ProcessQuestions(site, rootPath, key);
            await ProcessAnswers(site, rootPath, key);

            CreateBaseReadmes(site, rootPath);

            // If you have question IDs that are not unique to a site you'll need to clear
            // Had to disable these because we have links on docs in siteA to docs in siteB
            //processedCategories.Clear();
            //processedQuestions.Clear();
        }

        // Process HelpJuice Categories into directories
        static async Task ProcessCategories(string site, string rootPath, string key)
        {

            var url = new UriBuilder("https", $"{site}.helpjuice.com", 443, "/api/categories", $"?api_key={key}").Uri;
            var categories = await GetAsync<Category>(client, url);

            // Categories have parent/child relationship and unpack to directory/sub-directories
            Console.WriteLine($"Converting {categories.Count} categories into directories");

            while (categories.Count > 0)
            {
                var tempCategories = new List<Category>(categories);
                foreach (var c in tempCategories)
                {
                    if (c.ParentId == null || (c.ParentId != null && processedCategories.ContainsKey(c.ParentId.Value)))
                    {
                        var basePath = rootPath;
                        if (c.ParentId != null)
                        {
                            basePath = processedCategories[c.ParentId.Value].LocalPath;
                        }

                        var fullPath = Path.Combine(basePath, c.Name.Trim()).Replace(" ", "-").ToLower();
                        DirectoryHandler(fullPath);
                        c.LocalPath = fullPath;
                        processedCategories.Add(c.Id, c);
                        categories.Remove(c);
                    }
                }
            }
        }

        // Process HelpJuice Questions into question-name-as-folder/README.md
        static async Task ProcessQuestions(string site, string rootPath, string key)
        {
            var page = 1;
            var hasMoreQuestions = true;
            while (hasMoreQuestions)
            {
                var url = new UriBuilder("https", $"{site}.helpjuice.com", 443, "/api/questions", $"?page={page}&api_key={key}").Uri;
                var questions = await GetAsync<Question>(client, url);

                if (questions.Count == 0)
                {
                    hasMoreQuestions = false;
                }
                else
                {
                    Console.WriteLine($"Converting {questions.Count} questions into files (page {page})");

                    foreach (var q in questions)
                    {
                        var filename = q.Name.Trim()
                            .Replace("/", "&")
                            .Replace(Environment.NewLine, "&")
                            .Replace(":", String.Empty)
                            .Replace(" ", "-")
                            .ToLower();

                        var originalUrl = new StringBuilder()
                            .Append($"https://docs.{site.ToLower()}.com/");

                        var category = new Category();
                        if (q.Categories.Count > 0)
                        {
                            // Might be multiple categories but we'll just take the first
                            category = processedCategories[q.Categories[0].Id];
                            filename = Path.Combine(category.LocalPath, filename);

                            if (!String.IsNullOrEmpty(category.CodeName))
                            {
                                originalUrl.Append(category.CodeName)
                                    .Append("/");
                            }
                        }
                        else
                        {
                            // No category, goes into root
                            filename = Path.Combine(rootPath, filename);
                        }

                        // At this point, filename = the folder to build out for the question
                        DirectoryHandler(filename);
                        // Put the actual question content into a README.md within that folder
                        filename = Path.Combine(filename, "README.md");
                        originalUrl.Append(q.CodeName);

                        // File contents
                        var content = new StringBuilder()
                            .Append($"# {q.Name}{Environment.NewLine}")
                            .Append(Environment.NewLine)
                            .Append($"**Created At:** {q.CreatedAt}  {Environment.NewLine}")
                            .Append($"**Updated At:** {q.UpdatedAt}  {Environment.NewLine}")
                            .Append($"**Original Doc:** [{q.CodeName}]({originalUrl})  {Environment.NewLine}")
                            .Append($"**Original ID:** {q.Id}  {Environment.NewLine}")
                            .Append($"**Internal:** {(q.Accessibility.Equals(0) ? "Yes" : "No")}  {Environment.NewLine}")
                            .Append(Environment.NewLine);

                        if (q.Tags.Count > 0)
                        {
                            content.Append($"**Tags:**{Environment.NewLine}");
                            foreach (var t in q.Tags)
                            {
                                content.Append($"<badge text='{t}' vertical='middle' />{Environment.NewLine}");
                            }
                        }

                        FileHandler(filename, content.ToString());
                        q.LocalPath = filename;
                        processedQuestions.Add(q.Id, q);
                    }
                    page++;
                }
            }
        }

        // Process HelpJuice Answers into content of markdown files
        static async Task ProcessAnswers(string site, string rootPath, string key)
        {
            var page = 1;
            var hasMoreAnswers = true;
            while (hasMoreAnswers)
            {
                var url = new UriBuilder("https", $"{site}.helpjuice.com", 443, "/api/answers", $"?page={page}&api_key={key}").Uri;
                var answers = await GetAsync<Answer>(client, url);

                if (answers.Count == 0)
                {
                    hasMoreAnswers = false;
                }
                else
                {
                    Console.WriteLine($"Converting {answers.Count} answers HTML into Markdown files (page {page})");

                    foreach (var a in answers)
                    {
                        if (processedQuestions.ContainsKey(a.QuestionId))
                        {
                            var filename = processedQuestions[a.QuestionId].LocalPath;
                            var content = a.Body;
                            SanitizeHTML(ref content);
                            content = await ImageHandler(filename, processedQuestions[a.QuestionId].CodeName, content);
                            // if (a.QuestionId.Equals(277711))
                            // {
                            LinkHandler(filename, ref content);
                            // }
                            FileHandler(filename, markdownConverter.Convert(content));
                        }
                    }
                    page++;
                }
            }
        }

        // Task Runner
        static async Task RunAsync()
        {
            foreach (var site in secrets)
            {
                // if (site.Key.Equals("zumasys", StringComparison.CurrentCultureIgnoreCase))
                // {
                await ProcessSite(site.Key);
                // }
            }

            var outputPath = Path.Combine(Path.GetTempPath(), _outputRoot);

            FileHandler(Path.Combine(outputPath, "Images.txt"), string.Join(Environment.NewLine, unconvertedImages));
            FileHandler(Path.Combine(outputPath, "Links.txt"), string.Join(Environment.NewLine, unconvertedLinks));
        }

        // Helper method for calling APIs
        static async Task<List<T>> GetAsync<T>(HttpClient client, Uri path)
        {
            List<T> result = null;
            HttpResponseMessage response = await client.GetAsync(path);
            if (response.IsSuccessStatusCode)
            {
                switch (response.Content.Headers.ContentType.MediaType)
                {
                    case "text/csv":
                        var stream = await response.Content.ReadAsStreamAsync();
                        using (var reader = new StreamReader(stream))
                        using (var csv = new CsvReader(reader))
                        {
                            result = csv.GetRecords<T>().ToList();
                        }
                        break;
                    case "application/json":
                        var json = await response.Content.ReadAsStringAsync();
                        result = JsonSerializer.Deserialize<List<T>>(json);
                        break;
                    default:
                        throw new NotImplementedException($"HTTP Response Content Type {response.Content.Headers.ContentType.MediaType} not supported");
                }
            }

            return result;
        }

        // Scan created Directories and create TOC-like base README.md files as needed
        static void CreateBaseReadmes(string site, string rootPath)
        {
            Console.WriteLine($"Creating base/TOC README.md files for {site}");

            CreateReadme(rootPath, site);

            foreach (var pc in processedCategories)
            {
                CreateReadme(pc.Value.LocalPath, pc.Value.Name);
            }
        }

        // Helper method for creating a base README.md
        static void CreateReadme(string localPath, string name)
        {
            var filename = Path.Combine(localPath, "README.md");
            if (!File.Exists(filename))
            {
                // Build the README content
                var content = new StringBuilder()
                    .Append($"# {name}{Environment.NewLine}")
                    .Append(Environment.NewLine)
                    .Append("## Topics")
                    .Append(Environment.NewLine)
                    .Append(Environment.NewLine);

                // Include links to sub-directories
                var children = Directory.GetDirectories(localPath);
                foreach (var c in children)
                {
                    var relativePath = Path.GetRelativePath(localPath, c);
                    content.Append($"[{relativePath}](./{relativePath})  {Environment.NewLine}");
                }

                content.Append(Environment.NewLine);

                FileHandler(filename, content.ToString());
            }
        }

        // Helper method for writing out directories
        static void DirectoryHandler(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        // Helper method for writing out files
        static void FileHandler(string path, string contents)
        {
            StreamWriter stream;

            if (!File.Exists(path))
            {
                stream = File.CreateText(path);
            }
            else
            {
                stream = new StreamWriter(path, true);
            }

            using (stream)
            {
                stream.Write(contents);
            }
        }

        // Helper method to extract and download images + update src paths
        static async Task<string> ImageHandler(string filename, string title, string html)
        {
            var pattern = @"<img.+?src=[\""'](.+?)[\""'].+?>";
            var rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            var matches = rgx.Matches(html);

            for (var i = 0; i < matches.Count; i++)
            {
                // Take src from img tag
                var oldSrc = matches[i].Groups[1].Value;
                // Zumasys hacks! Not all images are coming from HelpJuice
                if (!oldSrc.Contains("s3.amazonaws.com"))
                {
                    if (oldSrc.Contains("http://www.jbase.com/r5"))
                    {
                        oldSrc = oldSrc.Replace("http://www.jbase.com/r5", "https://static.zumasys.com/jbase/r99")
                                        .Replace("http://jbase.com/r5", "https://static.zumasys.com/jbase/r99")
                                        .Replace("https://jbase.com/r5", "https://static.zumasys.com/jbase/r99")
                                        .Replace("www.jbase.com/r5", "https://static.zumasys.com/jbase/r99");
                    }

                    // Parse img name converting spaces to hyphens and lower casing
                    var imageName = Path.GetFileName(oldSrc)
                        .Replace(" ", "-")
                        .Replace("%20", "-")
                        .ToLower();

                    // Default blob files/extension-less files to jpg
                    if (String.IsNullOrEmpty(Path.GetExtension(imageName)))
                    {
                        imageName = Path.ChangeExtension(imageName, ".jpg");
                    }

                    // Construct new image src
                    var newSrc = Path.Combine(Path.GetDirectoryName(filename), imageName);

                    // Download image
                    try
                    {
                        var imageBytes = await downloader.GetByteArrayAsync(new Uri(oldSrc));

                        // Save image
                        await File.WriteAllBytesAsync(newSrc, imageBytes);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{oldSrc} - {ex.Message}");
                        unconvertedImages.Add(oldSrc);
                    }
                    finally
                    {
                        // Update src in html
                        var newImageAttrs = new StringBuilder("./")
                            .Append(Path.GetRelativePath(Path.GetDirectoryName(filename), newSrc))
                            // See if we can force in an attribute that the Markdown parser will grab for alt text
                            .Append($"\" alt=\"{title}: {Path.GetFileNameWithoutExtension(imageName)}");
                        html = html.Replace(oldSrc, newImageAttrs.ToString());
                    }
                }
                else
                {
                    unconvertedImages.Add(oldSrc);
                }
            }

            return html;
        }

        // Helper method to ensure links have proper targets
        static void LinkHandler(string filename, ref string html)
        {
            var pattern = @"<a.+?href=[\""'](.+?)[\""'].+?>";
            var rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            var matches = rgx.Matches(html);

            for (var i = 0; i < matches.Count; i++)
            {
                // Take href from a tag
                var oldTag = matches[i];
                var oldTarget = oldTag.Groups[1].Value;

                // Zumasys hacks! Some external links were to an old jBASE path that no longer exists
                if (oldTarget.Contains("jbase.com/r5") || oldTarget.Contains("jbase.com/r99"))
                {

                    var newTarget = oldTarget.Replace("http://www.jbase.com/r5", "https://static.zumasys.com/jbase/r99")
                                            .Replace("http://jbase.com/r5", "https://static.zumasys.com/jbase/r99")
                                            .Replace("https://jbase.com/r5", "https://static.zumasys.com/jbase/r99")
                                            .Replace("www.jbase.com/r5", "https://static.zumasys.com/jbase/r99")
                                            .Replace("http://www.jbase.com/r99", "https://static.zumasys.com/jbase/r99")
                                            .Replace("http://jbase.com/r99", "https://static.zumasys.com/jbase/r99")
                                            .Replace("https://jbase.com/r99", "https://static.zumasys.com/jbase/r99")
                                            .Replace("www.jbase.com/r99", "https://static.zumasys.com/jbase/r99");
                    html = html.Replace(oldTarget, newTarget);
                } // Try and filter out non-HelpJuice links
                else if (oldTarget.Contains("http") && (!oldTarget.Contains("zumasys") && !oldTarget.Contains("jbase")))
                {
                    unconvertedLinks.Add(oldTarget);
                }
                else
                {
                    var coreTarget = Path.GetFileNameWithoutExtension(oldTarget)
                        .Replace("%20", String.Empty)
                        .Replace(".html", String.Empty)
                        .Replace(".htm", String.Empty);
                    // If the target contains an in-document anchor link, strip out for our question and category searches
                    var anchorTarget = String.Empty;
                    if (coreTarget.Contains("#"))
                    {
                        var anchorIndex = coreTarget.IndexOf("#");
                        anchorTarget = coreTarget.Substring(anchorIndex, coreTarget.Length - anchorIndex);
                        coreTarget = coreTarget.Substring(0, anchorIndex);
                    }
                    var localPath = String.Empty;
                    int questionId, categoryId;

                    // First see if link target is a record in processedQuestions where Target equals CodeName
                    localPath = processedQuestions.Where(q => q.Value.CodeName.Equals(coreTarget, StringComparison.CurrentCultureIgnoreCase))
                                                   .Select(q => Path.GetDirectoryName(q.Value.LocalPath))
                                                   .FirstOrDefault();

                    // If we didn't find a match, next try and find a valid question ID in the target
                    if (String.IsNullOrEmpty(localPath) && coreTarget.Contains("-"))
                    {
                        var foundQuestionId = Int32.TryParse(coreTarget.Substring(0, coreTarget.IndexOf('-')), out questionId);
                        if (foundQuestionId && processedQuestions.ContainsKey(questionId))
                        {
                            localPath = Path.GetDirectoryName(processedQuestions[questionId].LocalPath);
                        }
                    }

                    // If we still don't have a match, try and find based on the processedCategories where Target equals CodeName
                    if (String.IsNullOrEmpty(localPath))
                    {
                        localPath = processedCategories.Where(c => c.Value.CodeName.Equals(coreTarget, StringComparison.CurrentCultureIgnoreCase))
                                                        .Select(c => c.Value.LocalPath)
                                                        .FirstOrDefault();
                    }

                    // If we still didn't find a match, next try and find a valid category ID in the target
                    if (String.IsNullOrEmpty(localPath) && coreTarget.Contains("-"))
                    {
                        var foundCategoryId = Int32.TryParse(coreTarget.Substring(0, coreTarget.IndexOf('-')), out categoryId);
                        if (foundCategoryId && processedCategories.ContainsKey(categoryId))
                        {
                            localPath = processedCategories[categoryId].LocalPath;
                        }
                    }

                    if (!String.IsNullOrEmpty(localPath))
                    {
                        if (!String.IsNullOrEmpty(anchorTarget))
                        {
                            localPath = $"{localPath}{anchorTarget}";
                        }
                        var newPath = new StringBuilder(Path.GetRelativePath(Path.GetDirectoryName(filename), localPath))
                            .Replace(@"\", "/")
                            .Insert(0, "./");
                        // Take care to only replace the link
                        var newTag = oldTag.Value.Replace(oldTarget, newPath.ToString());
                        html = html.Replace(oldTag.Value, newTag);
                    }
                    else
                    {
                        unconvertedLinks.Add(oldTarget);
                    }
                }
            }
        }

        // Helper method for cleaning inbound HTML
        static void SanitizeHTML(ref string html)
        {
            // Someone put preformatted code inside a 1x1 table--a lot :|
            html = html.Replace("<table style=\"width: 100%;\"><tbody><tr><td style=\"width: 100%;\"><pre>", "<pre>")
                       .Replace("</pre></td></tr></tbody></table>", "</pre>");
        }
    }
}
