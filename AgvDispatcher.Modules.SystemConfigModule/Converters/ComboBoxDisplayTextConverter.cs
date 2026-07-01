using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace AgvDispatcher.Modules.SystemConfigModule.Converters
{
    /// <summary>
    /// Renders the collapsed ComboBox text with DisplayMemberPath, so view-model
    /// objects never leak their type names into the editor UI.
    /// </summary>
    public sealed class ComboBoxDisplayTextConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var selectedItem = GetValue(values, 0);
            var displayMemberPath = GetValue(values, 1) as string;
            var selectedValue = GetValue(values, 2);
            var editableText = GetValue(values, 3) as string;

            if (IsEmpty(selectedItem))
            {
                if (!string.IsNullOrWhiteSpace(editableText))
                {
                    return editableText;
                }

                return IsEmpty(selectedValue) ? string.Empty : selectedValue?.ToString() ?? string.Empty;
            }

            if (selectedItem is ComboBoxItem comboBoxItem)
            {
                return comboBoxItem.Content?.ToString() ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(displayMemberPath))
            {
                var displayValue = ResolvePath(selectedItem, displayMemberPath);
                if (!IsEmpty(displayValue))
                {
                    return displayValue?.ToString() ?? string.Empty;
                }
            }

            if (!IsEmpty(selectedValue) && !ReferenceEquals(selectedValue, selectedItem))
            {
                return selectedValue?.ToString() ?? string.Empty;
            }

            return selectedItem?.ToString() ?? string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static object GetValue(object[] values, int index)
            => values.Length > index ? values[index] : DependencyProperty.UnsetValue;

        private static bool IsEmpty(object value)
            => value == null || value == DependencyProperty.UnsetValue || value == Binding.DoNothing;

        private static object ResolvePath(object source, string path)
        {
            var current = source;
            foreach (var part in path.Split('.'))
            {
                if (current == null || string.IsNullOrWhiteSpace(part))
                {
                    return DependencyProperty.UnsetValue;
                }

                var property = current.GetType().GetProperty(
                    part,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

                if (property == null)
                {
                    return DependencyProperty.UnsetValue;
                }

                current = property.GetValue(current) ?? DependencyProperty.UnsetValue;
            }

            return current;
        }
    }
}
