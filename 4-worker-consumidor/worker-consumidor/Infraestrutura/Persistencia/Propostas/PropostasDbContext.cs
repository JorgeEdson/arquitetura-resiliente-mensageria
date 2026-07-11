using Microsoft.EntityFrameworkCore;

namespace worker_consumidor.Infraestrutura.Persistencia.Propostas
{
    public class PropostasDbContext : DbContext
    {
        public PropostasDbContext(DbContextOptions<PropostasDbContext> options)
            : base(options)
        {
        }

        public DbSet<Dominio.Proposta> Propostas => Set<Dominio.Proposta>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {            
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(PropostasDbContext).Assembly);
        }

    }
}
