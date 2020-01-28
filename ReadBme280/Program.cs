using Sensors.Shared;

namespace ReadBme280
{
    public static class Program
    {
        public static int Main(string[] args) => new Main<ReadingService>().Run(args);
    }
}
