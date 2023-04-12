using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace HttpRequestBuilder.Tests;

public class HttpRequestBuilderTests
{
    private const string TestUri = "/api";

    [Fact]
    public void ShouldSetHttpMethodGetAsDefault()
    {
        var request = new HttpRequestBuilder().Build();
        request.Method.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public void ShouldAddHeader()
    {
        var request = new HttpRequestBuilder()
            .AddHeader("CustomHeader", "CustomHeaderValue")
            .Build();
        request.Headers.Should().Contain(kvp => kvp.Key == "CustomHeader" && kvp.Value.Single() == "CustomHeaderValue");
    }

    [Fact]
    public void ShouldBuildRequestWithHttpMethod()
    {
        var request = new HttpRequestBuilder()
            .WithMethod(HttpMethod.Delete)
            .Build();
        request.Method.Should().Be(HttpMethod.Delete);
    }

    [Fact]
    public void ShouldSetJsonAsDefaultAcceptHeader()
    {
        var request = new HttpRequestBuilder().Build();
        request.Headers.Accept.Should().Contain(m => m.MediaType == "application/json");
    }

    [Fact]
    public void ShouldSetAcceptHeaderAcceptHeader()
    {
        var request = new HttpRequestBuilder()
            .AddAcceptHeader("text/html")
            .Build();
        request.Headers.Accept.Should().Contain(m => m.MediaType == "text/html");
    }

    [Fact]
    public void ShouldAddQueryParameter()
    {
        var request = new HttpRequestBuilder()
            .WithRequestUri(TestUri)
            .AddQueryParameter("queryParam1", 42)
            .Build();
        request.RequestUri.Should().Be($"{TestUri}?queryParam1=42");
    }

    [Fact]
    public void ShouldThrowExceptionWhenQueryParameterValueIsNull()
    {
        Action action = () =>
        {
            var request = new HttpRequestBuilder()
                .AddQueryParameter("queryParam1", new QueryParameterValueReturningNull());
        };

        action.Should().Throw<ArgumentException>();
    }


    [Fact]
    public void ShouldThrowExceptionWhenQueryParameterValuesIsNull()
    {
        Action action = () =>
        {
            var request = new HttpRequestBuilder()
                .AddQueryParameter("queryParam1", new QueryParameterValueReturningNull(), new QueryParameterValueReturningNull());
        };

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ShouldAddQueryParameterWithMultipleValues()
    {
        var request = new HttpRequestBuilder()
            .WithRequestUri(TestUri)
            .AddQueryParameter("queryParam1", 42, 84)
            .Build();
        request.RequestUri.Should().Be($"{TestUri}?queryParam1=42,84");
    }


    [Fact]
    public async Task ShouldAddJsonContent()
    {
        var sampleContent = new SampleContent(42, "SampleName");
        var request = new HttpRequestBuilder()
           .WithRequestUri(TestUri)
           .WithJsonContent(sampleContent)
           .Build();
        var content = await request.Content!.ReadFromJsonAsync<SampleContent>()!;
        content.Should().BeEquivalentTo(sampleContent);
    }

    [Fact]
    public async Task ShouldReadJsonContent()
    {
        HttpResponseMessage responseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        responseMessage.Content = JsonContent.Create(new SampleContent(42, "SampleName"));

        var test = await responseMessage.ContentAs<SampleContent>();

        test!.Id.Should().Be(42);
        test.Name.Should().Be("SampleName");
    }



    [Fact]
    public async Task ShouldHandleInvalidContent()
    {
        HttpResponseMessage responseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("Rubbish")
        };

        Func<Task> act = async () => await responseMessage.ContentAs<SampleContent>();
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ShouldHandleSuccess()
    {
        var httpClient = new HttpClient(new HttpResponseMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = JsonContent.Create(new SampleContent(42, "SampleName")) }));
        var response = await httpClient.SendAndHandleRequest(new HttpRequestMessage(HttpMethod.Get, "http://nrk.no"));
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

    }


    [Fact]
    public async Task ShouldHandleError()
    {
        var httpClient = new HttpClient(new HttpResponseMessageHandler(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest) { Content = new StringContent("Rubbish") }));
        Func<Task> act = async () => await httpClient.SendAndHandleRequest(new HttpRequestMessage(HttpMethod.Get, "http://nrk.no"));
        await act.Should().ThrowAsync<Exception>();
    }


    [Fact]
    public async Task ShouldReadProblemDetails()
    {
        var httpClient = new HttpClient(new ProblemDetailsHttpMessageHandler(new ProblemDetails() { Title = "MyTitle", Detail = "MyDetail", Status = 500, Extensions = { { "MyExtension", "MyExtensionValue" } } }));
        Func<Task> act = async () => await httpClient.SendAndHandleRequest(new HttpRequestMessage(HttpMethod.Get, "http://nrk.no"));
        await act.Should().ThrowAsync<ProblemDetailsException>().Where(e => e.Title == "MyTitle" && e.Detail == "MyDetail" && e.Status == 500 && e.RequestUrl == "http://nrk.no/" && e.Extensions.Count == 1);
    }
}

public record SampleContent(long Id, string Name);


public class ProblemDetailsHttpMessageHandler : HttpMessageHandler
{
    private readonly ProblemDetails _problemDetails;

    public ProblemDetailsHttpMessageHandler(ProblemDetails problemDetails)
    {
        _problemDetails = problemDetails;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var responseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
        {
            Content = JsonContent.Create(_problemDetails),
            RequestMessage = request
        };
        return Task.FromResult(responseMessage);
    }
}


public class HttpResponseMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _responseMessage;

    public HttpResponseMessageHandler(HttpResponseMessage responseMessage)
    {
        _responseMessage = responseMessage;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responseMessage);
    }
}


public class QueryParameterValueReturningNull
{
    public override string? ToString() => null;
}
