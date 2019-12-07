using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CsvHelper;
using HelpjuiceConverter.Entities;
using ReverseMarkdown;

namespace HelpjuiceConverter
{
    class Program
    {
        static IConfigurationRoot Configuration { get; set; }
        static HttpClient client = new HttpClient();
        static Converter markdownConverter;
        static Dictionary<string, string> secrets = new Dictionary<string, string>();
        static Dictionary<int, string> processedDirectories = new Dictionary<int, string>();
        static Dictionary<int, string> processedQuestions = new Dictionary<int, string>();

        static async Task Main(string[] args)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            Startup();

            Console.WriteLine("Converting Docs from HelpJuice to Markdown");

            await RunAsync();

            timer.Stop();
            Console.WriteLine("Processing Completed in " + timer.Elapsed.ToString());
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
            var config = new ReverseMarkdown.Config
            {
                GithubFlavored = true, // generate GitHub flavoured markdown, supported for BR, PRE and table tags
                UnknownTags = Config.UnknownTagsOption.Bypass
            };

            markdownConverter = new Converter(config);
        }

        // Process a HelpJuice site/account i.e. site.helpjuice.com
        static async Task ProcessSite(string site)
        {
            Console.WriteLine("Processing for " + site);

            var rootPath = "C:\\Temp\\Docs\\" + site + "\\";
            DirectoryHandler(rootPath);

            var key = secrets[site];
            await ProcessCategories(site, rootPath, key);
            await ProcessQuestions(site, rootPath, key);
            await ProcessAnswers(site, rootPath, key);

            processedDirectories.Clear();
            processedQuestions.Clear();
        }

        // Process HelpJuice Categories into directories
        static async Task ProcessCategories(string site, string rootPath, string key)
        {

            var url = new Uri("https://" + site + ".helpjuice.com/api/categories?api_key=" + key);
            var categories = await GetAsync<Category>(client, url);

            // Categories have parent/child relationship and unpack to directory/sub-directories
            Console.WriteLine("Converting " + categories.Count + " categories into directories");

            while (categories.Count > 0)
            {
                var tempCategories = new List<Category>(categories);
                foreach (var c in tempCategories)
                {
                    if (c.parent_id == null || (c.parent_id != null && processedDirectories.ContainsKey(c.parent_id.Value)))
                    {
                        var basePath = rootPath;
                        if (c.parent_id != null)
                        {
                            basePath = processedDirectories[c.parent_id.Value];
                        }

                        var fullPath = basePath + "\\" + c.name.Trim();
                        DirectoryHandler(fullPath);
                        processedDirectories.Add(c.id, fullPath);
                        categories.Remove(c);
                    }
                }
            }
        }

        // Process HelpJuice Questions into markdown files
        static async Task ProcessQuestions(string site, string rootPath, string key)
        {
            var page = 1;
            var hasMoreQuestions = true;
            while (hasMoreQuestions)
            {
                var url = new Uri("https://" + site + ".helpjuice.com/api/questions?page=" + page + "&api_key=" + key);
                var questions = await GetAsync<Question>(client, url);

                if (questions.Count == 0)
                {
                    hasMoreQuestions = false;
                }
                else
                {
                    Console.WriteLine("Converting " + questions.Count + " questions into files (page " + page.ToString() + ")");

                    foreach (var q in questions)
                    {
                        var filename = q.name.Trim() + ".md";
                        filename = filename.Replace("/", " & ");
                        filename = filename.Replace(Environment.NewLine, " & ");
                        filename = filename.Replace(":", String.Empty);
                        if (q.categories.Count > 0)
                        {
                            // Might be multiple categories but we'll just take the first
                            filename = processedDirectories[q.categories[0].id] + "\\" + filename;
                        }
                        else
                        {
                            // No category, goes into root
                            filename = rootPath + filename;
                        }

                        // File contents
                        var contents = "# " + q.name + Environment.NewLine;
                        contents += Environment.NewLine;
                        contents += "**Created At:** " + q.created_at.ToString() + "  " + Environment.NewLine;
                        contents += "**Updated At:** " + q.updated_at.ToString() + "  " + Environment.NewLine;
                        contents += Environment.NewLine;

                        if (q.tags.Count > 0)
                        {
                            contents += "**Tags:**" + Environment.NewLine;
                            foreach (var t in q.tags)
                            {
                                contents += "<badge text='" + t + "' vertical='middle' />" + Environment.NewLine;
                            }
                        }

                        FileHandler(filename, contents);

                        processedQuestions.Add(q.id, filename);
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
                var url = new Uri("https://" + site + ".helpjuice.com/api/answers?page=" + page + "&api_key=" + key);
                var answers = await GetAsync<Answer>(client, url);

                if (answers.Count == 0)
                {
                    hasMoreAnswers = false;
                }
                else
                {
                    Console.WriteLine("Converting " + answers.Count + " answers HTML into Markdown files (page " + page.ToString() + ")");

                    foreach (var a in answers)
                    {
                        if (processedQuestions.ContainsKey(a.question_id))
                        {
                            var filename = processedQuestions[a.question_id];
                            var content = a.body;
                            SanitizeHTML(ref content);
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
                        result = await response.Content.ReadAsAsync<List<T>>();
                        break;
                    default:
                        throw new NotImplementedException("HTTP Response Content Type " + response.Content.Headers.ContentType.MediaType + " not supported");
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

        // Helper method for cleaning inbound HTML
        static void SanitizeHTML(ref string html)
        {
            // Someone put preformatted code inside a 1x1 table--a lot :|
            html = html.Replace("<table style=\"width: 100%;\"><tbody><tr><td style=\"width: 100%;\"><pre>", "<pre>");
            html = html.Replace("</pre></td></tr></tbody></table>", "</pre>");
        }
    }
}
