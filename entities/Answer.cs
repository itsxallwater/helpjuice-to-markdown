using System;

namespace HelpjuiceConverter.Entities
{
    class Answer
    {
        public int id { get; set; }
        public int question_id { get; set; }
        public string body { get; set; }
    }
}