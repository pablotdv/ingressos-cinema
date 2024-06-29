using Common;
using SendGrid.Extensions.DependencyInjection;
using SendGrid.Helpers.Mail;
using VendaIngressosCinema;
using VendaIngressosCinema.Services;
using VendaIngressosCinemaRabbitMQ;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder.Services.Configure<RabbitMqSettings>(options => builder.Configuration.GetSection("RabbitMqSettings").Bind(options));
builder.Services.AddSingleton<RabbitMqConnectionManager>();
builder.Services.AddSingleton<EmailService>();

builder.Services.AddSendGrid(options =>
{
    options.ApiKey = builder.Configuration.GetValue<string>("SENDGRID_API_KEY");
});

builder.Services.AddHttpClient<PagamentoService>(options =>
{
    options.BaseAddress = new Uri(builder.Configuration.GetValue<string>("Pagamento:baseUrl"));
});


builder.Services
    .AddEntityFrameworkMongoDB()
    .AddMongoDB<IngressosContext>("mongodb://localhost:27017/local", "local", options => { });

var host = builder.Build();
host.Run();
