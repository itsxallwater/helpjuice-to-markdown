using System;

namespace HelpjuiceConverter.Entities
{
    class Category
    {
        public int id { get; set; }
        public string name { get; set; }
        public string codename { get; set; }
        public int? parent_id { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public Uri url { get; set; }
    }
}