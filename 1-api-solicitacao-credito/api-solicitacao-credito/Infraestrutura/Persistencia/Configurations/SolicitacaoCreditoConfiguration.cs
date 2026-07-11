using api_solicitacao_credito.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace api_solicitacao_credito.Infraestrutura.Persistencia.Configurations
{
    public class SolicitacaoCreditoConfiguration : IEntityTypeConfiguration<SolicitacaoCredito>
    {
        public void Configure(EntityTypeBuilder<SolicitacaoCredito> entity)
        {
            entity.ToTable("SolicitacoesCredito");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                  .ValueGeneratedNever();

            entity.Property(x => x.IdempotencyKey)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.Property(x => x.IdCliente)
                  .IsRequired();

            entity.Property(x => x.ValorSolicitado)
                  .HasColumnType("decimal(18,2)")
                  .IsRequired();

            entity.Property(x => x.PrazoMeses)
                  .IsRequired();

            entity.Property(x => x.TipoProduto)
                  .IsRequired();

            entity.Property(x => x.DataSolicitacao)
                  .IsRequired();

            entity.Property(x => x.Status)
                  .IsRequired();

            entity.Property(x => x.DataCriacao)
                  .IsRequired();

            
            entity.HasIndex(x => x.IdempotencyKey)
                  .IsUnique()
                  .HasDatabaseName("UX_SolicitacoesCredito_IdempotencyKey");
        }
    }
}
