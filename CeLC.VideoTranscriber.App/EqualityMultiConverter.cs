using System.Globalization;
using System.Windows.Data;

namespace SubtitleEditorDemo;

public class EqualityMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return false;
        if (values[0] is int indexFromRow && values[1] is int currentIndex)
            return indexFromRow == currentIndex;
        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}