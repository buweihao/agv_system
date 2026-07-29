using AgvDispatcher.Core.Enums;
using AgvDispatcher.Core.Rules;

namespace AgvDispatcher.Modules.MonitorWorkspaceModule.Models
{
    /// <summary>
    /// 杩愯鐩戞帶椤甸潰鐢ㄧ殑 AGV 琛岃鍥炬ā鍨嬨€?
    /// <para>
    /// 鐢辫溅杈嗙姸鎬佸揩鐓?<c>VehicleStatusSnapshot</c> 杞崲鑰屾潵锛屾壙杞?AGV 鍒楄〃/璇︽儏灞曠ず鎵€闇€鐨勫瓧娈碉紝
    /// 骞舵彁渚涗綆鐢甸噺鍒ゅ畾绛夋淳鐢熷睘鎬э紝涓撲緵鐩戞帶鐣岄潰缁戝畾浣跨敤锛堝尯鍒簬 Core 涓殑棰嗗煙妯″瀷 <c>Vehicle</c>锛夈€?
    /// </para>
    /// </summary>
    public class RobotModel
    {
        /// <summary>AGV 鍞竴缂栧彿锛堝 <c>AGV-001</c>锛夈€?/summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>杞﹁締鍝佺墝/鍨嬪彿鏍囪瘑锛堝 <c>RGV-A</c>锛夈€?/summary>
        public string Brand { get; set; } = string.Empty;

        /// <summary>褰撳墠杩愯鐘舵€侊紙绌洪棽/杩愯/鏁呴殰/绂荤嚎锛岃 <see cref="RobotState"/>锛夈€?/summary>
        public RobotState State { get; set; }

        public string StateDisplay => State switch
        {
            RobotState.Running => "\u8fd0\u884c\u4e2d",
            RobotState.Idle => "\u7a7a\u95f2",
            RobotState.Fault => "\u6545\u969c",
            RobotState.Offline => "\u79bb\u7ebf",
            _ => State.ToString()
        };
        /// <summary>褰撳墠鎵ц鐨勪换鍔＄紪鍙凤紱鏃犱换鍔℃椂閫氬父鏄剧ず涓?"-"銆?/summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>褰撳墠鎵€鍦ㄤ綅缃紙鑺傜偣缂栧彿鎴栦綅缃埆鍚嶏級銆?/summary>
        public string CurrentPosition { get; set; } = string.Empty;

        /// <summary>鐢甸噺鐧惧垎姣旓紙0~100锛夈€?/summary>
        public int BatteryLevel { get; set; }

        /// <summary>褰撳墠閫熷害锛坢/s锛夈€?/summary>
        public double Speed { get; set; }

        /// <summary>宸茶繍琛屾椂闀跨殑鏄剧ず鏂囨湰锛堝 <c>02:35:23</c>锛夈€?/summary>
        public string RunningTime { get; set; } = string.Empty;

        /// <summary>鏄惁浣庣數閲忥紝渚濇嵁 <see cref="VehicleStatusRules.IsLowBattery"/> 鐨勭粺涓€闃堝€煎垽瀹氥€?/summary>
        public bool IsLowBattery => VehicleStatusRules.IsLowBattery(BatteryLevel);

        /// <summary>鐢甸噺鐘舵€佹枃鏈紝浣庣數閲忔樉绀?<c>LOW</c>锛屽惁鍒欐樉绀?<c>OK</c>銆?/summary>
        public string BatteryStatusText => IsLowBattery ? "LOW" : "OK";
    }
}

