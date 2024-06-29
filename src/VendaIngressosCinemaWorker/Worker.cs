using System.Net;
using System.Text;
using System.Text.Json;
using Common;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver.Core.Bindings;
using RabbitMQ.Client;
using VendaIngressosCinema;
using VendaIngressosCinema.Services;

namespace VendaIngressosCinemaWorker;
public class Worker : BackgroundService
{
    private IngressosContext _context;
    private readonly ILogger<Worker> _logger;
    private readonly IConsumer<Null, string> _consumer;
    private readonly AntifraudeService _antifraudeService;
    private readonly RabbitMqConnectionManager _rabbitMqConnectionManager;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private readonly IModel _channel;

    public Worker(ILogger<Worker> logger, RabbitMqConnectionManager rabbitMqConnectionManager, IServiceScopeFactory serviceScopeFactory, AntifraudeService antifraudeService)
    {
        _logger = logger;

        var config = new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = "ingresso-venda-group",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
        _consumer = new ConsumerBuilder<Null, string>(config).Build();
        _consumer.Subscribe("ingresso-venda");

        _rabbitMqConnectionManager = rabbitMqConnectionManager;        
        _serviceScopeFactory = serviceScopeFactory;
        _antifraudeService = antifraudeService;        

        _channel = _rabbitMqConnectionManager.CreateModel();
        _channel.QueueDeclare(queue: "confirmacao-pagamento",
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var scope = _serviceScopeFactory.CreateScope())
        {
            _context = scope.ServiceProvider.GetRequiredService<IngressosContext>();
            
            _channel.QueueDeclare(queue: "confirmacao-pagamento",
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

            while (!stoppingToken.IsCancellationRequested)
            {
                var consumeResult = _consumer.Consume(stoppingToken);

                using var logscope = _logger.BeginScope("Kafka consumindo mensagemn: {Id}", Guid.NewGuid());
                try
                {
                    var request = JsonSerializer.Deserialize<IngressoRequest>(consumeResult.Message.Value);

                    var ingresso = await _context.Ingressos.FindAsync(request.IngressoId);
                    if (ingresso == null)
                    {
                        _logger.LogWarning("Ingresso não encontrado");
                        continue;
                    }

                    if (await AntifraudeReprovada(ingresso)) continue;

                    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
                    _channel.BasicPublish(exchange: string.Empty,
                        routingKey: "confirmacao-pagamento",
                        basicProperties: null,
                        body: body);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Erro ao deserializar mensagem {Mensagem}", consumeResult.Message.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar mensagem");                    
                }
            }
        }
    }

    private async Task<bool> AntifraudeReprovada(Ingresso ingresso)
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
            AntifraudeResponse = antifraudeResponse,
            Fluxo = Fluxo.Antifraude
        });
        ingresso.Status = !aprovado ? IngressoStatus.Rejeitado : ingresso.Status;
        _context.Ingressos.Update(ingresso);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Resultado antifraude {resultado} {id}", aprovado ? "Aprovado" : "Reprovado", antifraudeResponse.Id);
        return !aprovado;
    }

    

}
