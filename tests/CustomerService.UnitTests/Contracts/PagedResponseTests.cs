using CustomerService.Api.Contracts;

namespace CustomerService.UnitTests.Contracts;

public class PagedResponseTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(5, 0, 0)]
    public void TotalPages_RoundsUp(int totalCount, int pageSize, int expected)
    {
        var page = new PagedResponse<int>([], 1, pageSize, totalCount);

        Assert.Equal(expected, page.TotalPages);
    }
}
