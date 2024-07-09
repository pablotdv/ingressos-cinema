using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Serilog;
using System.IO;
using System.Threading.Tasks;

namespace VendaIngressosCinema;
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDiagnosticContext _diagnosticContext;

    public RequestResponseLoggingMiddleware(RequestDelegate next, IDiagnosticContext diagnosticContext)
    {
        _next = next;
        _diagnosticContext = diagnosticContext;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        var request = httpContext.Request;

        _diagnosticContext.Set("Host", request.Host);
        _diagnosticContext.Set("Protocol", request.Protocol);
        _diagnosticContext.Set("Scheme", request.Scheme);
        _diagnosticContext.Set("User-Agent", request.Headers.UserAgent);

        if (request.QueryString.HasValue)
        {
            _diagnosticContext.Set("QueryString", request.QueryString.Value);
        }

        _diagnosticContext.Set("ContentType", request.ContentType);

        string requestBodyPayload = await ReadRequestBody(request);
        _diagnosticContext.Set("RequestBody", requestBodyPayload);

        var endpoint = httpContext.Features.Get<IEndpointFeature>()?.Endpoint;
        if (endpoint is object)
        {
            _diagnosticContext.Set("EndpointName", endpoint.DisplayName);
        }


        await _next(httpContext);
    }

    private async Task<string> ReadRequestBody(HttpRequest request)
    {
        // Ensure the request's body can be read multiple times (for the next middlewares in the pipeline).
        request.EnableBuffering();

        using var streamReader = new StreamReader(request.Body, leaveOpen: true);
        var requestBody = await streamReader.ReadToEndAsync();

        // Reset the request's body stream position for next middleware in the pipeline.
        request.Body.Position = 0;
        return requestBody;
    }
}