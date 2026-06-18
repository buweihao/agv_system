# Skill 02: Mock 共享地图 Store 闭环

## 1. 目标

补齐 `MockMapService` 与 `MockMapManagementService` 的内存闭环，使它们共享同一个单例地图存储内核。验证草稿保存、地图发布、版本列表、回滚和事件触发规则。

本步骤仍然不接 SQLite。

## 2. 第一红线

- `MockMapService` 只能读当前已发布运行地图。
- `MockMapManagementService` 才能创建草稿、保存草稿、发布、回滚、导入导出。
- `SaveDraftAsync` 不能改变当前运行地图指针。
- `SaveDraftAsync` 不能触发 `MapPublishedEvent`。
- 只有 `PublishDraftAsync` 和 `RollbackToVersionAsync` 可以改变当前运行地图指针并触发 `MapPublishedEvent`。

## 3. 推荐设计

新增一个单例 `MockMapStore`，由容器注册为 singleton，并注入给两个服务。

建议内部结构：

- `Dictionary<string, MapDraftDto> Drafts`
- `Dictionary<string, MapSnapshotDto> PublishedSnapshots`
- `Dictionary<string, MapVersionDto> Versions`
- `Dictionary<string, string> CurrentVersionByMapId`
- 一个私有锁对象，保护发布、回滚和版本指针切换

版本 key 可使用 `$"{mapId}:{version}"`。草稿必须支持多个 `DraftId`，不要退化成单一 `DraftMap`，因为当前 `IMapManagementService` 已经有 `CreateDraftAsync` 和 `GetDraftAsync(DraftId)`。

Mock 初始数据必须覆盖核心静态字段：点位类型、坐标、区域、容量、启用状态、路线方向、路线距离、路线启用状态、区域、车辆通行约束和厂商点位映射。不要为了演示效果加入 `IsOccupied`、`IsLocked`、`ReservationId`。

## 4. 执行步骤

1. 保留现有 `IMapService` 同步方法签名，不改成 `GetCurrentMapAsync`。
2. 让 `MockMapService.GetCurrentMap` 从 `MockMapStore` 的当前版本指针读取快照。
3. `CreateDraftAsync`：
   - 如果指定 `SourceVersion`，从已发布快照复制为草稿。
   - 如果未指定，默认从当前运行图复制，或创建空草稿。
4. `SaveDraftAsync`：
   - 只更新 `Drafts[draftId]`。
   - 不写 `PublishedSnapshots`。
   - 不改 `CurrentVersionByMapId`。
   - 不发布事件。
5. `PublishDraftAsync`：
   - 先调用静态校验。
   - 校验失败直接返回失败结果。
   - 校验成功后生成新版本号或版本字符串。
   - 把草稿快照复制到 `PublishedSnapshots`。
   - 更新 `CurrentVersionByMapId`。
   - 在锁外发布 `MapPublishedEvent`。
6. `RollbackToVersionAsync`：
   - 校验目标版本存在。
   - 更新当前版本指针。
   - 在锁外发布 `MapPublishedEvent`。

## 5. 发布事件刷新规则

- `MockMapStore` 先完成当前版本指针切换，再触发 `MapPublishedEvent`。
- 事件 payload 只表达“地图已发布/已切换”的事实，不夹带完整快照。
- 路径规划、交通管制、监控等消费方收到事件后，只能重新调用 `IMapService` 读取当前地图。
- `MockMapManagementService` 不直接调用路径规划、交通管制、监控模块的刷新方法。

## 6. 验证场景

- 连续保存草稿 5 次，`IMapService.GetCurrentMap` 返回内容不变。
- 发布草稿后，`IMapService.GetCurrentMap` 返回新版本。
- 发布成功只触发一次 `MapPublishedEvent`。
- 回滚到历史版本后，当前运行图切换，并触发一次 `MapPublishedEvent`。
- 导入地图只生成草稿，不改变当前运行图。

## 7. 注释要求

在 `MockMapStore` 的版本指针、草稿区、发布区上写简洁中文注释，说明“草稿态”和“运行态”的区别。发布事件处必须注释说明为什么在锁外发布事件。
