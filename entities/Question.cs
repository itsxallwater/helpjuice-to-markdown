using System;
using System.Collections.Generic;

namespace HelpjuiceConverter.Entities
{
    class Question
    {        
        public int Id { get; set; }
        
        public string Name { get; set; }
        
        public int? Views { get; set; }
        
        public int AccountId { get; set; }
        
        public int Accessibility { get; set; }
        
        public string Description { get; set; }
        
        public string Email { get; set; }

        public bool IsPublished { get; set; }
        
        public string CodeName { get; set; }
        
        public int LanguageId { get; set; }
        
        public int? Position { get; set; }

        public int? SourceId { get; set; }

        public int? SourceType { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string AnswerSample { get; set; }

        public string LongAnswerSample { get; set; }
        
        public List<Category> Categories { get; set; }
        
        public List<string> Tags { get; set; }
    }
}