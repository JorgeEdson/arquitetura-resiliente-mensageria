using api_solicitacao_credito.DTOs;
using api_solicitacao_credito.Services;
using Microsoft.AspNetCore.Mvc;

namespace api_solicitacao_credito.Controllers
{
    [ApiController]
    [Route("api/solicitacoes-credito")]
    public class SolicitacaoCreditoController : ControllerBase
    {
        private readonly ISolicitacaoCreditoService _service;

        public SolicitacaoCreditoController(ISolicitacaoCreditoService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Post(
            [FromBody] SalvarSolicitacaoCreditoRequest request,
            CancellationToken cancellationToken)
        {
            var resultado = await _service.SalvarSolicitacaoCreditoAsync(request, cancellationToken);
            
            return Accepted(new
            {
                mensagem = resultado.Mensagem,
                jaExistia = resultado.JaExistia,
                idempotencyKey = resultado.IdempotencyKey,
                solicitacaoCreditoId = resultado.SolicitacaoCreditoId
            });
        }
    }
}
