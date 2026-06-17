namespace AgvDispatcher.Core.Contracts.Map
{
    public enum MapNodeType
    {
        Unknown = 0,
        Normal = 1,
        WorkStation = 2,
        ChargeStation = 3,
        WaitingPoint = 4,
        ParkingPoint = 5,
        PickPoint = 6,
        PutPoint = 7,
        Door = 8,
        Elevator = 9
    }

    public enum MapEdgeDirection
    {
        Unknown = 0,
        OneWay = 1,
        Bidirectional = 2
    }

    public enum MapEdgeType
    {
        Unknown = 0,
        Normal = 1,
        MainRoad = 2,
        NarrowRoad = 3,
        Intersection = 4,
        ChargingRoad = 5,
        WorkRoad = 6
    }

    public enum MapAreaType
    {
        Unknown = 0,
        Normal = 1,
        WorkArea = 2,
        ChargingArea = 3,
        WaitingArea = 4,
        NarrowArea = 5,
        IntersectionArea = 6,
        BlockedArea = 7
    }
}
