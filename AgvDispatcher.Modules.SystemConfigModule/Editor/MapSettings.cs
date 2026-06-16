using Prism.Mvvm;

namespace AgvDispatcher.Modules.SystemConfigModule.Editor
{
    /// <summary>
    /// 地图比例尺/原点设置（"比例尺叠加层"方案）。
    /// <para>
    /// 不改动 <see cref="Core.Models.MapPosition"/>（其 X/Y 仍为画布像素）。本类把画布像素坐标
    /// 与物理世界坐标（米）做相互换算，供编辑器角落读数与"米/像素"显示切换使用。
    /// 换算约定：world_m = (pixel - origin_pixel) / PixelsPerMeter，Y 轴向下为正（与画布一致）。
    /// </para>
    /// <para>持久化到系统参数仓储（键见 ViewModel：Map.PixelsPerMeter / Map.OriginX / Map.OriginY）。
    /// 缺省 PixelsPerMeter=1、原点=0 时退化为"1 像素 = 1 米 = 1 像素"，旧数据零影响。</para>
    /// </summary>
    public class MapSettings : BindableBase
    {
        private double _pixelsPerMeter = 50;
        /// <summary>每米对应的画布像素数（默认 50px/m）。必须为正，非正时取 1 兜底。</summary>
        public double PixelsPerMeter
        {
            get => _pixelsPerMeter;
            set => SetProperty(ref _pixelsPerMeter, value <= 0 ? 1 : value);
        }

        private double _originX;
        /// <summary>世界坐标原点对应的画布像素 X（默认 0）。</summary>
        public double OriginX
        {
            get => _originX;
            set => SetProperty(ref _originX, value);
        }

        private double _originY;
        /// <summary>世界坐标原点对应的画布像素 Y（默认 0）。</summary>
        public double OriginY
        {
            get => _originY;
            set => SetProperty(ref _originY, value);
        }

        private bool _showInMeters = true;
        /// <summary>角落读数是否以米显示（false 则显示画布像素）。</summary>
        public bool ShowInMeters
        {
            get => _showInMeters;
            set => SetProperty(ref _showInMeters, value);
        }

        /// <summary>画布像素 X → 世界 X（米）。</summary>
        public double ToWorldX(double pixelX) => (pixelX - OriginX) / PixelsPerMeter;

        /// <summary>画布像素 Y → 世界 Y（米）。</summary>
        public double ToWorldY(double pixelY) => (pixelY - OriginY) / PixelsPerMeter;

        /// <summary>把某个画布像素坐标格式化为角落读数文本，按 <see cref="ShowInMeters"/> 切换单位。</summary>
        /// <param name="pixelX">画布像素 X。</param>
        /// <param name="pixelY">画布像素 Y。</param>
        public string Format(double pixelX, double pixelY)
        {
            return ShowInMeters
                ? $"X={ToWorldX(pixelX):0.00} m  Y={ToWorldY(pixelY):0.00} m"
                : $"X={pixelX:0} px  Y={pixelY:0} px";
        }
    }
}
