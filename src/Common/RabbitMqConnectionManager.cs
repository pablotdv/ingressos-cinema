using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Common;

public class RabbitMqConnectionManager
{
    private readonly ConnectionFactory _factory;
    private IConnection _connection;

    public RabbitMqConnectionManager(IOptions<RabbitMqSettings> options)
    {
        _factory = new ConnectionFactory
        {
            HostName = options.Value.Hostname,
            UserName = options.Value.Username,
            Password = options.Value.Password
        };
        _connection = _factory.CreateConnection();
    }

    public IModel CreateModel()
    {
        return _connection.CreateModel();
    }

    public void CloseConnection()
    {
        if (_connection != null)
        {
            _connection.Close();
            _connection = null;
        }
    }
}
