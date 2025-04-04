using System.Text;
using Nest;

public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IElasticClient _elasticClient;

    public LoggingMiddleware(RequestDelegate next, IElasticClient elasticClient)
    {
        _next = next;
        _elasticClient = elasticClient;
    }

    public async Task Invoke(HttpContext context)
    {
        var logData = new StringBuilder();
        var request = context.Request;

        try
        {
            logData.AppendLine("---HTTP Request Logging---");
            logData.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            logData.AppendLine($"Schema: {request.Scheme}");
            logData.AppendLine($"Host: {request.Host}");
            logData.AppendLine($"Path: {request.Path}");
            logData.AppendLine($"QueryString: {request.QueryString}");

            request.EnableBuffering();
            using (var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true))
            {
                var body = await reader.ReadToEndAsync();
                logData.AppendLine($"Request Body: {body}");
                request.Body.Position = 0;
            }
            logData.AppendLine($"{"-",30}");
        }
        catch (Exception ex)
        {
            logData.AppendLine($"ERROR: {ex.Message}");
        }
        finally
        {
            await WriteLogAsync(logData.ToString());
            await _next(context);
        }
    }

    private async Task WriteLogAsync(string logData)
    {
        try
        {
            var currentDate = DateTime.Now.ToString("yyyyMMdd");
            var logDirectory = "Logs";
            Directory.CreateDirectory(logDirectory);

            var logFile = Path.Combine(logDirectory, $"requests_{currentDate}.log");
            await File.AppendAllTextAsync(logFile, logData);

            var logEntry = new
            {
                Timestamp = DateTime.Now,
                LogMessage = logData
            };

            var response = await _elasticClient.IndexDocumentAsync(logEntry);
            if (!response.IsValid)
            {
                Console.WriteLine($"Elasticsearch logging failed: {response.ServerError?.Error?.Reason}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
}
