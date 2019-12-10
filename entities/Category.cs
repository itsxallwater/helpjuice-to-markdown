using CsvHelper.Configuration.Attributes;
using System;
using System.Text.Json.Serialization;

namespace HelpjuiceConverter.Entities
{
    class Category
    {
        [JsonPropertyName("id")]
        [Name("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        [Name("name")]
        public string Name { get; set; }

        [JsonPropertyName("codename")]
        [Name("codename")]
        public string CodeName { get; set; }

        [JsonPropertyName("parent_id")]
        [Name("parent_id")]
        public int? ParentId { get; set; }

        [JsonPropertyName("created_at")]
        [Name("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        [Name("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [JsonPropertyName("url")]
        [Name("url")]
        public Uri Url { get; set; }
    }
}