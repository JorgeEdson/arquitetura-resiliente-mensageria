using Microsoft.EntityFrameworkCore;

namespace worker_consumidor.Infraestrutura.Persistencia.SolicitacoesRejeitadas
{
    public class SolicitacoesRejeitadasDbContext : DbContext
    {
        public SolicitacoesRejeitadasDbContext(DbContextOptions<SolicitacoesRejeitadasDbContext> options)
           : base(options)
        {
        }

        public DbSet<Dominio.SolicitacaoRejeitada> SolicitacoesRejeitadas => Set<Dominio.SolicitacaoRejeitada>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SolicitacoesRejeitadasDbContext).Assembly);

            base.OnModelCreating(modelBuilder);
        }

    }
}
