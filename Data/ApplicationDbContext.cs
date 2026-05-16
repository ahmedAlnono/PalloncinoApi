using Microsoft.EntityFrameworkCore;
using Palloncino.Models.Entities;
using Palloncino.Models.Enums;

namespace Palloncino.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // ========== DbSet Properties ==========

        // User Management
        public DbSet<User> Users { get; set; }
        public DbSet<Branch> Branches { get; set; }

        // Catalog & Inventory
        public DbSet<CatalogItem> CatalogItems { get; set; }
        public DbSet<Template> Templates { get; set; }
        public DbSet<TemplateItem> TemplateItems { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<InventoryMovement> InventoryMovements { get; set; }

        // Orders & Quotations
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Quotation> Quotations { get; set; }
        public DbSet<QuotationItem> QuotationItems { get; set; }
        public DbSet<Payment> Payments { get; set; }

        // Job Orders & Tasks
        public DbSet<JobOrder> JobOrders { get; set; }
        public DbSet<JobOrderItem> JobOrderItems { get; set; }
        public DbSet<JobOrderItemHistory> JobOrderItemHistories { get; set; }
        public DbSet<Models.Entities.Task> Tasks { get; set; }
        public DbSet<SubTask> SubTasks { get; set; }
        public DbSet<ChecklistItem> ChecklistItems { get; set; }

        // Communication
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // Attachments & Logs
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }

        public DbSet<UserDeviceToken> UserDeviceTokens { get; set; }

        public DbSet<DesignStatusHistory> DesignStatusHistories { get; set; }

        // ========== OnModelCreating - Fluent API Configurations ==========

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure SQLite specific settings
            if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
            {
                // SQLite doesn't support decimal precision, but we'll keep it for documentation
                foreach (var property in modelBuilder.Model.GetEntityTypes()
                    .SelectMany(t => t.GetProperties())
                    .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
                {
                    property.SetColumnType("decimal(18,2)");
                }
            }

            // ========== User Configuration ==========
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.Phone).IsUnique();
                entity.HasIndex(e => e.Role);
                entity.HasIndex(e => e.BranchId);

                entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Phone).IsRequired().HasMaxLength(20);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
                entity.Property(e => e.StripeCustomerId).HasMaxLength(200);

                // Relationships
                entity.HasOne(e => e.Branch)
                    .WithMany(b => b.Users)
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasData([
                    new User{
                            Id = 5,
                            FullName = "System Administrator",
                            Email = "admin@palloncino.com",
                            Phone = "0500000000",
                            PasswordHash = "$2a$11$M/iufdsSpA3jvv/8Oe1/eOS1ORuoGHXmj008HtGOOWuUp9x0ICwh6",
                            Role = UserRole.Admin,
                            Status = UserStatus.Active,
                            CreatedAt = new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc),
                            ProfileImageUrl = null,
                            BranchId = null,
                            LastLoginAt = null,
                            RefreshToken = null,
                            RefreshTokenExpiry = null,
                            StripeCustomerId = null
                    }
                ]);
            });

            // ========== Branch Configuration ==========
            modelBuilder.Entity<Branch>(entity =>
            {
                entity.HasIndex(e => e.Name).IsUnique();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Address).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.Property(e => e.ManagerName).HasMaxLength(200);
            });

            // ========== CatalogItem Configuration ==========
            modelBuilder.Entity<CatalogItem>(entity =>
            {
                entity.HasIndex(e => e.Sku).IsUnique();
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.IsRental);

                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Category).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Sku).HasMaxLength(50);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            });

            // ========== Template Configuration ==========
            modelBuilder.Entity<Template>(entity =>
            {
                entity.HasIndex(e => e.Category);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.BeforeDiscount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.AfterDiscount).HasColumnType("decimal(18,2)");
            });

            // ========== TemplateItem Configuration ==========
            modelBuilder.Entity<TemplateItem>(entity =>
            {
                entity.HasIndex(e => new { e.TemplateId, e.CatalogItemId }).IsUnique();

                entity.HasOne(e => e.Template)
                    .WithMany(t => t.TemplateItems)
                    .HasForeignKey(e => e.TemplateId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.CatalogItem)
                    .WithMany(c => c.TemplateItems)
                    .HasForeignKey(e => e.CatalogItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== InventoryItem Configuration ==========
            modelBuilder.Entity<InventoryItem>(entity =>
            {
                entity.HasIndex(e => e.Sku).IsUnique();
                entity.HasIndex(e => e.BranchId);
                entity.HasIndex(e => e.Status);

                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Sku).IsRequired().HasMaxLength(50);
                entity.Property(e => e.PurchasePrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SalePrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Unit).IsRequired().HasMaxLength(20);

                entity.HasOne(e => e.Branch)
                    .WithMany(b => b.InventoryItems)
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== Order Configuration ==========
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.PaymentStatus);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DeliveryFee).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CustomDesignDescription).HasMaxLength(2000);
                entity.Property(e => e.Notes).HasMaxLength(1000);
                entity.Property(e => e.RejectionReason).HasMaxLength(500);
                entity.Property(e => e.Address).HasMaxLength(500);

                entity.HasOne(e => e.Customer)
                    .WithMany(u => u.Orders)
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== OrderItem Configuration ==========
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.Property(e => e.ItemName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Order)
                    .WithMany(o => o.OrderItems)
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== Payment Configuration ==========
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasIndex(e => e.OrderId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.StripeCheckoutSessionId);
                entity.HasIndex(e => e.StripePaymentIntentId);

                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(10);
                entity.Property(e => e.StripeCheckoutSessionId).HasMaxLength(200);
                entity.Property(e => e.StripePaymentIntentId).HasMaxLength(200);
                entity.Property(e => e.StripeCustomerId).HasMaxLength(200);
                entity.Property(e => e.FailureReason).HasMaxLength(500);
                entity.Property(e => e.IdempotencyKey).HasMaxLength(100);

                entity.HasOne(e => e.Order)
                    .WithMany(o => o.Payments)
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== Quotation Configuration ==========
            modelBuilder.Entity<Quotation>(entity =>
            {
                entity.HasIndex(e => e.QuotationNumber).IsUnique();
                entity.HasIndex(e => e.OrderId);
                entity.HasIndex(e => e.Status);

                entity.Property(e => e.QuotationNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Notes).HasMaxLength(1000);
                entity.Property(e => e.PrintableVersion);

                entity.HasOne(e => e.Order)
                    .WithMany(o => o.Quotations)
                    .HasForeignKey(e => e.OrderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== QuotationItem Configuration ==========
            modelBuilder.Entity<QuotationItem>(entity =>
            {
                entity.Property(e => e.ItemName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(5,2)");
                entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasOne(e => e.Quotation)
                    .WithMany(q => q.QuotationItems)
                    .HasForeignKey(e => e.QuotationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== JobOrder Configuration ==========
            modelBuilder.Entity<JobOrder>(entity =>
            {
                entity.HasIndex(e => e.JobNumber).IsUnique();
                entity.HasIndex(e => e.BranchId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.DueAt);
                entity.HasIndex(e => e.AssignedToCoordinator);

                entity.Property(e => e.JobNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.SpecialInstructions).HasMaxLength(2000);
                entity.Property(e => e.DeliveryAddress).HasMaxLength(500);
                entity.Property(e => e.TotalCost).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalRevenue).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.SourceOrder)
                    .WithOne(o => o.JobOrder)
                    .HasForeignKey<JobOrder>(e => e.SourceOrderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Branch)
                    .WithMany(b => b.JobOrders)
                    .HasForeignKey(e => e.BranchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== JobOrderItem Configuration ==========
            modelBuilder.Entity<JobOrderItem>(entity =>
            {
                entity.HasIndex(e => e.JobOrderId);
                entity.HasIndex(e => e.InventoryItemId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Phase);

                entity.Property(e => e.ItemName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Sku).HasMaxLength(50);
                entity.Property(e => e.Unit).IsRequired().HasMaxLength(20);
                entity.Property(e => e.CostPerUnit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.SellingPricePerUnit).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DamageDeduction).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DeductionReason).HasMaxLength(500);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.ChecklistItemIds).HasMaxLength(500);
                entity.Property(e => e.ProofImageUrl).HasMaxLength(500);

                entity.HasOne(e => e.JobOrder)
                    .WithMany(jo => jo.JobOrderItems)
                    .HasForeignKey(e => e.JobOrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.InventoryItem)
                    .WithMany(i => i.JobOrderItems)
                    .HasForeignKey(e => e.InventoryItemId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== JobOrderItemHistory Configuration ==========
            modelBuilder.Entity<JobOrderItemHistory>(entity =>
            {
                entity.HasIndex(e => e.JobOrderItemId);
                entity.HasIndex(e => e.PerformedBy);
                entity.HasIndex(e => e.PerformedAt);

                entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Reason).HasMaxLength(500);

                entity.HasOne(e => e.JobOrderItem)
                    .WithMany()
                    .HasForeignKey(e => e.JobOrderItemId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== Task Configuration ==========
            modelBuilder.Entity<Palloncino.Models.Entities.Task>(entity =>
            {
                entity.HasIndex(e => e.JobOrderId);
                entity.HasIndex(e => e.AssignedTo);
                entity.HasIndex(e => e.CompletedBy);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.DueAt);

                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.SkipReason).HasMaxLength(500);

                entity.HasOne(e => e.JobOrder)
                    .WithMany(jo => jo.Tasks)
                    .HasForeignKey(e => e.JobOrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Assignee)
                    .WithMany(u => u.AssignedTasks)
                    .HasForeignKey(e => e.AssignedTo)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Completer)
                    .WithMany()
                    .HasForeignKey(e => e.CompletedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== SubTask Configuration ==========
            modelBuilder.Entity<SubTask>(entity =>
            {
                entity.HasIndex(e => e.TaskId);
                entity.HasIndex(e => e.IsCompleted);

                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(500);

                entity.HasOne(e => e.Task)
                    .WithMany(t => t.SubTasks)
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== ChecklistItem Configuration ==========
            modelBuilder.Entity<ChecklistItem>(entity =>
            {
                entity.HasIndex(e => e.TaskId);
                entity.HasIndex(e => e.Phase);
                entity.HasIndex(e => e.IsChecked);

                entity.Property(e => e.ItemName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.ProofImageUrl).HasMaxLength(500);

                entity.HasOne(e => e.Task)
                    .WithMany(t => t.ChecklistItems)
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== ChatMessage Configuration ==========
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasIndex(e => e.RoomType);
                entity.HasIndex(e => e.RoomId);
                entity.HasIndex(e => e.SenderId);
                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.Message).HasMaxLength(2000);
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.MentionedUserIds).HasMaxLength(500);

                entity.HasOne(e => e.Sender)
                    .WithMany(u => u.ChatMessages)
                    .HasForeignKey(e => e.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Task)
                    .WithMany(t => t.ChatMessages)
                    .HasForeignKey(e => e.RoomId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== Notification Configuration ==========
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasIndex(e => e.RecipientId);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.IsRead);
                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Body).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.RelatedEntityType).HasMaxLength(50);

                entity.HasOne(e => e.Recipient)
                    .WithMany(u => u.Notifications)
                    .HasForeignKey(e => e.RecipientId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== Attachment Configuration ==========
            modelBuilder.Entity<Attachment>(entity =>
            {
                entity.HasIndex(e => e.EntityId);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.UploadedBy);

                entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
                entity.Property(e => e.FileUrl).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.FileType).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);

                entity.HasOne(e => e.Uploader)
                    .WithMany()
                    .HasForeignKey(e => e.UploadedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== ActivityLog Configuration ==========
            modelBuilder.Entity<ActivityLog>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.EntityType);
                entity.HasIndex(e => e.EntityId);
                entity.HasIndex(e => e.Action);
                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
                entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Notes).HasMaxLength(1000);
                entity.Property(e => e.IpAddress).HasMaxLength(50);
                entity.Property(e => e.UserAgent).HasMaxLength(500);
                entity.Property(e => e.OldValues);
                entity.Property(e => e.NewValues);

                entity.HasOne(e => e.User)
                    .WithMany(u => u.ActivityLogs)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== InventoryMovement Configuration ==========
            modelBuilder.Entity<InventoryMovement>(entity =>
            {
                entity.HasIndex(e => e.InventoryItemId);
                entity.HasIndex(e => e.Type);
                entity.HasIndex(e => e.RelatedJobOrderId);
                entity.HasIndex(e => e.PerformedBy);
                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.Reason).HasMaxLength(500);

                entity.HasOne(e => e.InventoryItem)
                    .WithMany(i => i.InventoryMovements)
                    .HasForeignKey(e => e.InventoryItemId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.JobOrder)
                    .WithMany()
                    .HasForeignKey(e => e.RelatedJobOrderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<UserDeviceToken>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasIndex(e => e.IsActive);

                entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
                entity.Property(e => e.DeviceType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.DeviceModel).HasMaxLength(100);
                entity.Property(e => e.AppVersion).HasMaxLength(20);

                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DesignStatusHistory>(entity =>
            {
                entity.HasIndex(e => e.TaskId);
                entity.HasIndex(e => e.ChangedAt);

                entity.Property(e => e.PreviousStatus).HasMaxLength(50);
                entity.Property(e => e.NewStatus).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Notes).HasMaxLength(500);

                entity.HasOne(e => e.Task)
                    .WithMany()
                    .HasForeignKey(e => e.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ChangedByUser)
                    .WithMany()
                    .HasForeignKey(e => e.ChangedBy)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }

        // ========== Override SaveChanges for Auto-Audit ==========

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Auto-set CreatedAt and UpdatedAt
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is BaseEntity &&
                           (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entityEntry in entries)
            {
                var entity = (BaseEntity)entityEntry.Entity;

                if (entityEntry.State == EntityState.Added)
                {
                    entity.CreatedAt = DateTime.UtcNow;
                    entity.IsActive = true;
                    entity.IsDeleted = false;
                }
                else if (entityEntry.State == EntityState.Modified)
                {
                    entity.UpdatedAt = DateTime.UtcNow;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
