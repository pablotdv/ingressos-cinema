using Common;
using Common.Jobs;
using Confluent.Kafka;
using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VendaIngressosCinema.Services;

namespace VendaIngressosCinema.Controllers;

[ApiController]
[Route("[controller]")]
public class IngressosController : ControllerBase
{
    private readonly ILogger<IngressosController> _logger;
    private readonly IngressosContext _context;
    private readonly AntifraudeService _antifraudeService;
    private readonly PagamentoService _pagamentoService;
    private readonly EmailService _emailService;
    private readonly IProducer<Null, String> _producer;

    private readonly IBackgroundJobClient _backgroundJobClient;

    public IngressosController(ILogger<IngressosController> logger, IngressosContext context, AntifraudeService antifraudeService, PagamentoService pagamentoService, EmailService emailService, IProducer<Null, string> producer, IBackgroundJobClient backgroundJobClient)
    {
        _logger = logger;
        _context = context;
        _antifraudeService = antifraudeService;
        _pagamentoService = pagamentoService;
        _emailService = emailService;
        _producer = producer;
        _backgroundJobClient = backgroundJobClient;
    }

    // Get All Ingressos
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Ingresso>>> Get()
    {
        return await _context.Ingressos.ToListAsync();
    }

    // Get Ingresso by Id
    [HttpGet("{id}")]
    public async Task<ActionResult<Ingresso>> Get(Guid id)
    {
        var ingresso = await _context.Ingressos.FindAsync(id);

        if (ingresso == null)
        {
            return NotFound();
        }

        return ingresso;
    }

    // Create ingresso async
    [HttpPost("async")]
    public async Task<ActionResult<Ingresso>> PostAsync(IngressoModel request)
    {
        var ingresso = new Ingresso
        {
            Evento = request.Evento,
            Poltrona = request.Poltrona,
            Cliente = new Cliente
            {
                Nome = request.Nome,
                Cpf = request.Cpf,
                Email = request.Email,
                DataNascimento = request.DataNascimento,
                Endereco = request.Endereco
            },
            Data = request.Data,
            Valor = request.Valor,
            CartaoCredito = request.CartaoCredito,
            Status = IngressoStatus.Pendente
        };
        _context.Ingressos.Add(ingresso);
        await _context.SaveChangesAsync();

        //
        var messageEvent = new IngressoRequest
        {
            IngressoId = ingresso.Id
        };

        var jsonSerializer = System.Text.Json.JsonSerializer.Serialize(messageEvent);

        var result = await _producer.ProduceAsync("ingresso-venda", new Message<Null, string> { Value = jsonSerializer });

        _backgroundJobClient.Enqueue<ValidarPoltronaJob>(x => x.Validar(new ValidarPoltronaRequest
        {
            Id = ingresso.Id,
            Poltrona = ingresso.Poltrona,
            Evento = ingresso.Evento,
        }));

        return CreatedAtAction("Get", new { id = ingresso.Id }, ingresso);
    }

    // Create Ingresso
    [HttpPost]
    public async Task<ActionResult<Ingresso>> Post(IngressoModel request)
    {
        var ingresso = new Ingresso
        {
            Evento = request.Evento,
            Poltrona = request.Poltrona,
            Cliente = new Cliente
            {
                Nome = request.Nome,
                Cpf = request.Cpf,
                Email = request.Email,
                DataNascimento = request.DataNascimento,
                Endereco = request.Endereco
            },
            Data = request.Data,
            Valor = request.Valor,
            CartaoCredito = request.CartaoCredito,
            Status = IngressoStatus.Pendente
        };

        _context.Ingressos.Add(ingresso);
        await _context.SaveChangesAsync();

        if (!await ValidarAntifraude(ingresso))
        {
            return BadRequest();
        }

        if (!await ReservarPoltrona(ingresso))
        {
            return BadRequest();
        }

        if (!await EfetuarPagamento(ingresso))
        {
            return BadRequest();
        }

        await EnviarEmail(ingresso);

        return CreatedAtAction("Get", new { id = ingresso.Id }, ingresso);
    }

    private async Task<bool> EnviarEmail(Ingresso ingresso)
    {
        var enviado = await _emailService.Enviar(ingresso);
        ingresso.Historicos.Add(new IngressoHistorico
        {
            Data = DateTime.Now,
            Status = enviado ? "Email enviado com sucesso" : "Falha ao enviar email"
        });
        _context.Ingressos.Update(ingresso);
        await _context.SaveChangesAsync();
        return enviado;
    }

