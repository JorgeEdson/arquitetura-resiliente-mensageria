using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using worker_consumidor.Dominio;

namespace worker_consumidor.Infraestrutura.Persistencia.Propostas.Configurations
{
    public class PropostaConfiguration : IEntityTypeConfiguration<Proposta>
    {
        public void Configure(EntityTypeBuilder<Proposta> entity)
        {
            entity.ToTable("Propostas");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                  .ValueGeneratedNever();

            entity.Property(x => x.SolicitacaoCreditoId)
                  .IsRequired();

            entity.Property(x => x.IdempotencyKey)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(x => x.IdCliente)
                  .IsRequired();

            entity.Property(x => x.ValorSolicitado)
                  .IsRequired()
                  .HasColumnType("decimal(18,2)");

            entity.Property(x => x.ValorAprovado)
                  .IsRequired()
                  .HasColumnType("decimal(18,2)");

            entity.Property(x => x.ValorParcela)
                  .IsRequired()
                  .HasColumnType("decimal(18,2)");

            entity.Property(x => x.PrazoMeses)
                  .IsRequired();

            entity.Property(x => x.TipoProduto)
                  .IsRequired();

            entity.Property(x => x.TaxaJurosAnual)
                  .IsRequired()
                  .HasColumnType("decimal(5,2)"); // ex.: 18,50% a.a.

            entity.Property(x => x.DataSolicitacao)
                  .IsRequired();

            entity.Property(x => x.DataPrimeiraParcela)
                  .IsRequired();

            entity.Property(x => x.DataCriacaoProposta)
                  .IsRequired();

            entity.Property(x => x.StatusProposta)
                  .IsRequired()
                  .HasMaxLength(20);

            // Idempotência do consumidor: uma proposta por solicitação processada.
            entity.HasIndex(x => x.IdempotencyKey)
                  .IsUnique()
                  .HasDatabaseName("UX_Propostas_IdempotencyKey");
        }
    }
}
