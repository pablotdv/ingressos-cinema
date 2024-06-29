using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using VendaIngressosCinema.Services;

namespace VendaIngressosCinema;

public enum Fluxo
{
    ValidarPoltrona,
    Antifraude,
    Pagamento,
    EnviarEmail
}

public class IngressoHistorico
{
    public DateTime Data { get; set; }
    public Fluxo Fluxo { get; set; }
    public string Status { get; set; }
    public AntifraudeResponse? AntifraudeResponse { get; set; }
    public PagamentoResponse? PagamentoResponse { get; set; }
}

[BsonIgnoreExtraElements]
public class Ingresso
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Evento { get; set; }
    public string Poltrona { get; set; }
    public Cliente Cliente { get; set; }
    public DateTime Data { get; set; }
    public decimal Valor { get; set; }
    public string CartaoCredito { get; set; }
    [BsonRepresentation(BsonType.String)]
    public IngressoStatus Status { get; set; } = IngressoStatus.Pendente;
    public List<IngressoHistorico> Historicos { get; set; } = new();
}

public enum IngressoStatus
{
    Pendente,
    Aprovado,
    Cancelado,
    Rejeitado
}
