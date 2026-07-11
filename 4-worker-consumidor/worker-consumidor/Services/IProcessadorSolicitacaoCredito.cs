using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using worker_consumidor.Dominio;
using worker_consumidor.DTOs;
using worker_consumidor.Infraestrutura.Persistencia.Propostas;
using worker_consumidor.Infraestrutura.Persistencia.SolicitacoesRejeitadas;

namespace worker_consumidor.Services
{
    public interface IProcessadorSolicitacaoCredito
    {
        Task ProcessarAsync(
            string payloadJson,
            IDictionary<string, object> propriedades,
            CancellationToken cancellationToken);
    }

    public class ProcessadorSolicitacaoCredito : IProcessadorSolicitacaoCredito
    {
        // Códigos de erro do SQL Server para violação de unicidade.
        private const int SqlUniqueConstraintViolation = 2627;
        private const int SqlDuplicateKeyRowIndex = 2601;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ILogger<ProcessadorSolicitacaoCredito> _logger;
        private readonly PropostasDbContext _propostasDb;
        private readonly SolicitacoesRejeitadasDbContext _rejeitadasDb;

        public ProcessadorSolicitacaoCredito(
            ILogger<ProcessadorSolicitacaoCredito> logger,
            PropostasDbContext propostasDb,
            SolicitacoesRejeitadasDbContext rejeitadasDb)
        {
            _logger = logger;
            _propostasDb = propostasDb;
            _rejeitadasDb = rejeitadasDb;
        }

        public async Task ProcessarAsync(
            string payloadJson,
            IDictionary<string, object> propriedades,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Iniciando processamento da solicitação de crédito. PayloadLength={Length}.",
                payloadJson?.Length ?? 0);

            var dto = JsonSerializer.Deserialize<SolicitarCreditoMensagemDto>(payloadJson!, JsonOptions);

            if (dto is null)
            {
                _logger.LogError("Falha ao desserializar payload da solicitação de crédito.");
                throw new InvalidOperationException("Payload inválido para solicitação de crédito.");
            }

            if (string.IsNullOrWhiteSpace(dto.IdempotencyKey))
            {
                _logger.LogError("Mensagem sem IdempotencyKey. SolicitacaoCreditoId={SolicitacaoCreditoId}.", dto.SolicitacaoCreditoId);
                throw new InvalidOperationException("Mensagem sem IdempotencyKey.");
            }

            // ---- GATILHO DE DEMONSTRAÇÃO (apenas para a palestra) ----
            // IdCliente 666 simula uma falha permanente no processamento, para
            // demonstrar o retry (quorum) e a ida da mensagem para a DLQ.
            // Remover em cenário real.
            if (dto.IdCliente == 666)
            {
                _logger.LogWarning(
                    "Gatilho de demonstração acionado (IdCliente=666). Lançando falha simulada. SolicitacaoCreditoId={SolicitacaoCreditoId}.",
                    dto.SolicitacaoCreditoId);
                throw new InvalidOperationException("Falha simulada para demonstração de DLQ (IdCliente=666).");
            }
            // ----------------------------------------------------------

            // Regra de aprovação simples para demonstração.
            bool aprovada = dto.ValorSolicitado <= 20_000m && dto.PrazoMeses <= 36;

            if (aprovada)
                await TratarAprovadaAsync(dto, cancellationToken);
            else
                await TratarRejeitadaAsync(dto, MontarMotivoRejeicao(dto), cancellationToken);
        }

