using System.Globalization;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace VisioAnalytica.App.Risk.Converters;

/// <summary>
/// Convierte un nivel de riesgo a un color de borde.
/// </summary>
public class RiskLevelToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string riskLevel)
        {
            return riskLevel.ToUpper() switch
            {
                "ALTO" or "HIGH" => Application.Current?.Resources["Error"] ?? Color.FromArgb("#F44336"),
                "MEDIO" or "MEDIUM" or "MED" => Application.Current?.Resources["Warning"] ?? Color.FromArgb("#FF9800"),
                "BAJO" or "LOW" => Application.Current?.Resources["Success"] ?? Color.FromArgb("#4CAF50"),
                _ => Application.Current?.Resources["Gray600"] ?? Color.FromArgb("#757575")
            };
        }
        return Application.Current?.Resources["Gray600"] ?? Color.FromArgb("#757575");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte un nivel de riesgo a un color de fondo.
/// </summary>
public class RiskLevelToBgColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string riskLevel)
        {
            return riskLevel.ToUpper() switch
            {
                "ALTO" or "HIGH" => Color.FromArgb("#FFEBEE"), // Light red
                "MEDIO" or "MEDIUM" or "MED" => Color.FromArgb("#FFF3E0"), // Light orange
                "BAJO" or "LOW" => Color.FromArgb("#E8F5E9"), // Light green
                _ => Color.FromArgb("#F5F5F5") // Light gray
            };
        }
        return Color.FromArgb("#F5F5F5");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Convierte un nivel de riesgo a un color de texto.
/// </summary>
public class RiskLevelToTextColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string riskLevel)
        {
            return riskLevel.ToUpper() switch
            {
                "ALTO" or "HIGH" => Color.FromArgb("#C62828"), // Dark red
                "MEDIO" or "MEDIUM" or "MED" => Color.FromArgb("#E65100"), // Dark orange
                "BAJO" or "LOW" => Color.FromArgb("#2E7D32"), // Dark green
                _ => Application.Current?.Resources["Gray700"] ?? Color.FromArgb("#616161")
            };
        }
        return Application.Current?.Resources["Gray700"] ?? Color.FromArgb("#616161");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

