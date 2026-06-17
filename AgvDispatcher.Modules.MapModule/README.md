# MapModule (地图模块)

本模块的核心职责是提供与地图相关的服务，并且作为地图功能的独立开发边界。

## 契约与边界

1. **实现标准接口**：本模块必须负责实现 `AgvDispatcher.Core.Contracts.Map.IMapService`。
2. **内部自治**：模块内部的 Domain、Services、Repositories 和 Mappers 结构由本模块的开发者自行决定，外部不干涉。
3. **禁止直接引用**：其他业务模块（如任务模块、调度模块等）**绝对不得**直接引用本模块（`AgvDispatcher.Modules.MapModule`）内部的任何模型或实现类。
4. **统一对外交互**：外部与其他模块交互时，只能通过 `IMapService` 及其对应的 DTO（如 `MapSnapshotDto`、`MapNodeDto` 等）进行数据获取与调用。

## 当前实现状态

- 目前对外提供 `MockMapService` 作为演示与契约联调使用。
- 本模块暂时不负责真实业务中的流量管制、任务调度、以及路径锁定。这些行为由其他专用服务处理，地图模块仅负责回答“地图上有什么”的静态结构问题。
