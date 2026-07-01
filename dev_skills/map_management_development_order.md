# 地图编辑与地图管理开发顺序

这份文档是后续开工顺序。每一步都以前一步验收通过为前提，避免在契约、Mock、UI、SQLite 之间来回返工。

## 0. 边界审计

对应文档：`skill_00_boundary_audit.md`

目标：

- 确认当前代码只保留一套 `IMapService` 和一套 `IMapManagementService`。
- 找出旧 `Core.Models.MapNode/MapEdge`、运行态字段和地图事件发布点。
- 明确本阶段只做静态地图配置，不做路径规划、占用、锁、调度和车辆控制。

验收：

- 输出现有可复用契约清单。
- 输出需要去污的字段和文件。
- 确认所有后续读取当前地图的模块只能走 `IMapService`。

## 1. 契约审计与格式去污

对应文档：`skill_01_contracts.md`

目标：

- 复用现有 `AgvDispatcher.Core.Contracts.Map` 和 `AgvDispatcher.Core.Contracts.MapManagement`。
- 补清楚静态字段边界：点位、路线、区域、容量、启用状态、车辆通行约束、厂商点位映射。
- 清理 sample JSON、导入导出 payload 中的运行态污染字段。

验收：

- DTO 中没有 `IsOccupied`、`IsLocked`、`ReservationId` 等运行态状态。
- `IMapService` 不包含保存、发布、回滚、导入导出。
- `IMapManagementService` 不包含路径规划、可达性、车辆控制。

## 2. Mock 共享 Store 闭环

对应文档：`skill_02_mock_store.md`

目标：

- 建立 `MockMapStore` 单例，让 `MockMapService` 和 `MockMapManagementService` 共享草稿区、发布区、版本指针。
- 先用内存闭环跑通创建草稿、保存草稿、校验、发布、版本列表、回滚、导入导出。

验收：

- 保存草稿不改变 `IMapService.GetCurrentMap` 的结果。
- 发布草稿后 `IMapService.GetCurrentMap` 返回新版本。
- 只有发布和回滚触发 `MapPublishedEvent`。
- 导入地图只生成草稿，不直接覆盖运行地图。

## 3. 纯静态地图校验器

对应文档：`skill_03_validator.md`

目标：

- 实现无 UI、无数据库、无路径规划依赖的静态校验器。
- 校验点位、路线、区域、容量、厂商映射和静态引用一致性。

验收：

- P0 错误阻止发布。
- P1 警告只提示运维确认。
- 校验器不调用路径规划、交通管制、车辆实时状态或任务调度。
- 校验消息能定位到元素类型和元素 ID。

## 4. 地图编辑 UI 草稿化迁移

对应文档：`skill_04_ui_migration.md`

目标：

- 把现有地图编辑 UI 从直接改库迁移为编辑草稿。
- 覆盖点位、路线、区域、容量、启用状态、车辆通行约束、厂商点位映射。
- UI 通过 `IMapManagementService` 做保存草稿、校验、发布、版本、回滚、导入导出。

验收：

- “保存”语义改成“保存草稿”。
- “发布地图”和“回滚版本”会提示它们会切换当前运行地图。
- UI 不直接使用 `IMapRepository` 保存地图。
- UI 不用 `IMapService` 做编辑、保存或发布。

## 5. SQLite 真实只读 IMapService

对应文档：`skill_05_persistence.md` 阶段 A

目标：

- 在 Mock 闭环稳定后，实现新版 `AgvDispatcher.Core.Contracts.Map.IMapService` 的真实静态查询。
- 从现有 SQLite 表读取当前地图并映射为新 DTO。

验收：

- `PersistentMapService` 只读，不保存、不发布、不回滚。
- mapper 只映射静态字段，过滤锁、占用、预约等运行态残留。
- 不把旧路径规划相关方法迁到新版 `IMapService`。

## 6. SQLite 版本化地图管理

对应文档：`skill_05_persistence.md` 阶段 B

目标：

- 实现草稿、发布版本、当前运行版本指针、回滚、导入导出的持久化闭环。
- 发布和回滚在事务提交成功后触发 `MapPublishedEvent`。

验收：

- 草稿和已发布版本分区存储。
- 当前运行版本指针可恢复。
- 回滚只切换当前运行版本，不重写历史快照。
- 文件读写留在 UI 层，服务层只处理 payload。

## 每一步固定检查

- 开始前先查看当前改动，避免覆盖用户已有修改。
- 修改公共契约前先确认是否真的需要强类型字段，能用已有 DTO 和 `Properties` 就不要扩契约。
- 每一步完成后优先构建主程序：

```powershell
dotnet build AgvDispatcher.Shell\AgvDispatcher.Shell.csproj --no-restore /m:1 /p:RunPostBuildEvent=Never
```

- 如果 Shell 构建因模块复制冲突失败，先确认是否是现有 PostBuild `xcopy` 共享问题，再单独构建相关模块。
