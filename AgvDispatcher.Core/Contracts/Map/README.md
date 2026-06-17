# Map Contracts

`AgvDispatcher.Core.Contracts.Map` defines the read-only contract for the current published runtime map.

## IMapService Scope

`IMapService` answers static map questions for the map version that has already been published as the current runtime map.

Allowed responsibilities:

- Get the current published `MapSnapshotDto`.
- Query static nodes, edges, areas, and vendor node mappings.
- Check whether a static node or edge exists.
- Query outgoing edges from a node.
- Query nodes by static node type.
- Convert between system node IDs and vendor node codes.

## Out Of Scope

`IMapService` must not contain or grow these responsibilities:

- Path finding, path preview, route search, or route scoring.
- Resource reservation, rolling locks, traffic control, or occupancy state.
- Vehicle control or dispatch decisions.
- Map draft editing, saving, validation, publishing, rollback, import, or export.

Runtime traffic state belongs to traffic-control and route-reservation contracts, not map DTOs. Do not add fields such as `IsOccupied`, `OccupiedByVehicleId`, `IsLocked`, `ReservationId`, or `TrafficLockState` to `MapNodeDto` or `MapEdgeDto`.

Map editing and publishing belong to `AgvDispatcher.Core.Contracts.MapManagement.IMapManagementService`.

## Legacy Note

New code must depend on `AgvDispatcher.Core.Contracts.Map.IMapService`. Do not introduce or depend on a legacy `AgvDispatcher.Core.Interfaces.IMapService` compatibility interface.
