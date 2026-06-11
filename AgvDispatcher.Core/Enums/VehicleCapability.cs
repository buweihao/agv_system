using System;

namespace AgvDispatcher.Core.Enums
{
    [Flags]
    public enum VehicleCapability
    {
        None = 0,
        Transfer = 1 << 0,          // 搬运
        Lift = 1 << 1,              // 顶升
        Fork = 1 << 2,              // 叉取
        Tow = 1 << 3,               // 牵引
        Roller = 1 << 4,            // 滚筒
        Charge = 1 << 5,            // 充电
        Patrol = 1 << 6,            // 巡检
        HeavyLoad = 1 << 7,         // 大载重
        NarrowAisle = 1 << 8,       // 小车通道
        AutoDoor = 1 << 9,          // 自动门交互
        Elevator = 1 << 10,         // 电梯交互
        ConveyorDock = 1 << 11,     // 输送线对接
        AutoCharge = 1 << 12        // 自动充电
    }
}
