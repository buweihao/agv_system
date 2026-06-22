# AgvDispatcher.Tests

## 覆盖范围

- `Routing/`：路径规划、交通管制和路径预约的既有冒烟测试。
- `Dispatching/`：调度编排的任务选车、规划、预约、滚动窗口、车辆命令、推进和取消流程。
- `Map/MapStaticQueryServiceTests.cs`：`IMapService` 的只读静态地图查询，包括地图快照、节点、边、方向、节点类型和厂商节点映射。
- `Map/MapManagementServiceTests.cs`：`IMapManagementService` 的草稿创建与保存、校验、发布、版本回滚、事件、导入和导出。

## 不覆盖范围

地图测试不覆盖真实数据库、真实文件路径、WPF UI 初始化或区域导航，也不覆盖路径规划、交通管制、路径预约和车辆调度。后四项属于独立业务模块；将这些行为纳入地图模块测试会模糊边界，并使只读地图查询和地图编辑测试依赖无关的运行时状态。

`MapModule.RegisterTypes` 当前未做容器集成测试。Prism WPF/DryIoc 容器的完整启动需要 UI/Application 生命周期，容易让服务测试受线程和 WPF 初始化影响；服务的接口契约与实现已直接覆盖，模块注册代码保持为轻量组合根。

## InMemory 交通管制 MVP 资源模型

当前每个路径段锁定两类资源：该段的 `Edge`，以及该段目标点 `ToNode` 对应的 `Node`。资源状态仅保存在当前进程内。

当前阶段用于验证以下正式规则：

- 同一资源不能被两个 AGV 同时锁定。
- 车辆经过路径段后释放已通过资源，并继续申请下一滚动窗口。
- 取消或完成任务会释放该预约的全部剩余资源。
- `Blocked`/`Disabled` 资源触发 `RequiresReplan`。
- `Locked`/`Reserved`/`Occupied` 资源触发 `WaitingForTraffic`。

当前暂不处理路口区域、窄通道区域、双向边对向冲突、充电区容量、工位占用、优先级抢占，以及跨进程或重启后的状态恢复。这些能力留给后续持久化交通控制与路径预约实现。

## 执行

```powershell
dotnet test AgvDispatcher.Tests\AgvDispatcher.Tests.csproj --no-restore /m:1 /p:RunPostBuildEvent=Never
```
