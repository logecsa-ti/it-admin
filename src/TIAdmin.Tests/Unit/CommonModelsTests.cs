namespace TIAdmin.Tests.Unit;

using FluentAssertions;
using TIAdmin.Application.Common.Constants;
using TIAdmin.Application.Common.Models;
using Xunit;

public class PagedResultTests
{
    [Fact]
    public void TotalPages_ShouldRoundUp()
    {
        var result = new PagedResult<int>([1, 2, 3], page: 1, pageSize: 25, totalItems: 150);

        result.TotalPages.Should().Be(6);
        result.HasPrevious.Should().BeFalse();
        result.HasNext.Should().BeTrue();
    }

    [Fact]
    public void TotalPages_WhenExactMultiple_ShouldNotAddExtraPage()
    {
        var result = new PagedResult<int>([], page: 1, pageSize: 25, totalItems: 50);

        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public void TotalPages_WhenNoItems_ShouldBeZero()
    {
        var result = PagedResult<int>.Empty(page: 1, pageSize: 25);

        result.TotalPages.Should().Be(0);
        result.Items.Should().BeEmpty();
    }
}

public class PagedQueryTests
{
    [Theory]
    [InlineData(0, 25)]
    [InlineData(-5, 25)]
    [InlineData(500, 200)]
    [InlineData(50, 50)]
    public void PageSize_ShouldClampToValidRange(int input, int expected)
    {
        var query = new PagedQuery { PageSize = input };

        query.PageSize.Should().Be(expected);
    }

    [Fact]
    public void Skip_ShouldCalculateOffset()
    {
        var query = new PagedQuery { Page = 3, PageSize = 25 };

        query.Skip.Should().Be(50);
    }

    [Fact]
    public void NormalizeSearch_ShouldTrimAndReturnNullForWhitespace()
    {
        new PagedQuery { Search = "  laptop  " }.NormalizeSearch().Should().Be("laptop");
        new PagedQuery { Search = "   " }.NormalizeSearch().Should().BeNull();
        new PagedQuery().NormalizeSearch().Should().BeNull();
    }
}
