using SendGrid.Extensions.DependencyInjection;
using VendaIngressosCinema;
using VendaIngressosCinema.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<AntifraudeService>(options =>
{
    options.BaseAddress = new Uri(builder.Configuration.GetValue<string>("Antifraude:baseUrl"));
});

builder.Services.AddHttpClient<PagamentoService>(options =>
{
    options.BaseAddress = new Uri(builder.Configuration.GetValue<string>("Pagamento:baseUrl"));
});

builder.Services.AddSendGrid(options =>
{
    options.ApiKey = builder.Configuration.GetValue<string>("SENDGRID_API_KEY");
});

builder.Services
    .AddEntityFrameworkMongoDB()
    .AddMongoDB<IngressosContext>("mongodb://localhost:27017/local", "local", options => { });

builder.Services.AddScoped<EmailService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
