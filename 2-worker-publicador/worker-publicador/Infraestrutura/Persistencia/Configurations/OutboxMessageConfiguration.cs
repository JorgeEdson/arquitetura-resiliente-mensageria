using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using worker_publicador.Dominio;

namespace worker_publicador.Infraestrutura.Persistencia.Configurations
{
    public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> entity)
        {
            entity.ToTable("OutboxMessages");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                  .ValueGeneratedNever();

            entity.Property(x => x.SolicitacaoCreditoId)
                  .IsRequired();

            entity.Property(x => x.IdempotencyKey)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(x => x.TipoMensagem)
                  .IsRequired()
                  .HasMaxLength(100);

            entity.Property(x => x.Payload)
                  .IsRequired();

            entity.Property(x => x.Status)
                  .IsRequired();

            entity.Property(x => x.DataCriacao)
                  .IsRequired();

            entity.Property(x => x.Tentativas)
                  .IsRequired();

            entity.HasIndex(x => x.Status)
                  .HasDatabaseName("IX_Outbox_Status");

            entity.HasIndex(x => x.IdempotencyKey)
                  .HasDatabaseName("IX_Outbox_IdempotencyKey");
        }
    }
}
