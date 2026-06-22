# 调度编排层 Core 契约

## 职责

调度编排层负责串联任务校验、车辆选择、运行地图查询、交通状态查询、路径规划、路径预约滚动锁和车辆命令下发。它只协调各模块，不接管各模块内部的领域职责。

调度编排层明确不负责：

- 地图编辑、草稿、发布及版本管理；
- 路径规划算法；
- 交通资源状态、占用、锁定、封锁和释放的内部维护；
- 厂商通信协议与适配细节；
- UI 展示；
- 直接访问数据库、Redis 或 RabbitMQ。

## 典型流程

任务校验 → 车辆选择 → 读取当前已发布地图 → 读取交通快照 → 生成 `PathPlanConstraint` → 调用 `IPathPlanner` → 调用 `IRouteReservationService` 创建预约 → `AcquireNextWindow` 锁定首个滚动窗口 → 下发 `DispatchCommand` → 运行中调用 `AdvanceRouteAsync` 释放已通过资源并申请下一个窗口。

具体的地图查询、路径计算、交通资源管理、预约滚动锁和厂商命令发送仍由对应模块完成。

## 与旧 IDispatchService 的关系

旧 `IDispatchService` 继续保留，可作为 UI 和旧代码的兼容入口。后续实现可由旧入口委托 `IDispatchOrchestrationService`，逐步把跨模块流程迁移到新的调度编排层。
