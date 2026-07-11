using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using worker_consumidor.Dominio;

namespace worker_consumidor.Infraestrutura.Persistencia.SolicitacoesRejeitadas.Configurations
{
    public class SolicitacaoRejeitadaConfiguration : IEntityTypeConfiguration<SolicitacaoRejeitada>
    {
        public void Configure(EntityTypeBuilder<SolicitacaoRejeitada> entity)
        {
            entity.ToTable("SolicitacoesRejeitadas");

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

            entity.Property(x => x.PrazoMeses)
                  .IsRequired();

            entity.Property(x => x.TipoProduto)
                  .IsRequired();

            entity.Property(x => x.DataSolicitacao)
                  .IsRequired();

            entity.Property(x => x.DataRejeicao)
                  .IsRequired();

            entity.Property(x => x.MensagemRejeicao)
                  .IsRequired()
                  .HasMaxLength(500);

            // Idempotência do consumidor: uma rejeição por solicitação processada.
            entity.HasIndex(x => x.IdempotencyKey)
                  .IsUnique()
                  .HasDatabaseName("UX_SolicitacoesRejeitadas_IdempotencyKey");
        }
    }
}
