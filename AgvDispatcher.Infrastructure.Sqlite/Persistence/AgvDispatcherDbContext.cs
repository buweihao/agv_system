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

        public DbSet<MapNode> MapNodes => Set<MapNode>();

        public DbSet<MapEdge> MapEdges => Set<MapEdge>();

        public DbSet<TaskTemplateConfig> TaskTemplates => Set<TaskTemplateConfig>();

        public DbSet<ParameterConfig> SystemParameters => Set<ParameterConfig>();

        public DbSet<AlarmEvent> Alarms => Set<AlarmEvent>();

        public DbSet<OperationLog> OperationLogs => Set<OperationLog>();

        public DbSet<TaskOrder> TaskOrders => Set<TaskOrder>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureVehicle(modelBuilder);
            ConfigureChargeStation(modelBuilder);
            ConfigureMap(modelBuilder);
            ConfigureTaskConfig(modelBuilder);
            ConfigureAlarm(modelBuilder);
            ConfigureOperationLog(modelBuilder);
            ConfigureTaskOrder(modelBuilder);
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
                entity.OwnsOne(station => station.Position);
            });
        }

        private static void ConfigureMap(ModelBuilder modelBuilder)
        {
            var dictionaryConverter = CreateDictionaryConverter();
            var dictionaryComparer = CreateDictionaryComparer();

            modelBuilder.Entity<MapNode>(entity =>
            {
                entity.ToTable("MapNodes");
                entity.HasKey(node => node.NodeId);
                entity.Property(node => node.NodeId).HasMaxLength(64);
                entity.Property(node => node.MapId).HasMaxLength(64);
                entity.Property(node => node.NodeCode).HasMaxLength(64);
                entity.Property(node => node.Name).HasMaxLength(128);
                entity.OwnsOne(node => node.Position);
                entity.Property(node => node.Tags)
                    .HasConversion(dictionaryConverter)
                    .Metadata.SetValueComparer(dictionaryComparer);
                entity.Property(node => node.AllowedBrands).HasMaxLength(256);
            });

            modelBuilder.Entity<MapEdge>(entity =>
            {
                entity.ToTable("MapEdges");
                entity.HasKey(edge => edge.EdgeId);
                entity.Property(edge => edge.EdgeId).HasMaxLength(64);
                entity.Property(edge => edge.FromNodeId).HasMaxLength(64);
                entity.Property(edge => edge.ToNodeId).HasMaxLength(64);
                entity.Property(edge => edge.AllowedBrands).HasMaxLength(256);
                entity.HasIndex(edge => new { edge.FromNodeId, edge.ToNodeId });
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
