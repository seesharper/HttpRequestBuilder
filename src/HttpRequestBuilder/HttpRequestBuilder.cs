using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace HttpRequestBuilder;

/// <summary>
/// A builder class that can be used to build a <see cref="HttpRequestMessage"/> with a fluent interface. 
/// </summary>
public class HttpRequestBuilder
{
    private string _requestUri = string.Empty;

    private const string DefaultAcceptHeader = "application/json";

    private readonly HttpRequestMessage _requestMessage = new();

    private readonly Dictionary<string, string> _queryParameters = new();


    /// <summary>
    /// Sets the <see cref="HttpMethod"/> of the request. Defaults to <see cref="HttpMethod.Get"/>.
    /// </summary>
    /// <param name="method">The <see cref="HttpMethod"/> to be used for this request.</param>
    /// <returns>The <see cref="HttpRequestBuilder"/> for chaining calls.</returns>
    public HttpRequestBuilder WithMethod(HttpMethod method)
    {
        _requestMessage.Method = method;
        return this;
    }

    /// <summary>
    /// Sets the request uri. This will be appended to the base url.
    /// </summary>
    /// <param name="requestUri">The uri of the request</param>
    /// <returns>The <see cref="HttpRequestBuilder"/> for chaining calls.</returns>
    public HttpRequestBuilder WithRequestUri(string requestUri)
    {
        _requestUri = requestUri;
        return this;
    }

    /// <summary>
    /// Adds a query parameter to the request uri.
    /// </summary>
    /// <typeparam name="T">The type of value to be added.</typeparam>
    /// <param name="name">The name of he query parameter.</param>
    /// <param name="value">The query parameter value.</param>
    /// <returns>The <see cref="HttpRequestBuilder"/> for chaining calls.</returns>
    /// <exception cref="ArgumentException">Thrown if the string representation (ToString()) is null.</exception>
    public HttpRequestBuilder AddQueryParameter<T>(string name, T value) where T : notnull
    {
        string? stringValue = value.ToString();
        if (stringValue is null)
        {
            throw new ArgumentException($"The parameter {nameof(value)} must return an non-null value from its `ToString()` method.");
        }
        _queryParameters[name] = stringValue.ToString();
        return this;
    }
    /// <summary>
    /// Adds a list of query parameters to the request uri.
    /// </summary>
    /// <typeparam name="T">The type of the values to be added.</typeparam>
    /// <param name="name">The name of the query parameter.</param>
    /// <param name="values">A list of values </param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public HttpRequestBuilder AddQueryParameter<T>(string name, params T[] values) where T : notnull
    {
        var result = new StringBuilder();
        foreach (var item in values)
        {
            string? stringValue = item.ToString();
            if (stringValue is null)
            {
                throw new ArgumentException($"The parameter {nameof(values)} must contain elements where all return a non-null value from its `ToString()` method.");
            }

            result.Append(item.ToString());
            result.Append(',');
        }
        result.Length--; // remove the last comma
        _queryParameters[name] = result.ToString();
        return this;
    }


    /// <summary>
    /// Adds a header to the request.
    /// </summary>
    /// <param name="name">The name of the header.</param>
    /// <param name="value">The header value.</param>
    /// <returns></returns>
    public HttpRequestBuilder AddHeader(string name, string value)
    {
        _requestMessage.Headers.Add(name, value);
        return this;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="acceptHeader"></param>
    /// <returns></returns>
    public HttpRequestBuilder AddAcceptHeader(string acceptHeader)
    {
        _requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));
        return this;
    }

    /// <summary>
    /// Sets the request message content to JSON serialized from the specified object.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize to JSON.</typeparam>
    /// <param name="content">The object to serialize to JSON and use as the request message content.</param>
    /// <param name="mediaType">The media type of the JSON content. Defaults to 'application/json'.</param>
    /// <param name="options">The JSON serialization options to use.</param>
    /// <returns>A reference to this instance after the request message content has been set.</returns>
    public HttpRequestBuilder WithJsonContent<T>(T content, MediaTypeHeaderValue? mediaType = null, JsonSerializerOptions? options = null) where T : class
    {
        _requestMessage.Content = JsonContent.Create(content, mediaType, options);
        return this;
    }

    /// <summary>
    /// Builds the <see cref="HttpRequestMessage"/> instance using the configured settings.
    /// </summary>
    /// <returns>The configured <see cref="HttpRequestMessage"/>.</returns>
    public HttpRequestMessage Build()
    {
        var queryString = BuildQueryString();
        if (!string.IsNullOrWhiteSpace(queryString))
        {
            _requestUri = $"{_requestUri}?{queryString}";
        }

        if (_requestMessage.Headers.Accept.Count == 0)
        {
            AddAcceptHeader(DefaultAcceptHeader);
        }

        _requestMessage.RequestUri = new Uri(_requestUri, UriKind.Relative);
        return _requestMessage;
    }

    private string BuildQueryString()
    {
        if (_queryParameters.Count == 0)
        {
            return string.Empty;
        }
        var encoder = UrlEncoder.Default;
        return _queryParameters
            .Select(kvp => $"{encoder.Encode(kvp.Key)}={encoder.Encode(kvp.Value)}")
            .Aggregate((current, next) => $"{current}&{next}");
    }
}


