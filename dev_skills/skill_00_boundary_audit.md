# Skill 00: 地图模块边界审计

## 1. 目标

在开始任何地图编辑与地图管理开发前，先审计当前代码边界，确认后续改动只是在现有契约上迁移和补齐能力，禁止重新发明地图契约。

本步骤不实现业务功能，只输出开发前判断和风险清单。

## 2. 第一红线

- 不新建第二套 `IMapService`。
- 不新建第二套 `IMapManagementService`。
- 不重定义现有 `AgvDispatcher.Core.Contracts.Map` DTO。
- 不把 `Core.Models.MapNode` / `Core.Models.MapEdge` 当成新的跨模块契约。
- 不把路径规划、可达性、交通管制、点位占用、车辆控制放进地图管理开发范围。

## 3. 当前契约基线

开发前必须先阅读并以这些文件为准：

- `AgvDispatcher.Core/Contracts/Map/IMapService.cs`
- `AgvDispatcher.Core/Contracts/Map/MapSnapshotDto.cs`
- `AgvDispatcher.Core/Contracts/Map/MapNodeDto.cs`
- `AgvDispatcher.Core/Contracts/Map/MapEdgeDto.cs`
- `AgvDispatcher.Core/Contracts/Map/VendorNodeMappingDto.cs`
- `AgvDispatcher.Core/Contracts/MapManagement/Interfaces/IMapManagementService.cs`
- `AgvDispatcher.Core/Contracts/MapManagement/Requests/MapManagementRequests.cs`
- `AgvDispatcher.Core/Contracts/MapManagement/Results/MapManagementResults.cs`
- `AgvDispatcher.Core/Events/MapPublishedEvent.cs`

## 4. 审计步骤

1. 搜索所有 `IMapService` 使用点，确认是否使用新版命名空间：
   `AgvDispatcher.Core.Contracts.Map.IMapService`。
2. 搜索旧模型使用点：
   `AgvDispatcher.Core.Models.MapNode`、`AgvDispatcher.Core.Models.MapEdge`。
   这些只能作为旧数据库实体或迁移兼容层存在，不能继续扩散为新模块接口。
3. 搜索运行态污染字段：
   `IsOccupied`、`OccupiedBy`、`CurrentAgvId`、`IsLocked`、`ReservationId`、`TrafficLock`。
4. 搜索地图事件发布点，确认只有发布和回滚运行地图时允许触发 `MapPublishedEvent`。
5. 检查 `docs/sample-data/MapConfig_Sample.json` 是否含有运行态字段；如果有，只能在后续 Skill 01 中作为格式去污处理。

## 5. 能力边界映射

审计时必须把地图模块能力与消费方写清楚。这里的“提供给谁”只表示静态数据消费关系，不表示地图模块直接调用这些业务模块。

| 地图能力 | 允许提供给 | 边界说明 |
| --- | --- | --- |
| 地图点位位置 | 地图静态查询模块、监控模块 | 只提供静态坐标、类型、区域、启用状态等，不提供车辆实时位置。 |
| 地图路线配置 | 地图静态查询模块、路径规划模块 | 只提供路线端点、方向、距离、启用状态、通行约束，不计算路径。 |
| 区域配置 | 交通管制模块、监控模块 | 只提供区域静态定义，不做交通资源分配。 |
| 点位/区域容量配置 | 交通管制模块 | 只提供最大容量等静态配置，不提供当前占用数。 |
| 点位启用/禁用状态 | 路径规划模块、监控模块 | 只表示静态配置是否启用，不表示当前是否可进入。 |
| 路线启用/禁用状态 | 路径规划模块、监控模块 | 只表示静态配置是否启用，不表示当前是否被锁定。 |
| 车辆通行约束配置 | 路径规划模块、调度模块 | 只提供车型、品牌、能力标签等静态约束，不判断某台车是否可派。 |
| 厂商点位映射配置 | 厂商适配模块、调度模块 | 只提供系统点位与厂商编码映射，不承载厂商实时坐标。 |
| 地图校验能力 | 地图发布流程、运维人员 | 只做静态一致性校验，不做可达性或死锁分析。 |
| 地图发布能力 | `IMapService`、路径规划模块、交通管制模块、监控模块 | 发布后切换当前运行地图，消费方应通过 `MapPublishedEvent` 感知并重新读取 `IMapService`。 |
| 地图版本能力 | 调度模块、持久化恢复模块 | 只提供已发布版本、当前版本和回滚能力，不把版本状态塞进 `MapSnapshotDto`。 |

## 6. 静态关键问题验收清单

后续每一步开发都要能回答这些问题，且答案只能来自当前已发布地图或地图草稿的静态配置：

- 地图上有哪些点？
- 每个点是什么类型？
- 点在哪里？
- 点属于哪个区域？
- 点是否启用？
- 点位容量是多少？
- 两点之间有没有路线？
- 路线是单向还是双向？
- 路线是否启用？
- 路线距离是多少？

不能把这些问题扩展成“车辆现在在哪”“点现在是否占用”“路径现在是否被锁”“任务该怎么调度”。

## 7. 审计输出

输出一份简短清单：

- 现有可复用契约。
- 仍在使用旧模型的模块。
- 需要去污的字段和文件。
- 本次阶段明确不碰的模块。
- 后续执行 01-05 的建议顺序。

## 8. 注释要求

后续开发只在公共契约、跨模块边界、非显然转换逻辑处添加简洁中文注释。不要为了“注释覆盖率”给每个普通属性或直白赋值写机械注释。
