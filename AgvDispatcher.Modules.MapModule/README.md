# MapModule

`AgvDispatcher.Modules.MapModule` owns map-related UI and module-level service implementations.

## Responsibilities

- Implement `AgvDispatcher.Core.Contracts.Map.IMapService` for current published map static queries.
- Implement or reserve `AgvDispatcher.Core.Contracts.MapManagement.IMapManagementService` for map draft editing, validation, publishing, versioning, import, and export.
- Keep internal editor ViewModels, repositories, mappers, and mock implementations inside this module boundary.

## External Access Rules

Other modules must not reference MapModule internal models or implementation classes directly.

Allowed external integration points:

- `IMapService` for static read-only access to the current published runtime map.
- `IMapManagementService` for map management screens and tools.
- `MapPublishedEvent` as a signal that the current runtime map has changed.

When a module receives `MapPublishedEvent`, it should refresh its own cache or call `IMapService.GetCurrentMap`.

## Out Of Scope

MapModule must not own these responsibilities:

- Path planning, route search, path distance calculation, or path availability.
- Traffic control, point/edge occupancy state, rolling locks, or reservations.
- Dispatch decisions or vehicle control.

Path planning belongs to `AgvDispatcher.Core.Contracts.Planning.Interfaces.IPathPlanner` and its future implementation module. Runtime traffic state belongs to traffic-control and route-reservation services.

## Current Implementation

- `MockMapService` provides a static in-memory `IMapService` implementation for contract integration.
- `MockMapManagementService` provides an in-memory `IMapManagementService` placeholder for editor and management workflow development.
