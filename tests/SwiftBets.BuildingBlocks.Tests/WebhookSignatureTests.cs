using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using SwiftBets.BuildingBlocks.Web.Webhooks;

namespace SwiftBets.BuildingBlocks.Tests;

public sealed class WebhookSignatureTests
{
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"event":"charge.success","data":{"reference":"dep_1"}}""");
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Bare_sha512_signature_matches_a_provider_that_signs_the_body_alone()
    {
        var expected = Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes("sk_test"), Body));

        new WebhookSignature(["sk_test"], WebhookHash.Sha512).Verify(Body, expected).ShouldBe(WebhookVerdict.Valid);
    }

    [Fact]
    public void A_changed_body_or_wrong_secret_does_not_verify()
    {
        var signature = new WebhookSignature(["s1"]).Sign(Body);

        new WebhookSignature(["s1"]).Verify([.. Body, (byte)' '], signature).ShouldBe(WebhookVerdict.Mismatch);
        new WebhookSignature(["s2"]).Verify(Body, signature).ShouldBe(WebhookVerdict.Mismatch);
        new WebhookSignature(["s1"]).Verify(Body, "not-hex").ShouldBe(WebhookVerdict.Malformed);
        new WebhookSignature(["s1"]).Verify(Body, null).ShouldBe(WebhookVerdict.Missing);
    }

    [Fact]
    public void During_rotation_the_old_and_new_secret_both_verify()
    {
        var rotating = new WebhookSignature(["new", "old"]);

        rotating.Verify(Body, new WebhookSignature(["old"]).Sign(Body)).ShouldBe(WebhookVerdict.Valid);
        rotating.Verify(Body, new WebhookSignature(["new"]).Sign(Body)).ShouldBe(WebhookVerdict.Valid);
    }

    [Fact]
    public void Timestamped_delivery_is_refused_outside_the_tolerance_so_it_cannot_be_replayed()
    {
        var signature = new WebhookSignature(["s"]);
        var header = signature.SignTimestamped(Body, Now);

        signature.VerifyTimestamped(Body, header, Now.AddMinutes(4), TimeSpan.FromMinutes(5)).ShouldBe(WebhookVerdict.Valid);
        signature.VerifyTimestamped(Body, header, Now.AddMinutes(6), TimeSpan.FromMinutes(5)).ShouldBe(WebhookVerdict.Expired);
        signature.VerifyTimestamped(Body, header.Replace("t=", "t=1", StringComparison.Ordinal), Now, TimeSpan.FromMinutes(5)).ShouldBe(WebhookVerdict.Mismatch);
        signature.VerifyTimestamped(Body, "v1=abc", Now, TimeSpan.FromMinutes(5)).ShouldBe(WebhookVerdict.Malformed);
    }

    [Fact]
    public async Task Raw_body_is_read_whole_and_an_oversized_one_is_refused()
    {
        (await Request(Body).ReadRawBodyAsync()).ShouldBe(Body);
        (await Request(new byte[2048]).ReadRawBodyAsync(limitBytes: 1024)).ShouldBeNull();
    }

    private static HttpRequest Request(byte[] body)
    {
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream(body);
        return context.Request;
    }
}
