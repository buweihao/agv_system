using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AgvDispatcher.Core.Contracts.Map;
using Prism.Mvvm;

namespace AgvDispatcher.Modules.SystemConfigModule.Editor
{
    /// <summary>
    /// Map editor projection for a static area. Areas are static map configuration only:
    /// name, type, color, enabled state, and polygon boundary.
    /// </summary>
    public sealed class EditorAreaVm : BindableBase
    {
        private string _areaId;
        private string _areaName;
        private MapAreaType _areaType;
        private string _color;
        private bool _enabled;
        private string _boundaryText;
        private bool _isSelected;

        public EditorAreaVm(MapAreaDto area)
        {
            _areaId = area.AreaId;
            _areaName = area.AreaName;
            _areaType = area.AreaType;
            _enabled = area.Enabled;
            _color = area.Properties.TryGetValue("Color", out var color) && !string.IsNullOrWhiteSpace(color)
                ? color
                : DefaultColor(area.AreaType);
            _boundaryText = FormatBoundary(area.BoundaryPoints);
        }

        public string AreaId
        {
            get => _areaId;
            set
            {
                if (SetProperty(ref _areaId, value))
                {
                    RaisePropertyChanged(nameof(AreaDisplayName));
                    RaisePropertyChanged(nameof(AreaSelectorName));
                }
            }
        }

        public string AreaName
        {
            get => _areaName;
            set
            {
                if (SetProperty(ref _areaName, value))
                {
                    RaisePropertyChanged(nameof(AreaDisplayName));
                    RaisePropertyChanged(nameof(AreaSelectorName));
                }
            }
        }

        public MapAreaType AreaType
        {
            get => _areaType;
            set
            {
                if (SetProperty(ref _areaType, value))
                {
                    if (string.IsNullOrWhiteSpace(Color))
                    {
                        Color = DefaultColor(value);
                    }
                }
            }
        }

        public string Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, NormalizeColor(value)))
                {
                    RaisePropertyChanged(nameof(Fill));
                    RaisePropertyChanged(nameof(Stroke));
                }
            }
        }

        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (SetProperty(ref _enabled, value))
                {
                    RaisePropertyChanged(nameof(Opacity));
                }
            }
        }

        public string BoundaryText
        {
            get => _boundaryText;
            set
            {
                if (SetProperty(ref _boundaryText, value))
                {
                    RaisePropertyChanged(nameof(PointsText));
                    RaisePropertyChanged(nameof(LabelLeft));
                    RaisePropertyChanged(nameof(LabelTop));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    RaisePropertyChanged(nameof(StrokeThickness));
                }
            }
        }

        public string AreaDisplayName => string.IsNullOrWhiteSpace(AreaName)
            ? AreaId
            : $"{AreaId} - {AreaName}";

        public string AreaSelectorName => string.IsNullOrWhiteSpace(AreaName)
            ? AreaId
            : AreaName;

        public string PointsText => FormatBoundary(ParseBoundary(BoundaryText));

        public string Fill => ToAlphaColor(Color, "33");

        public string Stroke => Color;

        public double StrokeThickness => IsSelected ? 3 : 1;

        public double Opacity => Enabled ? 1.0 : 0.35;

        public double LabelLeft => ParseBoundary(BoundaryText).DefaultIfEmpty(new MapPointDto()).Average(point => point.X);

        public double LabelTop => ParseBoundary(BoundaryText).DefaultIfEmpty(new MapPointDto()).Average(point => point.Y);

        public MapAreaDto ToDto()
        {
            var properties = new Dictionary<string, string> { ["Color"] = Color };
            return new MapAreaDto
            {
                AreaId = AreaId.Trim(),
                AreaName = string.IsNullOrWhiteSpace(AreaName) ? AreaId.Trim() : AreaName.Trim(),
                AreaType = AreaType,
                BoundaryPoints = ParseBoundary(BoundaryText).ToList(),
                Enabled = Enabled,
                Properties = properties
            };
        }

        public static IReadOnlyList<MapPointDto> ParseBoundary(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return [];
            }

            var points = new List<MapPointDto>();
            foreach (var pair in text.Split(';', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries))
            {
                var parts = pair.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                {
                    continue;
                }

                if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var x)
                    && double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var y))
                {
                    points.Add(new MapPointDto { X = x, Y = y });
                }
            }

            return points;
        }

        private static string FormatBoundary(IEnumerable<MapPointDto> points)
            => string.Join("; ", points.Select(point =>
                $"{point.X.ToString("0.##", CultureInfo.InvariantCulture)},{point.Y.ToString("0.##", CultureInfo.InvariantCulture)}"));

        private static string NormalizeColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color))
            {
                return "#2D8CFF";
            }

            color = color.Trim();
            return color.StartsWith("#", System.StringComparison.Ordinal) ? color : $"#{color}";
        }

        private static string ToAlphaColor(string color, string alpha)
        {
            var normalized = NormalizeColor(color);
            return normalized.Length == 7 ? $"#{alpha}{normalized[1..]}" : normalized;
        }

        private static string DefaultColor(MapAreaType areaType) => areaType switch
        {
            MapAreaType.WorkArea => "#2D8CFF",
            MapAreaType.ChargingArea => "#9B6DFF",
            MapAreaType.WaitingArea => "#00BFA6",
            MapAreaType.NarrowArea => "#FFB020",
            MapAreaType.IntersectionArea => "#32D583",
            MapAreaType.BlockedArea => "#FF4D4F",
            _ => "#5A7FA6"
        };
    }
}
