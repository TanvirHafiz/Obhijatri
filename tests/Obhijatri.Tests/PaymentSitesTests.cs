using Obhijatri.Safety.PaymentLock;

namespace Obhijatri.Tests;

public class PaymentSitesTests
{
    [Theory]
    [InlineData("bkash.com")]
    [InlineData("pay.bkash.com")]
    [InlineData("www.paypal.com")]
    [InlineData("nagad.com.bd")]
    public void KnownPaymentSites_AreDetected(string host) => Assert.True(PaymentSites.IsPaymentSite(host));

    [Theory]
    [InlineData("prothomalo.com")]
    [InlineData("example.com")]
    [InlineData("bkash-verify.xyz")]
    public void OtherSites_AreNot(string host) => Assert.False(PaymentSites.IsPaymentSite(host));
}
