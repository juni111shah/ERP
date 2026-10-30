using ERP.Application.Common;
using ERP.Modules.Purchasing.Features;

namespace ERP.UnitTests;

public class GoodsReceiptRulesTests
{
    [Theory]
    [InlineData(typeof(CreateGoodsReceipt))]
    [InlineData(typeof(DeleteGoodsReceipt))]
    public void Goods_receipt_commands_retry_on_a_concurrency_conflict(Type command)
        => Assert.True(typeof(IRetryOnConcurrency).IsAssignableFrom(command), command.Name);
}
