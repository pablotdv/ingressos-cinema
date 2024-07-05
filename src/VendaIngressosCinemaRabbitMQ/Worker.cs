using System.Text;
using System.Text.Json;
using Common;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VendaIngressosCinema;
using VendaIngressosCinema.Services;

namespace VendaIngressosCinemaRabbitMQ;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly PagamentoService _pagamentoService;
    private readonly EmailService _emailService;
    private readonly IModel _channel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ResiliencePipeline<bool> _pipeline;

    private IngressoRequest ingressoRequest;

    public Worker(ILogger<Worker> logger,
        PagamentoService pagamentoService,
        EmailService emailService,
        RabbitMqConnectionManager rabbitMqConnectionManager,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _pagamentoService = pagamentoService;
        _emailService = emailService;


        _channel = rabbitMqConnectionManager.CreateModel();
        _channel.QueueDeclare(queue: "confirmacao-pagamento",
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);
        _serviceProvider = serviceProvider;

        _pipeline = new ResiliencePipelineBuilder<bool>()
            .AddFallback(new()
            {
                ShouldHandle = new PredicateBuilder<bool>().Handle<ValidarPoltronaException>(),
                FallbackAction = args =>
                {
                    _channel.BasicPublish(exchange: "",
                                         routingKey: "dl-ingresso-venda",
                                         basicProperties: null,
                                         body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ingressoRequest)));
                    return new ValueTask<Outcome<bool>>(Outcome.FromResult(false));
                }
            })
            .AddRetry(new()
            {
                ShouldHandle = new PredicateBuilder<bool>().Handle<ValidarPoltronaException>(),
                MaxRetryAttempts = 4,
                Delay = TimeSpan.FromSeconds(15),
            })
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IngressosContext>();
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            ingressoRequest = JsonSerializer.Deserialize<IngressoRequest>(message);

            var result = await _pipeline.ExecuteAsync(async token =>
            {
                return !await PoltronaReservada(ingressoRequest.IngressoId, context);
            }, stoppingToken);

            if (result) return;

            if (await PagamentoReprovado(ingressoRequest.IngressoId, context)) return;
            await EnviarEmail(ingressoRequest.IngressoId, context);
            Console.WriteLine($" [x] Received {message}");
        };
        _channel.BasicConsume(queue: "confirmacao-pagamento",
                             autoAck: true,
                             consumer: consumer);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task<bool> PoltronaReservada(Guid ingressoId, IngressosContext context)
    {
        var ingresso = await context.Ingressos.FindAsync(ingressoId);

        var fluxoValidarPoltrona = ingresso.Historicos.FirstOrDefault(a => a.Fluxo == Fluxo.ValidarPoltrona);

        if (fluxoValidarPoltrona == null)
        {
            throw new ValidarPoltronaException();
        }

        return fluxoValidarPoltrona.Status == "Poltrona reservada com sucesso";
    }

    private async Task<bool> PagamentoReprovado(Guid ingressoId, IngressosContext context)
    {
        var ingresso = await context.Ingressos.FindAsync(ingressoId);

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
            PagamentoResponse = pagamentoResponse,
            Fluxo = Fluxo.Pagamento
        });
        ingresso.Status = aprovado ? IngressoStatus.Aprovado : IngressoStatus.Rejeitado;
        context.Ingressos.Update(ingresso);
        await context.SaveChangesAsync();

        return !aprovado;
    }

    private async Task<bool> EnviarEmail(Guid ingressoId, IngressosContext context)
    {
        var ingresso = await context.Ingressos.FindAsync(ingressoId);

        var enviado = await _emailService.Enviar(ingresso);
        ingresso.Historicos.Add(new IngressoHistorico
        {
            Data = DateTime.Now,
            Status = enviado ? "Email enviado com sucesso" : "Falha ao enviar email",
            Fluxo = Fluxo.EnviarEmail
        });
        context.Ingressos.Update(ingresso);
        await context.SaveChangesAsync();
        return enviado;
    }
}


public class ValidarPoltronaException : Exception
{
    public ValidarPoltronaException() : base("Fluxo de validar poltrona não encontrado")
    {
    }
}