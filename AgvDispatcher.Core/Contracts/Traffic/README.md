# 交通控制模块 Core 契约

本目录定义 AGV 调度系统中“运行时交通资源状态与控制契约”。它只描述接口、请求、DTO、枚举和事件，不包含具体交通控制算法实现，不访问数据库，不调用厂商协议，也不依赖 Redis、RabbitMQ、HTTP 或 UI。

## 模块职责

- 维护运行时交通资源状态，例如点位占用、边锁定、区域封锁、工位或充电桩预占。
- 判断节点、边、区域、工位、充电桩、电梯、门等资源是否可用。
- 处理资源的预留、占用、锁定、封锁、释放等动作。
- 向路径规划、路径预留、调度、监控等模块暴露交通状态快照。

## 与其他模块的边界

- `IMapService` 负责“路在哪里”：地图节点、边、区域、工位等静态或配置数据的读取、编辑、保存、校验和发布。
- `ITrafficControlService` 负责“现在这条路能不能走”：运行时占用、预留、锁定、封锁、释放和可用性判断。
- `IPathPlanner` 只负责算路径，可以读取 `TrafficSnapshotDto` 或由调用方整理后的交通约束，但不真正锁资源、不维护占用状态。
- `IRouteReservationService` 是路径滚动预留策略层，可以调用 `ITrafficControlService` 申请下一段资源；`ITrafficControlService` 不反向依赖它。
- `DispatchService` 决定任务、车辆和调度策略，不直接维护交通资源状态。

## 属于交通控制模块的状态

- 点位占用，例如 AGV 当前所在节点或安全包络覆盖的节点。
- 边占用或边锁定，例如 AGV 正在通过某条边，或窄路需要互斥通行。
- 区域封锁，例如人工维护、临时施工、消防联动导致某区域不可通行。
- 工位、充电桩、电梯、门等资源的预留、占用和封锁。
- 目的地预留，例如任务执行前提前锁定终点工位。

## 不属于交通控制模块的内容

- 地图编辑、地图保存、地图校验、地图发布。
- 路径搜索、最短路、可达性等规划算法本身。
- 任务分配、派车评分、任务生命周期编排。
- 厂商通信协议、车辆控制指令下发、HTTP 接口实现。
- 数据库、缓存、消息队列、UI 展示。

## 典型调用流程

1. 路径规划前，调度或规划编排方读取 `TrafficSnapshotDto`，将当前占用、预留、封锁等状态转为规划约束。
2. 路径预留服务根据滚动窗口调用 `TryAcquireAsync`，以 `TrafficLockMode.Reserve` 或 `TrafficLockMode.Lock` 锁定下一段路径资源。
3. AGV 状态上报后，车辆状态同步模块调用 `UpdateAgvOccupancyAsync`，刷新该车实际占用的节点和边。
4. AGV 离开资源后，调用 `ReleaseAsync` 释放对应预留、占用或锁定状态。
5. 人工封锁区域、边、门、电梯等资源时，调用 `BlockResourcesAsync`；恢复通行时调用 `UnblockResourcesAsync`。

## MapNode 与运行时状态

`MapNode`、`MapEdge` 等地图模型只应保存静态地图信息，不应增加 `IsOccupied`、`ReservedByAgvId` 这类运行时字段。运行时占用、预留、锁定、封锁等状态应通过 `TrafficResourceStatusDto`、`TrafficSnapshotDto` 和 `TrafficResourceChangedEvent` 表达。

## 主要入口

- `ITrafficControlService.GetTrafficSnapshotAsync`：读取当前交通状态快照。
- `ITrafficControlService.CheckAvailabilityAsync`：检查一组资源是否可用。
- `ITrafficControlService.TryAcquireAsync`：申请预留、占用、锁定或封锁资源。
- `ITrafficControlService.ReleaseAsync`：释放资源。
- `ITrafficControlService.UpdateAgvOccupancyAsync`：根据 AGV 上报刷新占用状态。
- `ITrafficControlService.BlockResourcesAsync` / `UnblockResourcesAsync`：人工或运营封锁、解封资源。
