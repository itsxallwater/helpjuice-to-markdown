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

            processedCategories.Clear();
            processedQuestions.Clear();
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
                            .Replace(":", string.Empty)
                            .Replace(" ", "-")
                            .ToLower();

                        var category = new Category();
                        if (q.Categories.Count > 0)
                        {
                            // Might be multiple categories but we'll just take the first
                            category = processedCategories[q.Categories[0].Id];
                            filename = Path.Combine(category.LocalPath, filename);
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

                        // File contents
                        var originalUrl = new StringBuilder()
                            .Append($"https://docs.{site.ToLower()}.com/");
                        if (category.CodeName != String.Empty)
                        {
                            originalUrl.Append(category.CodeName)
                                .Append("/");
                        }
                        originalUrl.Append(q.CodeName);

                        var content = new StringBuilder()
                            .Append($"# {q.Name}{Environment.NewLine}")
                            .Append(Environment.NewLine)
                            .Append($"**Created At:** {q.CreatedAt}  {Environment.NewLine}")
                            .Append($"**Updated At:** {q.UpdatedAt}  {Environment.NewLine}")
                            .Append($"**Original Doc:** [{q.CodeName}]({originalUrl})  {Environment.NewLine}")
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
                            content = await ImageHandler(filename, a.QuestionId, content);
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
                await ProcessSite(site.Key);
            }
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
        static async Task<string> ImageHandler(string filename, int questionId, string html)
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
                        oldSrc = oldSrc.Replace("http://www.jbase.com/r5", "https://static.zumasys.com/jbase/r99");
                    }

                    // Parse img name converting spaces to hyphens and lower casing
                    var imageName = Path.GetFileName(oldSrc)
                        .Replace(" ", "-")
                        .Replace("%20", "-")
                        .ToLower();

                    // Default blob files/extension-less files to jpg
                    if (Path.GetExtension(imageName).Equals(String.Empty))
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
                        Console.WriteLine(ex.Message);
                    }
                    finally
                    {
                        // Update src in html
                        newSrc = "./" + Path.GetRelativePath(Path.GetDirectoryName(filename), newSrc);
                        html = html.Replace(oldSrc, newSrc);
                    }
                }
            }

            return html;
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
