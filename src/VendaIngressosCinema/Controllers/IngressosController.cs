using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace VendaIngressosCinema.Controllers;

[ApiController]
[Route("[controller]")]
public class IngressosController : ControllerBase
{   
    private readonly ILogger<IngressosController> _logger;
    private readonly IngressosContext _context;

    public IngressosController(ILogger<IngressosController> logger, IngressosContext context)
    {
        _logger = logger;
        _context = context;
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

    // Create Ingresso
    [HttpPost]
    public async Task<ActionResult<Ingresso>> Post(Ingresso ingresso)
    {
        _context.Ingressos.Add(ingresso);
        await _context.SaveChangesAsync();

        return CreatedAtAction("Get", new { id = ingresso.Id }, ingresso);
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
