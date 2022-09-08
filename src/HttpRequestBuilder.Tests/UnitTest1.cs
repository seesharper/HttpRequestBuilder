namespace HttpRequestBuilder.Tests;

public class UnitTest1
{
    [Fact]
    public void ShouldBuildRequestWithHttpMethod()
    {
        var request = new HttpRequestBuilder().WithMethod(HttpMethod.Delete).Build();
        request.Method.Should().Be(HttpMethod.Delete);
    }
}