using CsvHelper.Configuration.Attributes;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HelpjuiceConverter.Entities
{
    class Question
    {
        [JsonPropertyName("id")]
        [Name("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        [Name("name")]
        public string Name { get; set; }

        [JsonPropertyName("views")]
        [Name("views")]
        public int? Views { get; set; }

        [JsonPropertyName("account_id")]
        [Name("account_id")]
        public int AccountId { get; set; }

        [JsonPropertyName("accessibility")]
        [Name("accessibility")]
        public int Accessibility { get; set; }

        [JsonPropertyName("description")]
        [Name("description")]
        public string Description { get; set; }

        [JsonPropertyName("email")]
        [Name("email")]
        public string Email { get; set; }

        [JsonPropertyName("is_published")]
        [Name("is_published")]
        public bool IsPublished { get; set; }

        [JsonPropertyName("codename")]
        [Name("codename")]
        public string CodeName { get; set; }

        [JsonPropertyName("language_id")]
        [Name("language_id")]
        public int LanguageId { get; set; }

        [JsonPropertyName("position")]
        [Name("position")]
        public int? Position { get; set; }

        [JsonPropertyName("source_id")]
        [Name("source_id")]
        public int? SourceId { get; set; }

        [JsonPropertyName("source_type")]
        [Name("source_type")]
        public int? SourceType { get; set; }

        [JsonPropertyName("created_at")]
        [Name("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        [Name("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("answer_sample")]
        [Name("answer_sample")]
        public string AnswerSample { get; set; }

        [JsonPropertyName("long_answer_sample")]
        [Name("long_answer_sample")]
        public string LongAnswerSample { get; set; }

        [JsonPropertyName("categories")]
        [Name("categories")]
        public List<Category> Categories { get; set; }

        [JsonPropertyName("tags")]
        [Name("tags")]
        public List<string> Tags { get; set; }

        // The local path this Question was mapped to
        [JsonIgnore]
        [Ignore]
        public string LocalPath { get; set; }
    }
}