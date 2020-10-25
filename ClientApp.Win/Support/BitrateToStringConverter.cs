using System;
using System.Globalization;
using System.Windows.Data;

namespace Streamster.ClientApp.Win.Support
{
    public class BitrateToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                var r = (((int)d) / 100) * 100;
                if (r >= 10000)
                {
                    d = (double)r / 1000;
                    return $"{d:F1}M";
                }
                return r.ToString();
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
