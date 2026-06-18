# Route Reservation / Rolling Lock Module

该契约模块负责根据路径规划结果创建路径资源预约，并按滚动窗口申请、持有和释放路径资源。

## 职责边界

- 本模块负责路径资源预约和滚动锁。
- 本模块不负责路径规划；`IPathPlanner` 只返回 `PathPlanResult`，预约服务接收其中的 `PathSegmentDto`。
- 本模块不负责地图编辑或地图查询。
- 本模块不负责调度派车策略，也不维护任务生命周期。
- 本模块不负责车辆控制指令下发，也不直接修改车辆状态。
- 本模块不直接访问数据库、Redis、RabbitMQ 或厂商 SDK。
- 本模块通过 `ITrafficControlService` 申请和释放交通资源；交通控制服务只处理资源状态，不理解完整路径策略。

## 调用关系

调度模块应调用 `IRouteReservationService` 创建预约、推进滚动窗口和释放资源，不应把滚动锁策略直接写入 `DispatchService`。实现类负责把路径段适配为交通资源请求，并将资源不可用映射为等待、失败或需要重规划的结果。
