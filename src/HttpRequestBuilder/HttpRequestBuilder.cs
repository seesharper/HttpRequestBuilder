using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

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

    public HttpRequestBuilder AddQueryParameter(string name, object value)
    {
        string? stringValue = value.ToString();
        if (stringValue is null)
        {
            throw new ArgumentException($"The parameter {nameof(value)} must return an non-null value from its `ToString()` method.");
        }
        queryParameters[name] = stringValue.ToString();
        return this;
    }
    public HttpRequestBuilder AddQueryParameters(string name, object[] values)
    {
        var result = "";
        foreach (var item in values)
        {
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

    public HttpRequestBuilder WithAcceptHeader(string acceptHeader)
    {
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));
        return this;
    }

    public HttpRequestBuilder WithJsonContent<T>(T content) where T : class
    {
        requestMessage.Content = new JsonContent(content);
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
            WithAcceptHeader(DefaultAcceptHeader);
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

    private class JsonContent : StringContent
    {
        public JsonContent(object value)
            : base(JsonSerializer.Serialize(value), Encoding.UTF8,
                "application/json")
        {
        }
    }
}

public static class HttpRequestBuilderExtensions
{
    public static HttpRequestBuilder WithTicketHeader(this HttpRequestBuilder builder, string ticket)
    {
        return builder.AddHeader("ticketheader", ticket);
    }
}
public static class HttpResponseMessageResultExtensions
{
    public static async Task<T> ContentAs<T>(this HttpResponseMessage response) where T : class
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Stream data = await response.Content.ReadAsStreamAsync();
        T? value = JsonSerializer.Deserialize<T>(data, options);
        if (value is null)
        {
            throw new InvalidOperationException("Failed to deserialize");
        }
        return value;
    }
}
