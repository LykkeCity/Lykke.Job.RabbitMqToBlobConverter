using System;

namespace Lykke.Job.RabbitMqToBlobConverter.Services
{
    internal static class DateTimeConverter
    {
        private const string _format = "yyyy-MM-dd HH:mm:ss.fff";

        public static string Convert(DateTime dateTime)
        {
            return dateTime.ToString(_format);
        }
    }
}
