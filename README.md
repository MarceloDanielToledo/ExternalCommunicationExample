# External Communication using HttpClient ⚡

When working with external services or within a microservices architecture, effective communication through HTTP calls is paramount. This README guides you through the usage of the C# `HttpClient` class, starting with common mistakes and leading to best practices, including resilient patterns, logging improvements, and the use of `IHttpClientFactory` along with Polly.

## Common Mistakes in HttpClient Usage

The provided `PersonService` class demonstrates common pitfalls in `HttpClient` usage, such as inadequate lifecycle management, absence of exception handling, and repeated configuration information. The refactored code showcases the importance of dependency injection, proper exception handling, and configuration centralization.

```csharp
public class PersonService : IPersonService
{
    public async Task<ExternalResponse<GetPersonResponse>> GetName(string name)
    {
        HttpClient client = new();
        client.BaseAddress = new Uri("https://api.genderize.io");
        var response = await client.GetAsync($"/?name={name}");
        if (response.IsSuccessStatusCode)
        {
            // Deserialize and return the object
            return ExternalResponse<GetPersonResponse>.SuccessResponse(await ResultService.ToResult<GetPersonResponse>(response));
        }
        else
        {
            return ExternalResponse<GetPersonResponse>.ErrorResponse();
        }
    }

    public async Task<ExternalResponse<GetPersonResponse>> GetLastName(string lastname)
    {
        HttpClient client = new();
        client.BaseAddress = new Uri("https://api.genderize.io");
        var response = await client.GetAsync($"/?lastname={lastname}");
        if (response.IsSuccessStatusCode)
        {
            // Deserialize and return the object
            return ExternalResponse<GetPersonResponse>.SuccessResponse(await ResultService.ToResult<GetPersonResponse>(response));
        }
        else
        {
            return ExternalResponse<GetPersonResponse>.ErrorResponse();
        }
    }
}
```

### In this case, we notice several details:

* The lifecycle of the HttpClient is not managed; creating instances of the class in each method can lead to performance issues due to the lack of connection reuse. It can be reused through dependency injection.
* There is no exception handling in the code.
* IDisposable is not implemented, which is automatically generated using dependency injection or by using the using block.
* Configuration handling repeats the information of the external service URL.

## Using HttpClientFactory

Utilizing `IHttpClientFactory` helps manage `HttpClient` instances efficiently by creating a singleton instance and handling configurations. This section emphasizes the benefits of reusing instances, configuring timeouts, and using named clients for improved maintainability.

```csharp
    var externalUrl = configuration.GetSection("ExternalService:BaseURL").Value ?? throw new NullReferenceException();
    services.AddHttpClient($"{HttpConstants.ClientName}", client =>
    {
    client.BaseAddress = new Uri(externalUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
    });
```


Here, we defined the HttpClient instance with a name or "key" (named usage) that we will use across all services through the CreateClient() method.

We have already set the BaseAddress (in this case, we read it from a configuration file), but in addition, we have defined a timeout of 15 seconds (which should be much less!). Other configurations can be applied based on the specific case and requirements.


```csharp
public class PersonService(IHttpClientFactory httpClientFactory) : IPersonService
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

        public async Task<ExternalResponse<GetPersonResponse>> Get(string name)
        {
            try
            {
                using var response = await _httpClientFactory.CreateClient(HttpConstants.ClientName).GetAsync(PersonRoutes.Get(name));
                if (response.IsSuccessStatusCode)
                {
                    return ExternalResponse<GetPersonResponse>.SuccessResponse(await ResultService.ToResult<GetPersonResponse>(response));

                }
                else
                {
                    return ExternalResponse<GetPersonResponse>.ErrorResponse();
                }

            }
            catch (TaskCanceledException)
            {
                return ExternalResponse<GetPersonResponse>.Timeout();
            }
            catch (Exception)
            {
                return ExternalResponse<GetPersonResponse>.ExceptionError();
            }
        }
    }
```

Personally, I also prefer to store the routes after the base URL in another class (e.g., "PersonRoutes") to avoid typing errors. Additionally, I use static methods to set a variable within the URL path.

It's important not to forget about exception handling and its corresponding log. In this case, I handle it within the method and provide a response without error details.

## Modifying HTTP Communication Logs

In the case where we need to log each HTTP request to an external service, we almost always obtain the following 4 logs as output:

