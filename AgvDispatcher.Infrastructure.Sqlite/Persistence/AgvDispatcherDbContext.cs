using System.Text.Json;
using AgvDispatcher.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AgvDispatcher.Infrastructure.Sqlite.Persistence
{
    public class AgvDispatcherDbContext : DbContext
    {
        public AgvDispatcherDbContext(DbContextOptions<AgvDispatcherDbContext> options)
            : base(options)
        {
        }

        public DbSet<Vehicle> Vehicles => Set<Vehicle>();

        public DbSet<ChargeStation> ChargeStations => Set<ChargeStation>();

        public DbSet<ChargeSessionRecord> ChargeSessionRecords => Set<ChargeSessionRecord>();

        public DbSet<MapNode> MapNodes => Set<MapNode>();

        public DbSet<MapEdge> MapEdges => Set<MapEdge>();

        public DbSet<MapArea> MapAreas => Set<MapArea>();

        public DbSet<MapLocationAlias> MapLocationAliases => Set<MapLocationAlias>();

        public DbSet<MapVersionEntity> MapVersions => Set<MapVersionEntity>();

        public DbSet<TaskTemplateConfig> TaskTemplates => Set<TaskTemplateConfig>();

        public DbSet<ParameterConfig> SystemParameters => Set<ParameterConfig>();

        public DbSet<AlarmEvent> Alarms => Set<AlarmEvent>();

        public DbSet<OperationLog> OperationLogs => Set<OperationLog>();

        public DbSet<TaskOrder> TaskOrders => Set<TaskOrder>();
        public DbSet<TrafficResourceLockEntity> TrafficResourceLocks => Set<TrafficResourceLockEntity>();
        public DbSet<RouteReservationEntity> RouteReservations => Set<RouteReservationEntity>();
        public DbSet<RouteReservationSegmentEntity> RouteReservationSegments => Set<RouteReservationSegmentEntity>();
        public DbSet<RouteReservationEventEntity> RouteReservationEvents => Set<RouteReservationEventEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureVehicle(modelBuilder);
            ConfigureChargeStation(modelBuilder);
            ConfigureMap(modelBuilder);
            ConfigureMapVersion(modelBuilder);
            ConfigureMapLocationAlias(modelBuilder);
            ConfigureTaskConfig(modelBuilder);
            ConfigureAlarm(modelBuilder);
            ConfigureOperationLog(modelBuilder);
            ConfigureTaskOrder(modelBuilder);
            ConfigureTrafficReservations(modelBuilder);
        }

        private static void ConfigureTrafficReservations(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TrafficResourceLockEntity>(entity =>
            {
                entity.ToTable("TrafficResourceLocks"); entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.ResourceType, x.ResourceId }).IsUnique();
                entity.Property(x => x.ResourceId).HasMaxLength(128);
                entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken();
            });
            modelBuilder.Entity<RouteReservationEntity>(entity =>
            {
                entity.ToTable("RouteReservations"); entity.HasKey(x => x.ReservationId);
                entity.HasMany(x => x.Segments).WithOne(x => x.Reservation)
                    .HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(x => x.TaskId); entity.HasIndex(x => x.VehicleId);
            });
            modelBuilder.Entity<RouteReservationSegmentEntity>(entity =>
            {
                entity.ToTable("RouteReservationSegments"); entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.ReservationId, x.Sequence }).IsUnique();
            });
            modelBuilder.Entity<RouteReservationEventEntity>(entity =>
            {
                entity.ToTable("RouteReservationEvents"); entity.HasKey(x => x.EventId);
                entity.HasIndex(x => x.ReservationId);
            });
        }

        private static void ConfigureMapLocationAlias(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MapLocationAlias>(entity =>
            {
                entity.ToTable("MapLocationAliases");
                entity.HasKey(e => e.AliasId);
                entity.Property(e => e.AliasId).HasMaxLength(64);
                entity.Property(e => e.MapId).HasMaxLength(64);
                entity.Property(e => e.MapVersion).HasMaxLength(64);
                entity.Property(e => e.NodeId).HasMaxLength(64);
                entity.Property(e => e.AliasType).HasMaxLength(64);
                entity.Property(e => e.AliasValue).HasMaxLength(128);
                entity.Property(e => e.Brand).HasMaxLength(64);
                entity.Property(e => e.Remark).HasMaxLength(256);
                entity.HasIndex(e => new { e.MapId, e.MapVersion, e.Brand, e.AliasValue }).IsUnique();
            });
        }

        private static void ConfigureVehicle(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Vehicle>(entity =>
            {
                entity.ToTable("Vehicles");
                entity.HasKey(vehicle => vehicle.VehicleId);
                entity.Property(vehicle => vehicle.VehicleId).HasMaxLength(64);
                entity.Property(vehicle => vehicle.VehicleCode).HasMaxLength(64);
                entity.Property(vehicle => vehicle.Name).HasMaxLength(128);
                entity.Property(vehicle => vehicle.Brand).HasMaxLength(64);
                entity.Property(vehicle => vehicle.Model).HasMaxLength(64);
                entity.Property(vehicle => vehicle.AreaCode).HasMaxLength(32);
                
                entity.Property(vehicle => vehicle.AdapterType).HasMaxLength(64);
                entity.Property(vehicle => vehicle.ProtocolType).HasMaxLength(32);
                entity.Property(vehicle => vehicle.Endpoint).HasMaxLength(128);
                entity.Property(vehicle => vehicle.NavigationType).HasMaxLength(32);
                entity.Property(vehicle => vehicle.LoadMode).HasMaxLength(32);
                entity.Property(vehicle => vehicle.HomeNodeId).HasMaxLength(64);
                entity.Property(vehicle => vehicle.ChargeNodeId).HasMaxLength(64);

                entity.HasIndex(vehicle => vehicle.VehicleCode).IsUnique();
            });
        }

        private static void ConfigureChargeStation(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChargeStation>(entity =>
            {
                entity.ToTable("ChargeStations");
                entity.HasKey(station => station.StationId);
                entity.Property(station => station.StationId).HasMaxLength(64);
                entity.Property(station => station.StationCode).HasMaxLength(64);
                entity.Property(station => station.Name).HasMaxLength(128);
                entity.Property(station => station.NodeId).HasMaxLength(64);
                
                entity.Property(station => station.AllowedBrands).HasMaxLength(256);
                entity.Property(station => station.ProtocolType).HasMaxLength(64);
                entity.Property(station => station.Endpoint).HasMaxLength(128);

                entity.OwnsOne(station => station.Position);
            });

            modelBuilder.Entity<ChargeSessionRecord>(entity =>
            {
                entity.ToTable("ChargeSessionRecords");
                entity.HasKey(session => session.SessionId);
                entity.Property(session => session.SessionId).HasMaxLength(64);
                entity.Property(session => session.StationId).HasMaxLength(64);
                entity.Property(session => session.VehicleId).HasMaxLength(64);
                entity.Property(session => session.Status).HasMaxLength(64);
                entity.HasIndex(session => session.StationId);
                entity.HasIndex(session => session.VehicleId);
            });
        }

        private static void ConfigureMap(ModelBuilder modelBuilder)
        {
            var dictionaryConverter = CreateDictionaryConverter();
            var dictionaryComparer = CreateDictionaryComparer();

            modelBuilder.Entity<MapNode>(entity =>
            {
                entity.ToTable("MapNodes");
                entity.HasKey(node => new { node.MapId, node.MapVersion, node.NodeId });
                entity.Property(node => node.NodeId).HasMaxLength(64);
                entity.Property(node => node.MapId).HasMaxLength(64);
                entity.Property(node => node.MapVersion).HasMaxLength(64);
                entity.Property(node => node.NodeCode).HasMaxLength(64);
                entity.Property(node => node.Name).HasMaxLength(128);
                entity.OwnsOne(node => node.Position);
                entity.Property(node => node.Tags)
                    .HasConversion(dictionaryConverter)
                    .Metadata.SetValueComparer(dictionaryComparer);
                entity.Property(node => node.AllowedBrands).HasMaxLength(256);
                entity.HasIndex(node => new { node.MapId, node.MapVersion, node.NodeCode }).IsUnique();
            });

            modelBuilder.Entity<MapEdge>(entity =>
            {
                entity.ToTable("MapEdges");
                entity.HasKey(edge => new { edge.MapId, edge.MapVersion, edge.EdgeId });
                entity.Property(edge => edge.EdgeId).HasMaxLength(64);
                entity.Property(edge => edge.MapId).HasMaxLength(64);
                entity.Property(edge => edge.MapVersion).HasMaxLength(64);
                entity.Property(edge => edge.FromNodeId).HasMaxLength(64);
                entity.Property(edge => edge.ToNodeId).HasMaxLength(64);
                entity.Property(edge => edge.AllowedBrands).HasMaxLength(256);
                entity.Property(edge => edge.Remark).HasMaxLength(512);
                entity.HasIndex(edge => new { edge.FromNodeId, edge.ToNodeId });
            });

            modelBuilder.Entity<MapArea>(entity =>
            {
                entity.ToTable("MapAreas");
                entity.HasKey(area => new { area.MapId, area.MapVersion, area.AreaId });
                entity.Property(area => area.MapId).HasMaxLength(64);
                entity.Property(area => area.MapVersion).HasMaxLength(64);
                entity.Property(area => area.AreaId).HasMaxLength(64);
                entity.Property(area => area.AreaName).HasMaxLength(128);
                entity.Property(area => area.BoundaryJson).HasColumnType("TEXT");
                entity.Property(area => area.Properties)
                    .HasConversion(dictionaryConverter)
                    .Metadata.SetValueComparer(dictionaryComparer);
            });
        }

        private static void ConfigureMapVersion(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MapVersionEntity>(entity =>
            {
                entity.ToTable("MapVersions");
                entity.HasKey(version => version.Id);
                entity.Property(version => version.Id).HasMaxLength(64);
                entity.Property(version => version.MapId).HasMaxLength(64);
                entity.Property(version => version.MapVersion).HasMaxLength(64);
                entity.Property(version => version.Name).HasMaxLength(128);
                entity.Property(version => version.BaseMapId).HasMaxLength(64);
                entity.Property(version => version.BaseMapVersion).HasMaxLength(64);
                entity.Property(version => version.CreatedBy).HasMaxLength(64);
                entity.Property(version => version.Description).HasMaxLength(512);
                entity.HasIndex(version => new { version.MapId, version.MapVersion }).IsUnique();
                entity.HasIndex(version => version.IsActive);
            });
        }

        private static void ConfigureTaskConfig(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TaskTemplateConfig>(entity =>
            {
                entity.ToTable("TaskTemplates");
                entity.HasKey(template => template.TemplateName);
                entity.Property(template => template.TemplateName).HasMaxLength(128);
                entity.Property(template => template.TemplateType).HasMaxLength(64);
                entity.Property(template => template.Scene).HasMaxLength(128);
            });

            modelBuilder.Entity<ParameterConfig>(entity =>
            {
                entity.ToTable("SystemParameters");
                entity.HasKey(parameter => parameter.ParamKey);
                entity.Property(parameter => parameter.ParamKey).HasMaxLength(128);
                entity.Property(parameter => parameter.ParamName).HasMaxLength(128);
                entity.Property(parameter => parameter.ParamValue).HasMaxLength(512);
            });
        }

        private static void ConfigureAlarm(ModelBuilder modelBuilder)
        {
            var dictionaryConverter = CreateDictionaryConverter();
            var dictionaryComparer = CreateDictionaryComparer();

            modelBuilder.Entity<AlarmEvent>(entity =>
            {
                entity.ToTable("Alarms");
                entity.HasKey(alarm => alarm.AlarmId);
                entity.Property(alarm => alarm.AlarmId).HasMaxLength(64);
                entity.Property(alarm => alarm.AlarmCode).HasMaxLength(64);
                entity.Property(alarm => alarm.Name).HasMaxLength(128);
                entity.Property(alarm => alarm.Metadata)
                    .HasConversion(dictionaryConverter)
                    .Metadata.SetValueComparer(dictionaryComparer);
                entity.HasIndex(alarm => alarm.OccurredAt);
                entity.HasIndex(alarm => alarm.State);
                entity.HasIndex(alarm => alarm.VehicleId);
            });
        }

        private static void ConfigureOperationLog(ModelBuilder modelBuilder)
        {
            var dictionaryConverter = CreateDictionaryConverter();
            var dictionaryComparer = CreateDictionaryComparer();

            modelBuilder.Entity<OperationLog>(entity =>
            {
                entity.ToTable("OperationLogs");
                entity.HasKey(log => log.LogId);
                entity.Property(log => log.LogId).HasMaxLength(64);
                entity.Property(log => log.Category).HasMaxLength(64);
                entity.Property(log => log.Action).HasMaxLength(64);
                entity.Property(log => log.Metadata)
                    .HasConversion(dictionaryConverter)
                    .Metadata.SetValueComparer(dictionaryComparer);
                entity.HasIndex(log => log.OccurredAt);
                entity.HasIndex(log => log.Category);
                entity.HasIndex(log => log.VehicleId);
                entity.HasIndex(log => log.TaskId);
            });
        }

        private static void ConfigureTaskOrder(ModelBuilder modelBuilder)
        {
            var dictionaryConverter = CreateDictionaryConverter();
            var dictionaryComparer = CreateDictionaryComparer();

            modelBuilder.Entity<TaskOrder>(entity =>
            {
                entity.ToTable("TaskOrders");
                entity.HasKey(task => task.TaskId);
                entity.Property(task => task.TaskId).HasMaxLength(64);
                entity.Property(task => task.TaskNo).HasMaxLength(64);
                entity.Property(task => task.TemplateId).HasMaxLength(128);
                entity.Property(task => task.TaskType).HasMaxLength(64);
                entity.Property(task => task.SourceNodeId).HasMaxLength(64);
                entity.Property(e => e.TargetNodeId).HasMaxLength(64);
                entity.Property(e => e.CurrentNodeId).HasMaxLength(64);
                entity.Property(e => e.AssignedVehicleId).HasMaxLength(64);
                entity.Property(e => e.CargoCode).HasMaxLength(64);
                entity.Property(e => e.CargoName).HasMaxLength(128);
                entity.Property(e => e.CreatedBy).HasMaxLength(64);
                entity.Property(e => e.CancelReason).HasMaxLength(256);
                entity.Property(e => e.FailureReason).HasMaxLength(512);
                
                entity.Property(e => e.AllowedBrands).HasMaxLength(256);
                entity.Property(e => e.ForbiddenBrands).HasMaxLength(256);

                entity.Property(task => task.Attributes)
                    .HasConversion(dictionaryConverter)
                    .Metadata.SetValueComparer(dictionaryComparer);
                entity.HasIndex(task => task.State);
                entity.HasIndex(task => task.CreatedAt);
                entity.HasIndex(task => task.AssignedVehicleId);
            });
        }

        private static ValueConverter<Dictionary<string, string>, string> CreateDictionaryConverter()
        {
            return new ValueConverter<Dictionary<string, string>, string>(
                value => SerializeDictionary(value),
                value => string.IsNullOrWhiteSpace(value)
                    ? new Dictionary<string, string>()
                    : DeserializeDictionary(value));
        }

        private static ValueComparer<Dictionary<string, string>> CreateDictionaryComparer()
        {
            return new ValueComparer<Dictionary<string, string>>(
                (left, right) => SerializeDictionary(left) == SerializeDictionary(right),
                value => SerializeDictionary(value).GetHashCode(),
                value => DeserializeDictionary(SerializeDictionary(value)));
        }

        private static string SerializeDictionary(Dictionary<string, string>? value)
        {
            return JsonSerializer.Serialize(value ?? new Dictionary<string, string>(), (JsonSerializerOptions?)null);
        }

        private static Dictionary<string, string> DeserializeDictionary(string value)
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(value, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>();
        }
    }
}
