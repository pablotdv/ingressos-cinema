using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace VendaIngressosCinema;

public class Ingresso
{    
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Evento { get; set; }
    public string Poltrona { get; set; }
    public Cliente Cliente { get; set; }
    public DateTime Data { get; set; }
    public double Valor { get; set; }
    public string CartaoCredito { get; set; }
}