```
info: System.Net.Http.HttpClient.my-client.LogicalHandler[100]
      Start processing HTTP request GET https://api.genderize.io/?name=daniel

info: System.Net.Http.HttpClient.my-client.ClientHandler[100]
      Sending HTTP request GET https://api.genderize.io/?name=daniel

info: System.Net.Http.HttpClient.my-client.ClientHandler[101]
      Received HTTP response headers after 0.2839ms - 200

info: System.Net.Http.HttpClient.my-client.LogicalHandler[101]
      End processing HTTP request after 5.3191ms - 200
```

Personally, I prefer to see two records, saving the most important details of the request and response for better visualization in case it is needed.

```
=== External Request ===
method = GET
uri = https://api.genderize.io/?name=Daniel
-- headers
```

```
=== External Response ===
uri = https://api.genderize.io/?name=Daniel
method = GET
status = OK
-- headers
header = traceparent    value = System.String[]
-- body
body = {"count":1725175,"name":"Daniel","gender":"male","probability":1.0}
```

How do we achieve this? By adding a "LoggerHandler" to our HttpClient in the configuration we just discussed:

```csharp
services.AddTransient<HttpLoggerHandler>();
var externalUrl = configuration.GetSection("ExternalService:BaseURL").Value ?? throw new NullReferenceException();
            services.AddHttpClient($"{HttpConstants.ClientName}", client =>
            {
                client.BaseAddress = new Uri(externalUrl);
                client.Timeout = TimeSpan.FromSeconds(15);
            }).AddHttpMessageHandler<HttpLoggerHandler>();
```

I have done the same but for the example API, adding an `HttpLoggerMiddleware` in the `Program.cs` class, maintaining the same structure in log storage.


## Adding Resilience to Our Communication

When accessing external resources from our code, especially when there is a network connection involved, it is highly likely to face adverse situations. This implies that our system may experience failures not necessarily because the called service is not functioning correctly but simply due to connectivity issues. These eventualities can have significant consequences for our systems if not handled with caution.

In this context, the importance of the resilience pattern in a .NET system is highlighted. This approach focuses on designing systems capable of resisting and recovering from failures, providing robust mechanisms to handle connectivity issues, network errors, or other setbacks. Resilience becomes a fundamental aspect to ensure system stability and availability, even in adverse conditions. Implementing strategies such as proper exception handling, redundancy in communication, and the ability to retry are essential practices within the resilience pattern to strengthen the reliability of applications in distributed environments.

## Polly

How will we apply the resilience pattern in our .NET project? We will use Polly, which will help us detect errors and create strategies to deal with them.

Many configurations can be set up; in this case, I implemented something straightforward: a retry policy (2 times, with a 1-second interval between each), which we can then add to our `HttpClient` instance.


```csharp
services.AddTransient<HttpLoggerHandler>();
var retryPolicy = HttpPolicyExtensions.HandleTransientHttpError()
		                .WaitAndRetry(2, retryAttempt => TimeSpan.FromSeconds(1));
var externalUrl = configuration.GetSection("ExternalService:BaseURL").Value ?? throw new NullReferenceException();
services.AddHttpClient($"{HttpConstants.ClientName}", client =>
	{
		client.BaseAddress = new Uri(externalUrl);
		client.Timeout = TimeSpan.FromSeconds(15);
	}).AddHttpMessageHandler<HttpLoggerHandler>().AddPolicyHandler((IAsyncPolicy<HttpResponseMessage>)retryPolicy);
```


## Conclusions

In conclusion, proyect emphasizes the importance of adopting best practices when working with `HttpClient` in a .NET environment. Key takeaways include:

- Proper lifecycle management of `HttpClient` instances for optimal performance.
- Effective use of `IHttpClientFactory` for centralized configuration and improved maintainability.
- Customization of HTTP communication logs for enhanced visibility and troubleshooting.
- Implementation of resilience patterns, such as retry policies using Polly, to ensure robustness in the face of network uncertainties.

By following these guidelines, developers can build more reliable and resilient systems when communicating with external services or within a microservices architecture. These practices contribute to the overall stability and performance of the application in diverse and dynamic network environments.

## Bibliography and Information

- [HttpClient](https://learn.microsoft.com/es-es/dotnet/api/system.net.http.httpclient?view=net-8.0)
- [IHttpClientFactory](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests)
- [HttpMessageHandler](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpmessagehandler?view=net-8.0)
- [Polly](https://www.pollydocs.org/)

Consider giving a star ⭐, forking the repository, and staying tuned for updates!