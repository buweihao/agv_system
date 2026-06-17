# 旧模型目录 (Legacy Models)

**注意：当前目录（`AgvDispatcher.Core.Models`）保留为旧版业务模型目录。**

根据最新的架构设计，为了明确模块间的边界，防止域模型泄露：

1. **新代码和跨模块接口**不要直接引用本目录下的地图业务模型（如 `MapNode`、`MapEdge` 等）。
2. 所有的跨模块通信和契约交互，应统一使用 `AgvDispatcher.Core.Contracts` 目录下的相关定义。
3. 针对地图对外提供的数据契约，请统一使用 `AgvDispatcher.Core.Contracts.Map` 命名空间下的 DTO（例如 `MapSnapshotDto`、`MapNodeDto`、`MapEdgeDto` 等）。

这里的类（如 `MapNode`, `MapEdge` 等）仅为兼容现有旧代码和逐步重构过渡保留，并且已打上 `Obsolete` 标记或添加 XML 注释以提醒开发者。
