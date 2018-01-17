namespace UniversalTennis.Algorithm.Models
{
    public class Config
    {
        public ConnectionStrings ConnectionStrings { get; set; }
        public string PlayerEventQueueName { get; set; }
        public string ResultEventQueueName { get; set; }
        public string UniversalTennisApiHost { get; set; }
        public string UniversalTennisApiVersion { get; set; }
        public string UniversalTennisApiToken { get; set; }
        public string OldestResultInMonths { get; set; }
    }

    public class ConnectionStrings
    {
        public string DefaultConnection { get; set; }
    }
}
