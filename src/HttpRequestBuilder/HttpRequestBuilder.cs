using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace HttpRequestBuilder;
public class HttpRequestBuilder
{
    private string requestUri = string.Empty;

    private const string DefaultAcceptHeader = "application/json";

    private readonly HttpRequestMessage requestMessage = new();

    private readonly Dictionary<string, string> queryParameters = new();

    public HttpRequestBuilder WithMethod(HttpMethod method)
    {
        requestMessage.Method = method;
        return this;
    }

    public HttpRequestBuilder WithRequestUri(string requestUri)
    {
        this.requestUri = requestUri;
        return this;
    }

    public HttpRequestBuilder AddQueryParameter<T>(string name, T value) where T : notnull
    {
        string? stringValue = value.ToString();
        if (stringValue is null)
        {
            throw new ArgumentException($"The parameter {nameof(value)} must return an non-null value from its `ToString()` method.");
        }
        queryParameters[name] = stringValue.ToString();
        return this;
    }
    public HttpRequestBuilder AddQueryParameter<T>(string name, params T[] values) where T : notnull
    {
        var result = "";
        foreach (var item in values)
        {
            string? stringValue = item.ToString();
            if (stringValue is null)
            {
                throw new ArgumentException($"The parameter {nameof(values)} must contain elements where all return an non-null value from its `ToString()` method.");
            }

            result += item.ToString() + ",";
        }
        result = result.Remove(result.Length - 1);
        queryParameters[name] = result;
        return this;
    }

    public HttpRequestBuilder AddHeader(string name, string value)
    {
        requestMessage.Headers.Add(name, value);
        return this;
    }

    public HttpRequestBuilder AddAcceptHeader(string acceptHeader)
    {
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));
        return this;
    }

    public HttpRequestBuilder WithJsonContent<T>(T content, MediaTypeHeaderValue? mediaType = null, JsonSerializerOptions? options = null) where T : class
    {
        requestMessage.Content = JsonContent.Create(content, mediaType, options);
        return this;
    }

    public HttpRequestMessage Build()
    {
        var queryString = BuildQueryString();
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            requestUri = $"{requestUri}?{queryString}";
        }

        if (requestMessage.Headers.Accept.Count == 0)
        {
            AddAcceptHeader(DefaultAcceptHeader);
        }

        requestMessage.RequestUri = new Uri(requestUri, UriKind.Relative);
        return requestMessage;
    }

    private string BuildQueryString()
    {
        if (queryParameters.Count == 0)
        {
            return string.Empty;
        }
        var encoder = UrlEncoder.Default;
        return queryParameters
            .Select(kvp => $"{encoder.Encode(kvp.Key)}={encoder.Encode(kvp.Value)}")
            .Aggregate((current, next) => $"{current}&{next}");
    }
}

public static class HttpResponseMessageResultExtensions
{
    public static async Task<T?> ContentAs<T>(this HttpResponseMessage response, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(options, cancellationToken);
        }
        catch (Exception ex)
        {
            var stringResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"There was a problem handling the request ('{response.RequestMessage?.RequestUri}') The status code was ({(int)response.StatusCode}) {response.StatusCode} and the raw response was ${stringResponse}", ex);
        }
    }
}


public static class HttpClientExtensions
{
    public static async Task<HttpResponseMessage> SendAndHandleRequest(this HttpClient client, HttpRequestMessage httpRequest, Func<HttpResponseMessage, bool>? isSuccessful = null, Action<ProblemDetails>? problemHandler = null, Func<HttpResponseMessage, Task>? exceptionHandler = null)
    {
        isSuccessful ??= (responseMessage) => responseMessage.IsSuccessStatusCode;
        problemHandler ??= (problemDetails) => throw new Exception(problemDetails.Detail);
        exceptionHandler ??= async (responseMessage) => await ReadAndThrowException(responseMessage);

        var response = await client.SendAsync(httpRequest);

        if (!isSuccessful(response))
        {
            ProblemDetails? problemDetails = await ReadProblemDetails(response);
            if (problemDetails is not null)
            {
                problemHandler(problemDetails);
            }
            else
            {
                await exceptionHandler(response);
            }
        }

        return response;
    }

    private static async Task ReadAndThrowException(HttpResponseMessage response)
    {
        var stringResponse = await response.Content.ReadAsStringAsync();
        throw new Exception($"There was a problem handling the request ('{response.RequestMessage?.RequestUri}') The status code was ({(int)response.StatusCode}) {response.StatusCode} and the raw response was ${stringResponse}");
    }


    private static async Task<ProblemDetails?> ReadProblemDetails(HttpResponseMessage responseMessage)
    {
        try
        {
            return await responseMessage.Content.ReadFromJsonAsync<ProblemDetails>();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
