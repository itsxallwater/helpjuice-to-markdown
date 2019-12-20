using CsvHelper.Configuration.Attributes;
using System.Text.Json.Serialization;

namespace HelpjuiceConverter.Entities
{
    class Answer
    {
        [JsonPropertyName("id")]
        [Name("id")]
        public int Id { get; set; }

        [JsonPropertyName("question_id")]
        [Name("question_id")]
        public int QuestionId { get; set; }

        [JsonPropertyName("body")]
        [Name("body")]
        public string Body { get; set; }

        // The local path this Category was mapped to
        [JsonIgnore]
        [Ignore]
        public string LocalPath { get; set; }
    }
}