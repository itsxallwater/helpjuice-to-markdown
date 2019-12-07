using System;

namespace HelpjuiceConverter.Entities
{
    class Category
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string CodeName { get; set; }

        public int? ParentId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Uri Url { get; set; }
    }
}