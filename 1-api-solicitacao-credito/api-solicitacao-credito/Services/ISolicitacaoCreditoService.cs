using api_solicitacao_credito.Dominio;
using api_solicitacao_credito.DTOs;
using api_solicitacao_credito.Infraestrutura.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace api_solicitacao_credito.Services
{
    public interface ISolicitacaoCreditoService
    {
        Task<SalvarSolicitacaoResultado> SalvarSolicitacaoCreditoAsync(
            SalvarSolicitacaoCreditoRequest request,
            CancellationToken cancellationToken = default);
    }

    public record SalvarSolicitacaoResultado(
        bool JaExistia,
        string IdempotencyKey,
        Guid? SolicitacaoCreditoId,
        string Mensagem);

    public class SolicitacaoCreditoService : ISolicitacaoCreditoService
    {   
        private const int SqlUniqueConstraintViolation = 2627;
        private const int SqlDuplicateKeyRowIndex = 2601;

        private const string TipoMensagemSolicitacaoCriada = "SolicitacaoCreditoCriada";

        private readonly CreditoDbContext _dbContext;
        private readonly ILogger<SolicitacaoCreditoService> _logger;

        public SolicitacaoCreditoService(
           CreditoDbContext dbContext,
           ILogger<SolicitacaoCreditoService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SalvarSolicitacaoResultado> SalvarSolicitacaoCreditoAsync(
           SalvarSolicitacaoCreditoRequest request,
           CancellationToken cancellationToken = default)
        {
            if (request is null)
            {
                _logger.LogError("Request de solicitação de crédito veio nulo.");
                throw new ArgumentNullException(nameof(request));
            }

            _logger.LogInformation(
                "Recebendo solicitação de crédito. IdCliente={IdCliente}, Valor={ValorSolicitado}, Prazo={PrazoMeses}, TipoProduto={TipoProduto}, DataSolicitacao={DataSolicitacao}",
                request.IdCliente,
                request.ValorSolicitado,
                request.PrazoMeses,
                request.TipoProduto,
                request.DataSolicitacao);

            var idempotencyKey = GerarIdempotencyKey(request);

            _logger.LogInformation("IdempotencyKey gerada: {IdempotencyKey}", idempotencyKey);

            // 1) Entidade de negócio (o "porquê" da atomicidade).
            var solicitacao = new SolicitacaoCredito
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = idempotencyKey,
                IdCliente = request.IdCliente,
                ValorSolicitado = request.ValorSolicitado,
                PrazoMeses = request.PrazoMeses,
                TipoProduto = request.TipoProduto,
                DataSolicitacao = request.DataSolicitacao,
                Status = StatusSolicitacao.Recebida,
                DataCriacao = DateTime.UtcNow
            };

            // 2) Evento correspondente, na MESMA transação (Outbox).
            var evento = new SolicitacaoCreditoCriadaEvento(
                SolicitacaoCreditoId: solicitacao.Id,
                IdempotencyKey: idempotencyKey,
                IdCliente: solicitacao.IdCliente,
                ValorSolicitado: solicitacao.ValorSolicitado,
                PrazoMeses: solicitacao.PrazoMeses,
                TipoProduto: solicitacao.TipoProduto,
                DataSolicitacao: solicitacao.DataSolicitacao);

            var payloadJson = JsonSerializer.Serialize(evento);

            var outbox = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                SolicitacaoCreditoId = solicitacao.Id,
                IdempotencyKey = idempotencyKey,
                TipoMensagem = TipoMensagemSolicitacaoCriada,
                Payload = payloadJson,
                Status = OutboxStatus.Pendente,
                DataCriacao = DateTime.UtcNow
            };

            _dbContext.SolicitacoesCredito.Add(solicitacao);
            _dbContext.OutboxMessages.Add(outbox);

            try
            {
                // Um único SaveChangesAsync => uma única transação.
                // Ou grava solicitação + outbox juntos, ou nada.
                var linhas = await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Solicitação e mensagem Outbox persistidas atomicamente. SolicitacaoId={SolicitacaoId}, OutboxId={OutboxId}, LinhasAfetadas={Linhas}",
                    solicitacao.Id,
                    outbox.Id,
                    linhas);

                return new SalvarSolicitacaoResultado(
                    JaExistia: false,
                    IdempotencyKey: idempotencyKey,
                    SolicitacaoCreditoId: solicitacao.Id,
                    Mensagem: "Solicitação de crédito recebida com sucesso e será processada de forma assíncrona.");
            }
            catch (DbUpdateException ex) when (EhViolacaoDeChaveUnica(ex))
            {
                // Idempotência sem SELECT prévio: a barreira é o índice UNIQUE.
                // A transação já sofreu rollback; nada foi gravado.
                _logger.LogInformation(
                    "Solicitação duplicada detectada pela violação do índice único. IdempotencyKey={IdempotencyKey}. Tratando como já recebida.",
                    idempotencyKey);

                return new SalvarSolicitacaoResultado(
                    JaExistia: true,
                    IdempotencyKey: idempotencyKey,
                    SolicitacaoCreditoId: null,
                    Mensagem: "Solicitação de crédito já havia sido recebida anteriormente.");
            }
        }

        private static bool EhViolacaoDeChaveUnica(DbUpdateException ex)
        {
            return ex.InnerException is SqlException sql &&
                   (sql.Number == SqlUniqueConstraintViolation ||
                    sql.Number == SqlDuplicateKeyRowIndex);
        }

        private static string GerarIdempotencyKey(SalvarSolicitacaoCreditoRequest request)
        {
            var dataRef = request.DataSolicitacao;

            if (dataRef == default)
                dataRef = DateTime.UtcNow;

            var dataUtc = DateTime.SpecifyKind(dataRef, DateTimeKind.Utc).ToUniversalTime();

            var baseUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Janela de 48h: casa com a política de expurgo da Outbox.
            long janela48h = (long)(dataUtc - baseUtc).TotalHours / 48;

            var raw = $"{request.IdCliente}|{request.ValorSolicitado}|{request.PrazoMeses}|{request.TipoProduto}|W{janela48h}";

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(raw);
            var hash = sha.ComputeHash(bytes);

            return Convert.ToHexString(hash);
        }
    }
}