        private async Task TratarAprovadaAsync(
            SolicitarCreditoMensagemDto dto,
            CancellationToken cancellationToken)
        {
            var taxaAnual = ObterTaxaJurosAnual(dto.TipoProduto);
            var valorAprovado = dto.ValorSolicitado;
            var valorParcela = CalcularParcelaPrice(valorAprovado, taxaAnual, dto.PrazoMeses);
            var dataPrimeiraParcela = CalcularDataPrimeiraParcela(dto.DataSolicitacao);

            var proposta = new Proposta
            {
                Id = Guid.NewGuid(),
                SolicitacaoCreditoId = dto.SolicitacaoCreditoId,
                IdempotencyKey = dto.IdempotencyKey,

                IdCliente = dto.IdCliente,
                ValorSolicitado = dto.ValorSolicitado,
                PrazoMeses = dto.PrazoMeses,
                TipoProduto = dto.TipoProduto,
                DataSolicitacao = dto.DataSolicitacao,

                ValorAprovado = valorAprovado,
                TaxaJurosAnual = taxaAnual,
                ValorParcela = valorParcela,
                DataPrimeiraParcela = dataPrimeiraParcela,

                DataCriacaoProposta = DateTime.UtcNow,
                StatusProposta = "Aprovada"
            };

            _propostasDb.Propostas.Add(proposta);

            try
            {
                await _propostasDb.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Proposta aprovada e persistida. SolicitacaoCreditoId={SolicitacaoCreditoId}, IdCliente={IdCliente}, ValorAprovado={ValorAprovado}, TaxaAnual={TaxaAnual}, ValorParcela={ValorParcela}, DataPrimeiraParcela={DataPrimeiraParcela}",
                    dto.SolicitacaoCreditoId,
                    dto.IdCliente,
                    valorAprovado,
                    taxaAnual,
                    valorParcela,
                    dataPrimeiraParcela);
            }
            catch (DbUpdateException ex) when (EhViolacaoDeChaveUnica(ex))
            {
                // Consumidor idempotente: mensagem reentregue (at-least-once).
                // A proposta já existe; considera processada com sucesso.
                _logger.LogInformation(
                    "Proposta já processada anteriormente (idempotência). IdempotencyKey={IdempotencyKey}. Ignorando.",
                    dto.IdempotencyKey);
            }
        }

        private async Task TratarRejeitadaAsync(
            SolicitarCreditoMensagemDto dto,
            string motivoRejeicao,
            CancellationToken cancellationToken)
        {
            var rejeitada = new SolicitacaoRejeitada
            {
                Id = Guid.NewGuid(),
                SolicitacaoCreditoId = dto.SolicitacaoCreditoId,
                IdempotencyKey = dto.IdempotencyKey,

                IdCliente = dto.IdCliente,
                ValorSolicitado = dto.ValorSolicitado,
                PrazoMeses = dto.PrazoMeses,
                TipoProduto = dto.TipoProduto,
                DataSolicitacao = dto.DataSolicitacao,

                DataRejeicao = DateTime.UtcNow,
                MensagemRejeicao = motivoRejeicao
            };

            _rejeitadasDb.SolicitacoesRejeitadas.Add(rejeitada);

            try
            {
                await _rejeitadasDb.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Solicitação de crédito rejeitada e persistida. SolicitacaoCreditoId={SolicitacaoCreditoId}, IdCliente={IdCliente}, MotivoRejeicao={Motivo}",
                    dto.SolicitacaoCreditoId,
                    dto.IdCliente,
                    motivoRejeicao);
            }
            catch (DbUpdateException ex) when (EhViolacaoDeChaveUnica(ex))
            {
                _logger.LogInformation(
                    "Rejeição já processada anteriormente (idempotência). IdempotencyKey={IdempotencyKey}. Ignorando.",
                    dto.IdempotencyKey);
            }
        }

        private static bool EhViolacaoDeChaveUnica(DbUpdateException ex)
        {
            return ex.InnerException is SqlException sql &&
                   (sql.Number == SqlUniqueConstraintViolation ||
                    sql.Number == SqlDuplicateKeyRowIndex);
        }

        private static string MontarMotivoRejeicao(SolicitarCreditoMensagemDto dto)
        {
            if (dto.ValorSolicitado > 20_000m && dto.PrazoMeses > 36)
                return "Valor solicitado acima do limite e prazo maior que o permitido.";

            if (dto.ValorSolicitado > 20_000m)
                return "Valor solicitado acima do limite permitido para este perfil.";

            if (dto.PrazoMeses > 36)
                return "Prazo maior que o permitido para este tipo de crédito.";

            return "Solicitação não atende aos critérios de aprovação.";
        }

        private static decimal ObterTaxaJurosAnual(TipoProduto tipoProduto)
        {
            return tipoProduto switch
            {
                TipoProduto.CreditoPessoal => 24.0m,
                TipoProduto.EmprestimoConsignado => 18.0m,
                TipoProduto.FinanciamentoVeicular => 16.0m,
                TipoProduto.FinanciamentoImobiliario => 12.0m,
                TipoProduto.CartaoCredito => 30.0m,
                TipoProduto.AntecipacaoFGTS => 14.0m,
                TipoProduto.CreditoEmpresarial => 20.0m,
                _ => 22.0m
            };
        }

        private static DateTime CalcularDataPrimeiraParcela(DateTime dataSolicitacao)
        {
            var dataBase = DateTime.SpecifyKind(dataSolicitacao, DateTimeKind.Utc);
            return dataBase.AddMonths(1).Date;
        }

        private static decimal CalcularParcelaPrice(
            decimal valor,
            decimal taxaAnualPercent,
            int prazoMeses)
        {
            if (prazoMeses <= 0) return valor;

            var taxaMensal = (double)(taxaAnualPercent / 12m / 100m);

            if (taxaMensal <= 0.0)
                return Math.Round(valor / prazoMeses, 2);

            var pv = (double)valor;
            var n = prazoMeses;

            // Fórmula Price: PMT = PV * i / (1 - (1 + i)^-n)
            var numerador = pv * taxaMensal;
            var denominador = 1 - Math.Pow(1 + taxaMensal, -n);
            var pmt = numerador / denominador;

            return Math.Round((decimal)pmt, 2);
        }
    }
}
