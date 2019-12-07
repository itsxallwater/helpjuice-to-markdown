namespace HelpjuiceConverter.Entities
{
    class Answer
    {
        public int Id { get; set; }

        public int QuestionId { get; set; }

        public string Body { get; set; }
    }
}