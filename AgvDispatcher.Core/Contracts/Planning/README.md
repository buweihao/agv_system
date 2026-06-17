# 路径规划模块 Core 契约

本目录包含路径规划模块（Path Planning Module）的核心接口、请求模型、结果模型和枚举定义。这些定义旨在标准化调度系统与路径规划算法之间的交互边界。

## 模块职责

- 根据地图快照、起点、终点、途经点、车辆信息以及当前的动态约束，计算可行的行驶路径。
- 返回具体的路径段、总距离、预计耗时、转弯次数以及代价信息。
- 支持可达性判断。
- 支持查询最近的可达点。
- 支持返回多条候选路径（当配置要求时）。

## 不属于本模块的职责

- **不负责**：地图编辑、保存或发布。
- **不负责**：点位占用、区域占用等状态管理。
- **不负责**：路径资源的预约申请和滚动锁控制。
- **不负责**：决定派车策略或任务分配。
- **不负责**：控制 AGV 的实际运动，以及监控其是否需要停车、让路、重规划等。
- **不负责**：维护全局车辆实时状态。

## 与其他模块的协作关系（典型调用流程）

调度模块通过集成多个模块的数据，最终生成完整的参数传递给 `IPathPlanner`：

1. 调度模块从 `IMapService` 获取 `MapSnapshotDto`。
2. 调度模块从 `ITrafficControlService` 获取当前占用、禁行、管制等交通信息。
3. 调度模块从 `IRouteReservationService` 获取当前预约、锁定信息。
4. 调度模块将这些动态约束信息统一整理成 `PathPlanConstraint`。
5. 调度模块将组装好的请求对象 (`PathPlanRequest`) 传递并调用 `IPathPlanner.PlanAsync`。
6. 路径规划模块完成算法计算，返回 `PathPlanResult`。
7. 调度模块根据 `PathPlanResult`，再去调用预约服务申请路径锁定。
8. 锁定成功后，调度模块最终向车辆下发行驶指令。

**注意：**
- `IPathPlanner` **不直接**申请锁。
- `IPathPlanner` **不直接**控制车辆。
- `IPathPlanner` **不直接**修改地图。
- `IPathPlanner` **不决定**任务调度策略。
- `IPathPlanner` 只作为一个纯粹的“计算器”，根据输入数据计算并返回路径结果。

## 核心契约与模型说明

### IPathPlanner

统一入口接口，支持四种计算：
- `PlanAsync`: 规划单条推荐路径。
- `PlanAlternativesAsync`: 规划多条候选路径。
- `CheckReachabilityAsync`: 快速检查目标点可达性。
- `FindNearestReachableNodeAsync`: 从一组候选点中寻找最近可达点。

### PathPlanRequest

执行路径规划的主要请求结构，包含起点、终点、途经点、规划模式 (`PathPlanMode`)、车辆信息以及动态约束 (`PathPlanConstraint`)。

### PathPlanConstraint

包含了执行路径算法时的动态阻挡因素：
- 被占用的资源 (`OccupiedNodeIds`, `OccupiedEdgeIds`)
- 被预约的资源 (`ReservedNodeIds`, `ReservedEdgeIds`)
- 被交通管制的资源 (`ForbiddenNodeIds`, `ForbiddenEdgeIds`)

### PathPlanResult / PathSegmentDto

规划成功时返回的核心结果，包含了路线的整体摘要信息（距离、耗时、总代价）以及具体的运动拆分步骤 (`PathSegmentDto` 列表)。

### 失败码与事件 (PathPlanFailureCode & PathPlanEventType)

统一的枚举定义。失败码便于前台展示与后端排错（如 `TargetNodeNotFound`, `PathBlockedByTrafficControl` 等）；事件用于发送给事件总线以记录系统流水账。

## 同事开发实现模块时的注意事项

1. **不可引入外部依赖**：算法实现层应该尽量减少对数据库、Redis、RabbitMQ 或具体 AGV 厂商 SDK 的强依赖。算法的输入和输出必须完全基于此 Core 契约。
2. **纯粹计算**：不要在算法内部主动反查别的服务状态。所有的依赖数据都应通过 `PathPlanRequest` 由调用方传入。
3. **支持取消**：算法可能会面临非常大的地图图层数据，注意响应传入的 `CancellationToken` 以防止线程饥饿或内存泄漏。

## 示例调用代码

```csharp
var request = new PathPlanRequest
{
    Context = new RequestContext
    {
        RequestId = Guid.NewGuid().ToString("N"),
        TraceId = traceId,
        SourceModule = "DispatchService",
        RequestedAt = DateTimeOffset.UtcNow
    },
    MapSnapshot = mapSnapshot,
    VehicleId = "AGV-001",
    StartNodeId = "A001",
    TargetNodeId = "B010",
    WaypointNodeIds = Array.Empty<string>(),
    Mode = PathPlanMode.Balanced,
    Constraint = new PathPlanConstraint
    {
        ForbiddenNodeIds = forbiddenNodes,
        ForbiddenEdgeIds = forbiddenEdges,
        OccupiedNodeIds = occupiedNodes,
        OccupiedEdgeIds = occupiedEdges,
        ReservedNodeIds = reservedNodes,
        ReservedEdgeIds = reservedEdges,
        AvoidOccupiedResources = true,
        AvoidReservedResources = true,
        AllowReverse = false
    },
    MaxAlternativeCount = 1
};

var result = await pathPlanner.PlanAsync(request, cancellationToken);

if (!result.Success)
{
    // 调度模块根据错误码决定等待、重试、换车、人工干预
    // 可以通过 result.Code 和 result.Error.Code 获取具体原因
    return;
}

var path = result.Data;
```
