using Archon.Core.Pagination;

namespace Archon.Testing.Unit.Core.Pagination
{
    public sealed class PagedRequestTests
    {
        [Test]
        public void Constructor_ShouldUseDefaultValues()
        {
            PagedRequest request = new PagedRequest();

            Assert.That(request.Page, Is.EqualTo(1));
            Assert.That(request.PageSize, Is.EqualTo(20));
        }

        [Test]
        public void Page_ShouldClampToMinimumValue()
        {
            PagedRequest request = new PagedRequest
            {
                Page = 0
            };

            Assert.That(request.Page, Is.EqualTo(1));
        }

        [Test]
        public void PageSize_ShouldClampToMaximumValue()
        {
            PagedRequest request = new PagedRequest
            {
                PageSize = 999
            };

            Assert.That(request.PageSize, Is.EqualTo(200));
        }
    }
}
