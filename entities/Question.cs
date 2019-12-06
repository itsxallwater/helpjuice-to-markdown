using System;
using System.Collections.Generic;

namespace HelpjuiceConverter.Entities
{
    class Question
    {
        public int id { get; set; }
        public string name { get; set; }
        public int? views { get; set; }
        public int account_id { get; set; }
        public int accessibility { get; set; }
        public string description { get; set; }
        public string email { get; set; }
        public Boolean is_published { get; set; }
        public string codename { get; set; }
        public int language_id { get; set; }
        public int? position { get; set; }
        public int? source_id { get; set; }
        public int? source_type { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public string answer_sample { get; set; }
        public string long_answer_sample { get; set; }
        public List<Category> categories { get; set; }
        public List<string> tags { get; set; }
    }
}