    private async Task<bool> ReservarPoltrona(Ingresso ingresso)
    {
        var poltronaJaReservada = await _context.Ingressos
                        .Where(a => a.Poltrona == ingresso.Poltrona)
                        .Where(a => a.Evento == ingresso.Evento)
                        .Where(a => a.Id != ingresso.Id)
                        .Where(a => a.Status == IngressoStatus.Aprovado || a.Status == IngressoStatus.Pendente)
                        .AnyAsync();

        ingresso.Historicos.Add(new IngressoHistorico
        {
            Data = DateTime.Now,
            Status = poltronaJaReservada ? "Poltrona já reservada" : "Poltrona reservada com sucesso"
        });
        ingresso.Status = poltronaJaReservada ? IngressoStatus.Rejeitado : ingresso.Status;
        _context.Ingressos.Update(ingresso);
        await _context.SaveChangesAsync();

        return !poltronaJaReservada;
    }

    private async Task<bool> ValidarAntifraude(Ingresso ingresso)
    {
        var antifraudeRequest = new AntifraudeRequest
        {
            Cpf = ingresso.Cliente.Cpf,
            Nome = ingresso.Cliente.Nome,
            DataNascimento = ingresso.Cliente.DataNascimento.ToShortDateString(),
            Email = ingresso.Cliente.Email,
            CartaoCredito = ingresso.CartaoCredito
        };
        var antifraudeResponse = await _antifraudeService.ValidarIngresso(antifraudeRequest);
        var aprovado = antifraudeResponse.Status.Equals("aprovado");
        ingresso.Historicos.Add(new IngressoHistorico
        {
            Data = DateTime.Now,
            Status = aprovado ? "Aprovado Antifraude" : "Cancelado Antifraude",
            AntifraudeResponse = antifraudeResponse
        });
        ingresso.Status = !aprovado ? IngressoStatus.Rejeitado : ingresso.Status;
        _context.Ingressos.Update(ingresso);
        await _context.SaveChangesAsync();

        return aprovado;
    }

    private async Task<bool> EfetuarPagamento(Ingresso ingresso)
    {
        var pagamentoRequest = new PagamentoRequest
        {
            Cpf = ingresso.Cliente.Cpf,
            Nome = ingresso.Cliente.Nome,
            CartaoCredito = ingresso.CartaoCredito,
            ValorCompra = ingresso.Valor
        };
        var pagamentoResponse = await _pagamentoService.EfetuarPagamento(pagamentoRequest);
        var aprovado = pagamentoResponse.Status.Equals("aprovado");
        ingresso.Historicos.Add(new IngressoHistorico
        {
            Data = DateTime.Now,
            Status = aprovado ? "Pagamento aprovado" : "Pagamento reprovado",
            PagamentoResponse = pagamentoResponse
        });
        ingresso.Status = aprovado ? IngressoStatus.Aprovado : IngressoStatus.Rejeitado;
        _context.Ingressos.Update(ingresso);
        await _context.SaveChangesAsync();

        return aprovado;
    }

    // Update Ingresso
    [HttpPut("{id}")]
    public async Task<IActionResult> Put(Guid id, Ingresso ingresso)
    {
        if (id != ingresso.Id)
        {
            return BadRequest();
        }

        var ingressoAtual = await _context.Ingressos.FindAsync(id);
        if (ingressoAtual == null)
        {
            return NotFound();
        }
        ingressoAtual.CartaoCredito = ingresso.CartaoCredito;
        ingressoAtual.Cliente = ingresso.Cliente;
        ingressoAtual.Data = ingresso.Data;
        ingressoAtual.Evento = ingresso.Evento;
        ingressoAtual.Poltrona = ingresso.Poltrona;
        ingressoAtual.Valor = ingresso.Valor;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!IngressoExists(id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    // Delete Ingresso
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ingresso = await _context.Ingressos.FindAsync(id);

        if (ingresso == null)
        {
            return NotFound();
        }

        _context.Ingressos.Remove(ingresso);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    //IngressoExists
    private bool IngressoExists(Guid id)
    {
        return _context.Ingressos.Any(e => e.Id == id);
    }
}

public class IngressoModel
{
    public string Evento { get; set; }
    public string Poltrona { get; set; }
    public string Nome { get; set; }
    public string Cpf { get; set; }
    public string Email { get; set; }
    public DateTime DataNascimento { get; set; }
    public string Endereco { get; set; }
    public DateTime Data { get; set; }
    public decimal Valor { get; set; }
    public string CartaoCredito { get; set; }
}
