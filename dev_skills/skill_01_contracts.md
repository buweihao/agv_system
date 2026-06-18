# Skill 01: 契约审计与地图格式去污

## 1. 目标

基于现有 `AgvDispatcher.Core.Contracts.Map` 和 `AgvDispatcher.Core.Contracts.MapManagement` 做审计、补缺和去污。禁止重新定义已有契约，禁止替换已有 DTO 的语义。

本步骤的核心是让地图 DTO 保持纯静态物理配置，不承载任何运行时交通状态。

## 2. 第一红线

- 禁止重建 `MapSnapshotDto`、`MapNodeDto`、`MapEdgeDto`。
- 禁止把版本管理状态塞进 `MapSnapshotDto`。
- 禁止在 `MapNodeDto` 中加入 `IsOccupied`、`CurrentAgvId`、`OccupiedByVehicleId`。
- 禁止在 `MapEdgeDto` 中加入 `IsLocked`、`ReservationId`、`TrafficLockState`。
- 禁止把路径规划、可达性、调度策略方法加到 `IMapService`。
- 禁止把保存、发布、回滚、导入导出方法加到 `IMapService`；这些只能在 `IMapManagementService`。

## 3. 允许改动

- 允许补充现有契约上的 XML 注释，使边界更清楚。
- 允许新增纯静态属性，但必须先说明为什么现有 `Properties` 不够用。
- 允许更新 README 或 sample JSON，去掉运行态字段。
- 允许新增转换辅助方法，但必须放在基础设施或专用 mapper 中，不污染 DTO。

## 4. 静态字段边界

如果需要补字段，先优先复用现有 DTO 和 `Properties`，只有字段属于稳定核心语义且多个模块都会读取时，才考虑加入强类型属性。

| 对象 | 允许的静态字段 | 禁止混入的运行态字段 |
| --- | --- | --- |
| 点位 `MapNodeDto` | 点位 ID、名称、类型、X/Y 坐标、朝向、所属区域、启用状态、静态容量、静态车辆通行约束、扩展静态属性 | 当前车辆、是否占用、占用车辆 ID、实时坐标、实时角度、当前任务 |
| 路线 `MapEdgeDto` | 路线 ID、起点、终点、方向、距离、启用状态、路线类型、所属区域、静态通行约束、扩展静态属性 | 是否锁定、预约 ID、当前占用车辆、交通锁状态、临时封控状态 |
| 区域 `MapAreaDto` | 区域 ID、名称、类型、边界、启用状态、静态容量、静态用途、扩展静态属性 | 当前区域车辆数、当前拥堵程度、实时管制状态 |
| 厂商映射 `VendorNodeMappingDto` | 厂商编码、系统点位 ID、厂商点位编码、厂商点位名称、扩展静态属性 | 厂商上报的车辆位置、`pointNo`/`lineNo` 实时状态、AGV 当前坐标 |
| 地图快照 `MapSnapshotDto` | 地图 ID、名称、版本、节点、路线、区域、厂商映射、静态扩展属性 | 当前运行统计、版本列表、草稿状态、发布流程状态、车辆实时状态 |

容量字段只表达“配置上最多允许多少”，不能表达“现在已经占了多少”。车辆通行约束只表达“哪些车型/品牌/能力标签允许通行”，不能表达“当前应该派哪台车”。

## 5. 具体执行步骤

1. 读取现有契约文件，列出现有字段，不重复造模型。
2. 检查 `MapNodeDto`、`MapEdgeDto`、`MapAreaDto`、`VendorNodeMappingDto` 是否只包含静态地图配置。
3. 检查 `MapManagement.Results.MapVersionDto` 是否承担版本管理状态；不要把 `IsDraft`、`IsCurrent`、发布人、发布时间迁移到 `MapSnapshotDto`。
4. 清理样例导入导出格式中的运行态字段，例如 `IsLocked`。
5. 明确旧 `Core.Models.MapNode/MapEdge` 的定位：旧数据库实体/兼容层，不作为新模块对外契约。

## 6. 输出标准

- 新代码仍引用现有 `AgvDispatcher.Core.Contracts.Map.IMapService`。
- `IMapService` 只有静态查询能力。
- `IMapManagementService` 只有草稿、校验、发布、版本、回滚、导入导出能力。
- 样例 JSON 中无运行态污染字段。
- 公共契约注释说明“静态地图”和“运行态交通状态”的边界。

## 7. 注释要求

在 DTO 或接口注释中直接写清楚：

- `IMapService` 读取当前已发布运行地图。
- `IMapManagementService` 管理草稿和版本。
- 保存草稿不触发 `MapPublishedEvent`。
- 只有发布或回滚当前运行地图才触发 `MapPublishedEvent`。
