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
    public async Task ShouldReadProblemDetails()
    {
        var httpClient = new HttpClient(new ProblemDetailsHttpMessageHandler());
        await httpClient.SendAndHandleRequest(new HttpRequestMessage(HttpMethod.Get, "http://nrk.no"));
    }


    [Fact]
    public async Task ShouldHandleInvalidContent()
    {
        HttpResponseMessage responseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        responseMessage.Content = new StringContent("Rubbish");

        var test = await responseMessage.ContentAs<SampleContent>();

        test.Id.Should().Be(42);
        test.Name.Should().Be("SampleName");
    }
}

public record SampleContent(long Id, string Name);


public class ProblemDetailsHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var responseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
        {
            Content = JsonContent.Create(new ProblemDetails() { Title = "MyTitle", Detail = "MyDetails" })
        };
        return Task.FromResult(responseMessage);
    }
}


public class QueryParameterValueReturningNull
{
    public override string? ToString() => null;
}
