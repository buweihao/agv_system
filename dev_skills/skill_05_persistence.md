# Skill 05: SQLite 真实只读查询与版本化持久化

## 1. 目标

在 Mock 闭环和 UI 草稿化验证通过后，再接入 SQLite。先实现真实 `IMapService` 只读静态查询，再实现版本化地图管理持久化。

本步骤分两阶段执行，避免一次性改动过大。

## 2. 第一红线

- 外部导入必须“导入即草稿”，不能直接覆盖当前运行地图。
- 回滚历史版本会切换当前运行地图，必须触发 `MapPublishedEvent`。
- `PersistentMapService` 只实现 `AgvDispatcher.Core.Contracts.Map.IMapService` 的静态查询能力。
- `PersistentMapService` 不做保存、发布、回滚、导入、导出。
- SQLite 实体中的任何运行态残留字段不得映射到 `MapSnapshotDto`。

## 3. 阶段 A: 真实只读 IMapService

先实现新版 `Contracts.Map.IMapService`：

1. 从现有 SQLite 地图表读取当前地图节点、边、别名。
2. 使用 mapper 转为 `MapSnapshotDto`、`MapNodeDto`、`MapEdgeDto`、`VendorNodeMappingDto`。
3. mapper 是安全转换屏障：
   - 只映射静态字段。
   - 不映射 `IsLocked`、占用、预约、交通状态。
   - 旧实体字段名与新 DTO 字段名不一致时，在 mapper 中显式转换。
4. 实现 `GetCurrentMap`、`GetNodes`、`GetEdges`、`GetNode`、`GetEdge`、`NodeExists`、`EdgeExists`、`GetOutgoingEdges`、`GetNodesByType`、厂商点位映射查询。

如果现有数据库没有版本表，阶段 A 可以先把现有表视为当前运行图，但必须在注释中说明这是过渡实现。

## 4. 静态字段映射要求

SQLite 旧实体到新 DTO 的 mapper 必须显式映射静态字段，并过滤运行态残留。

| 旧数据/字段类别 | 新 DTO 映射方向 | 注意事项 |
| --- | --- | --- |
| 点位坐标、类型、名称、编码 | `MapNodeDto` | 坐标和类型是静态地图信息，可以映射。 |
| `ParkingCapacity`、区域容量、最大流量配置 | `MapNodeDto` / `MapAreaDto` / `Properties` | 只表示配置容量，不表示当前占用。 |
| 路线端点、距离、方向、启用状态 | `MapEdgeDto` | 不把交通锁、预约状态映射进去。 |
| `AreaCode`、区域名称、区域类型 | `MapAreaDto` 或节点/路线区域引用 | 如果旧库没有区域表，阶段 A 可先从点位/路线区域编码派生区域快照，并加过渡注释。 |
| `AllowedBrands`、车型限制、能力标签 | 点位/路线静态约束或 `Properties` | 只表示允许规则，不做调度判断。 |
| 厂商点位别名、厂商编码 | `VendorNodeMappingDto` | 厂商 AGV 实时 `pointNo`、`lineNo`、`x/y/angle` 不属于地图 DTO。 |

不要把旧持久化服务里的 `FindPlannedPath`、`IsPathAvailable`、`FindReachableNodes` 等路径相关能力迁入新版 `PersistentMapService`。

## 5. 阶段 B: 版本化持久化

在阶段 A 稳定后，再设计并实现版本化存储。

建议持久化内容：

- 地图草稿表或草稿 JSON payload。
- 已发布版本表。
- 版本快照 JSON payload。
- 当前运行版本指针。
- 发布人、发布时间、备注。

发布流程：

1. 读取草稿。
2. 静态校验。
3. 保存发布版本快照。
4. 更新当前运行版本指针。
5. 提交事务。
6. 事务提交成功后发布 `MapPublishedEvent`。

回滚流程：

1. 确认目标版本存在。
2. 更新当前运行版本指针。
3. 提交事务。
4. 事务提交成功后发布 `MapPublishedEvent`。

## 6. 导入导出边界

当前 `IMapManagementService` 是 payload 级接口：

- `ExportMapAsync` 返回 `MapExportResultDto.Payload`。
- `ImportMapAsync` 接收 `ImportMapRequest.Payload`。

文件选择、`File.ReadAllText`、`File.WriteAllText` 应放在 UI 层或工具层，不要塞进服务契约。写文件时统一使用 UTF-8，避免中文点位名乱码。

## 7. 注册策略

注册时保持清晰：

- Mock 阶段注册 `MockMapStore`、`MockMapService`、`MockMapManagementService`。
- SQLite 阶段注册 `PersistentMapService` 和真实管理服务。
- 不要同时注册两个相同契约实现，避免容器解析不可预测。

## 8. 注释要求

在持久化 mapper、当前版本指针切换、事务提交后事件发布处写中文注释。注释重点说明边界和原因，不解释显而易见的赋值语句。
