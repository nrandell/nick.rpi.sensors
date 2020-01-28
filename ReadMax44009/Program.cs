using Sensors.Shared;

namespace ReadMax44009
{
    public static class Program
    {
        public static int Main(string[] args) => new Main<ReadingService>().Run(args);
    }
}
