# Map Management Contracts

`AgvDispatcher.Core.Contracts.MapManagement` defines the management-side contract for editing and publishing maps.

## IMapManagementService Scope

`IMapManagementService` is for map configuration screens, map editors, import/export tools, and administrator workflows. It is not part of the runtime dispatch decision path.

Allowed responsibilities:

- Create and load editable drafts.
- Save drafts without changing the current runtime map.
- Validate drafts before publication.
- Publish a draft as the current runtime map.
- List map versions.
- Roll back to a published version.
- Export and import map payloads.

## Publish Event Rule

Saving a draft must not publish `MapPublishedEvent`. Only a successful `PublishDraftAsync` or rollback that changes the current runtime map should publish `MapPublishedEvent`.

Subscribers should treat `MapPublishedEvent` as a signal that the current runtime map changed, then refresh caches or call `IMapService.GetCurrentMap`.

## Out Of Scope

This contract must not implement path planning, traffic control, resource reservation, rolling locks, AGV control, or occupancy management.
