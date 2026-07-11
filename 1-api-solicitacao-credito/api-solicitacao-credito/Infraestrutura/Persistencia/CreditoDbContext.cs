using api_solicitacao_credito.Dominio;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace api_solicitacao_credito.Infraestrutura.Persistencia
{
    public class CreditoDbContext : DbContext
    {
        public CreditoDbContext(DbContextOptions<CreditoDbContext> options)
        : base(options)
        {
        }

        public DbSet<SolicitacaoCredito> SolicitacoesCredito => Set<SolicitacaoCredito>();

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(CreditoDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }
    }
}