/// <summary>
/// Extends the <see cref="HttpResponseMessage"/> class.
/// </summary>
public static class HttpResponseMessageResultExtensions
{
    /// <summary>
    /// Reads the response content as JSON and deserializes it to an instance of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the JSON response content to.</typeparam>
    /// <param name="response">The <see cref="HttpResponseMessage"/> to read the content from.</param>
    /// <param name="options">The <see cref="JsonSerializerOptions"/> to use.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    /// <returns>The deserialized instance of the specified type.</returns>
    /// <exception cref="Exception">Thrown if there was a problem handling the request or deserializing the response content.</exception>
    public static async Task<T?> ContentAs<T>(this HttpResponseMessage response, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(options, cancellationToken);
        }
        catch (Exception ex)
        {
            var stringResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"There was a problem handling the request ('{response.RequestMessage!.RequestUri}') The status code was ({(int)response.StatusCode}) {response.StatusCode} and the raw response was '{stringResponse}'", ex);
        }
    }
}

/// <summary>
/// Represents an exception that is thrown when a server returns a ProblemDetails response.
/// </summary>
public class ProblemDetailsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProblemDetailsException"/> class with the specified parameters.
    /// </summary>
    /// <param name="requestUrl">The URL of the request that resulted in the problem details response.</param>
    /// <param name="title">The problem details title.</param>
    /// <param name="detail">The problem details detail.</param>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="status">The HTTP status code returned by the server.</param>
    /// <param name="extensions">Additional key-value pairs associated with the problem details response.</param>
    public ProblemDetailsException(string? requestUrl, string? title, string? detail, string? message, int? status, IDictionary<string, object?> extensions) : base(message)
    {
        RequestUrl = requestUrl;
        Title = title;
        Detail = detail;
        Status = status;
        Extensions = extensions;
    }

    /// <summary>
    /// Gets the URL of the request that resulted in the problem details response.
    /// </summary>
    public string? RequestUrl { get; }

    /// <summary>
    /// Gets the problem details title.
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// Gets the problem details detail.
    /// </summary>
    public string? Detail { get; }

    /// <summary>
    /// Gets the HTTP status code returned by the server.
    /// </summary>
    public int? Status { get; }

    /// <summary>
    /// Gets additional key-value pairs associated with the problem details response.
    /// </summary>
    public IDictionary<string, object?> Extensions { get; }
}



/// <summary>
/// Extends the <see cref="HttpClient"/> class.
/// </summary>
public static class HttpClientExtensions
{

    /// <summary>
    /// Sends an HTTP request using the provided <see cref="HttpClient"/> instance, and handles any errors or problem details returned by the server.
    /// </summary>
    /// <param name="client">The <see cref="HttpClient"/> instance to use for sending the request.</param>
    /// <param name="httpRequest">The <see cref="HttpRequestMessage"/> to send.</param>
    /// <param name="isSuccessful">An optional function that determines whether the HTTP response is considered successful. By default, any response with a successful status code (2xx) is considered successful.</param>
    /// <param name="problemHandler">An optional action to handle any <see cref="ProblemDetails"/> objects returned by the server. By default, a <see cref="ProblemDetailsException"/> is thrown.</param>
    /// <param name="errorHandler">An optional function to handle any other errors or exceptions returned by the server. By default, a generic <see cref="Exception"/> is thrown with the raw response content.</param>
    /// <returns>The <see cref="HttpResponseMessage"/> returned by the server.</returns>
    public static async Task<HttpResponseMessage> SendAndHandleRequest(this HttpClient client, HttpRequestMessage httpRequest, Func<HttpResponseMessage, bool>? isSuccessful = null, Action<ProblemDetails>? problemHandler = null, Func<HttpResponseMessage, Task>? errorHandler = null)
    {
        isSuccessful ??= (responseMessage) => responseMessage.IsSuccessStatusCode;
        problemHandler ??= (problemDetails) => ReadProblemDetailsAndThrowException(httpRequest, problemDetails);
        errorHandler ??= async (responseMessage) => await ReadAndThrowException(responseMessage);

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
                await errorHandler(response);
            }
        }

        return response;
    }

    private static Action<ProblemDetails> ReadProblemDetailsAndThrowException(HttpRequestMessage httpRequest, ProblemDetails problemDetails)
    {
        StringBuilder message = new();

        message.AppendLine($"There was a problem handling the request {httpRequest.Method} ('{httpRequest!.RequestUri}')");

        if (problemDetails.Status is not null)
        {
            message.AppendLine($"Status Code: {problemDetails.Status} ({(HttpStatusCode)problemDetails.Status})");
        }

        message.AppendLine($"Title: {problemDetails.Title}");
        message.AppendLine($"Detail: {problemDetails.Detail}");
        if (problemDetails.Extensions.Count > 0)
        {
            message.AppendLine("The following extensions were found and could possibly provide more information about the problem: ");
            foreach (var extension in problemDetails.Extensions)
            {
                message.AppendLine($"{extension.Key} : {extension.Value}");
            }
        }

        throw new ProblemDetailsException(httpRequest!.RequestUri!.ToString(), problemDetails.Title, problemDetails.Detail, message.ToString(), problemDetails.Status, problemDetails.Extensions);
    }

    private static async Task ReadAndThrowException(HttpResponseMessage response)
    {
        var stringResponse = await response.Content.ReadAsStringAsync();
        throw new Exception($"There was a problem handling the request ('{response.RequestMessage!.RequestUri}') The status code was ({(int)response.StatusCode}) {response.StatusCode} and the raw response was {stringResponse}");
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
