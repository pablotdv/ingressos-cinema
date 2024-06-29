using Microsoft.EntityFrameworkCore;
using VendaIngressosCinema;

namespace Common.Jobs;

public class ValidarPoltronaRequest
{
    public Guid Id { get; set; }
    public string Poltrona { get; set; }
    public string Evento { get; set; }
}

public class ValidarPoltronaJob
{
    private readonly IngressosContext _context;

    public ValidarPoltronaJob(IngressosContext context)
    {
        _context = context;
    }

    public async Task Validar(ValidarPoltronaRequest request)
    {
        var poltronaJaReservada = await _context.Ingressos
                        .Where(a => a.Poltrona == request.Poltrona)
                        .Where(a => a.Evento == request.Evento)
                        .Where(a => a.Id != request.Id)
                        .Where(a => a.Status == IngressoStatus.Aprovado || a.Status == IngressoStatus.Pendente)
                        .AnyAsync();

        var ingresso = await _context.Ingressos.FindAsync(request.Id);
        ingresso.Historicos.Add(new IngressoHistorico
        {
            Data = DateTime.Now,
            Status = poltronaJaReservada ? "Poltrona já reservada" : "Poltrona reservada com sucesso",
            Fluxo = Fluxo.ValidarPoltrona
        });
        ingresso.Status = poltronaJaReservada ? IngressoStatus.Rejeitado : ingresso.Status;
        _context.Ingressos.Update(ingresso);
        await _context.SaveChangesAsync();
    }
}
