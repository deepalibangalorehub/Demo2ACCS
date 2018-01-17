namespace UniversalTennis.Algorithm.Jobs
{
    public interface IRatingHistoryService
    {
        void SaveDailyRatings(string algorithm);
        void SaveWeeklyAverage(string type, string algorithm);
    }
}
