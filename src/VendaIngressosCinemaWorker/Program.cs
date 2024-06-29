using Common;
using SendGrid.Extensions.DependencyInjection;
using VendaIngressosCinema;
using VendaIngressosCinema.Services;
using VendaIngressosCinemaWorker;

var builder = Host.CreateApplicationBuilder(args);


builder.Services.Configure<RabbitMqSettings>(options => builder.Configuration.GetSection("RabbitMqSettings").Bind(options));
builder.Services.AddSingleton<RabbitMqConnectionManager>();

builder.Services.AddHttpClient<AntifraudeService>(options =>
{
    options.BaseAddress = new Uri(builder.Configuration.GetValue<string>("Antifraude:baseUrl"));
});

builder.Services
    .AddEntityFrameworkMongoDB()
    .AddMongoDB<IngressosContext>("mongodb://localhost:27017/local", "local", options => { });

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
