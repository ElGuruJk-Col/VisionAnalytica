using System.Globalization;

namespace VisioAnalytica.App.Risk.Converters;

/// <summary>
/// Convierte un valor booleano a un color de borde.
/// True = Primary (seleccionado), False = Gray300 (no seleccionado)
/// </summary>
public class BoolToBorderColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return Application.Current?.Resources["Primary"] ?? Color.FromArgb("#483589");
        }
        return Application.Current?.Resources["Gray300"] ?? Color.FromArgb("#E0E0E0");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte un valor booleano a un grosor de borde.
/// True = 3px (seleccionado), False = 1px (no seleccionado)
/// </summary>
public class BoolToBorderThicknessConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return 3.0;
        }
        return 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte un valor booleano a un color de fondo para el checkbox.
/// True = Primary (seleccionado), False = Transparent (no seleccionado)
/// </summary>
public class BoolToCheckboxBgConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected && isSelected)
        {
            return Application.Current?.Resources["Primary"] ?? Color.FromArgb("#483589");
        }
        return Colors.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

